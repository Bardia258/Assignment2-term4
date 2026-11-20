import Foundation
import Combine

extension Notification.Name {
    /// Posted by `APIClient` when an authorized request finds the JWT expired or rejected (401).
    static let sessionExpired = Notification.Name("MunicipalElections.sessionExpired")
}

/// The signed-in user for the whole app. The JWT lives in the Keychain; this
/// object publishes it to SwiftUI and owns logout, including expiry handling.
@MainActor
final class AuthSession: ObservableObject {
    nonisolated static let expiredMessage = "Your session has expired. Please log in again to continue."

    @Published private(set) var token: String?
    @Published private(set) var email: String?
    /// One-off message for the user (e.g. why they were logged out). Shown as an alert.
    @Published var notice: String?

    var isLoggedIn: Bool { token != nil }

    private let firebaseAuth: FirebaseAuthService
    private let authService: AuthService
    private var expiryTask: Task<Void, Never>?
    private var cancellables = Set<AnyCancellable>()

    init(firebaseAuth: FirebaseAuthService = .shared, authService: AuthService = .shared) {
        self.firebaseAuth = firebaseAuth
        self.authService = authService
        Self.removeLegacyPlaintextCredentials()
        NotificationCenter.default.publisher(for: .sessionExpired)
            .receive(on: DispatchQueue.main)
            .sink { [weak self] notification in
                // An old request must not sign out a newer login.
                guard let self else { return }
                if let rejectedToken = notification.object as? String,
                   rejectedToken != self.token { return }
                self.expire()
            }
            .store(in: &cancellables)
        restore()
    }

    /// Called after both the API and Firebase accepted the credentials.
    func signIn(token: String, email: String) throws {
        let normalizedEmail = email.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        guard !JWT.isExpired(token), !normalizedEmail.isEmpty,
              firebaseAuth.currentEmail == normalizedEmail else {
            throw APIError.sessionExpired
        }
        do {
            try KeychainService.save(token, for: .authToken)
            try KeychainService.save(normalizedEmail, for: .userEmail)
        } catch {
            logout()
            throw error
        }
        self.token = token
        self.email = normalizedEmail
        notice = nil
        scheduleExpiry(for: token)
    }

    func logout(notice: String? = nil) {
        expiryTask?.cancel()
        expiryTask = nil
        KeychainService.delete(.authToken)
        KeychainService.delete(.userEmail)
        firebaseAuth.signOut()
        token = nil
        email = nil
        self.notice = notice
    }

    /// Re-checks the token, e.g. when the app returns to the foreground
    /// (the expiry timer doesn't fire while the app is suspended).
    func checkExpiry() {
        if let token, JWT.isExpired(token) { expire() }
    }

    /// Verify the JWT with the original API, including the backend's security
    /// stamp/lockout checks, and bind the Firebase email to the API account.
    func validateWithServer() async {
        guard let checkedToken = token, let checkedEmail = email else { return }
        checkExpiry()
        guard token == checkedToken else { return }
        do {
            let user = try await authService.currentUser()
            guard token == checkedToken, !Task.isCancelled else { return }
            guard !user.id.isEmpty, user.email.lowercased() == checkedEmail.lowercased() else {
                logout(notice: "Your election-server account and saved-selections account don't match. Please log in again.")
                return
            }
        } catch let error as APIError where error.isUnauthorized {
            if token == checkedToken { expire() }
        } catch {
            // Do not discard a locally valid login for a network failure or
            // 404 while the Azure deployment is being updated. An API 401 and
            // the local expiry timer still sign out. Deploy /api/auth/me to
            // enable authoritative launch/foreground validation.
        }
    }

    // MARK: - Private

    private func expire() {
        guard isLoggedIn else { return }
        logout(notice: Self.expiredMessage)
    }

    private func restore() {
        guard let token = KeychainService.read(.authToken),
              let email = KeychainService.read(.userEmail) else {
            // No API session: don't leave a stray Firebase session behind.
            logout()
            return
        }
        if JWT.isExpired(token) {
            logout(notice: Self.expiredMessage)
        } else if firebaseAuth.currentEmail != email.lowercased() {
            // Firestore rules need the Firebase user; without it, ask for a fresh login.
            logout(notice: "Please log in again to continue.")
        } else {
            self.token = token
            self.email = email
            scheduleExpiry(for: token)
            // ContentView validates with the server before loading user data.
        }
    }

    private func scheduleExpiry(for token: String) {
        expiryTask?.cancel()
        guard let expiry = JWT.expirationDate(of: token) else { return }
        expiryTask = Task { [weak self] in
            let delay = expiry.addingTimeInterval(-JWT.leeway).timeIntervalSinceNow
            if delay > 0 { try? await Task.sleep(for: .seconds(delay)) }
            guard !Task.isCancelled else { return }
            self?.expire()
        }
    }

    /// A1 kept the JWT in plain text in UserDefaults. Wipe it on first launch of A2.
    private static func removeLegacyPlaintextCredentials() {
        UserDefaults.standard.removeObject(forKey: "authToken")
        UserDefaults.standard.removeObject(forKey: "userEmail")
    }
}
