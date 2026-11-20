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
    private var expiryTask: Task<Void, Never>?
    private var cancellables = Set<AnyCancellable>()

    init(firebaseAuth: FirebaseAuthService = .shared) {
        self.firebaseAuth = firebaseAuth
        Self.removeLegacyPlaintextCredentials()
        NotificationCenter.default.publisher(for: .sessionExpired)
            .receive(on: DispatchQueue.main)
            .sink { [weak self] _ in self?.expire() }
            .store(in: &cancellables)
        restore()
    }

    /// Called after both the API and Firebase accepted the credentials.
    func signIn(token: String, email: String) throws {
        try KeychainService.save(token, for: .authToken)
        try KeychainService.save(email, for: .userEmail)
        self.token = token
        self.email = email
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

    #if DEBUG
    /// Lets testers demo the expiry flow without waiting 60 minutes.
    func simulateExpiry() { expire() }
    #endif

    // MARK: - Private

    private func expire() {
        guard isLoggedIn else { return }
        logout(notice: Self.expiredMessage)
    }

    private func restore() {
        guard let token = KeychainService.read(.authToken),
              let email = KeychainService.read(.userEmail) else {
            // No API session: don't leave a stray Firebase session behind.
            if firebaseAuth.currentEmail != nil { firebaseAuth.signOut() }
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
            // TODO(api-auth-me): call validateWithServer() here once GET /api/auth/me is deployed.
        }
    }

    // TODO(api-auth-me): server-side session check at launch, not built yet because the
    // partner hasn't deployed GET /api/auth/me. Planned behaviour:
    //   - 200        -> keep the session.
    //   - 401        -> logout(notice: Self.expiredMessage)  ("Session expired").
    //   - 404 / 5xx / offline -> keep the session; the token is still valid locally and
    //     the endpoint may just be missing or unreachable.
    // APIClient already posts `.sessionExpired` on a 401 for any request that carried
    // the token, so the 401 branch only needs `requiresAuth: true` on that call.

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
