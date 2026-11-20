import Foundation
import Combine

/// Drives the Login and Register forms. A successful sign-in is two steps:
/// 1. the ASP.NET API returns a JWT, then
/// 2. Firebase Auth signs in (or creates) the same email/password user,
/// and only then is the session saved, so the app never has half a login.
@MainActor
final class AuthViewModel: ObservableObject {
    @Published var email = ""
    @Published var password = ""
    @Published var name = ""

    @Published private(set) var isLoading = false
    @Published var errorMessage: String? = nil
    @Published private(set) var accountCreated = false

    private let authService: AuthService
    private let firebaseAuth: FirebaseAuthService
    private var registeredCredentials: (email: String, password: String)?

    init(authService: AuthService = .shared, firebaseAuth: FirebaseAuthService = .shared) {
        self.authService = authService
        self.firebaseAuth = firebaseAuth
    }

    var trimmedEmail: String { email.trimmingCharacters(in: .whitespacesAndNewlines) }
    /// Firebase stores emails in lower case and Firestore rules compare them, so normalize once here.
    private var normalizedEmail: String { trimmedEmail.lowercased() }
    private var trimmedName: String { name.trimmingCharacters(in: .whitespacesAndNewlines) }

    var isEmailValid: Bool {
        trimmedEmail.count <= 100
            && trimmedEmail.range(of: #"^[^@\s]+@[^@\s]+\.[^@\s]+$"#, options: .regularExpression) != nil
    }

    var isLoginValid: Bool {
        isEmailValid && !password.isEmpty && password.count <= 128
    }

    /// Mirrors ASP.NET Identity's default password rules so the user sees the
    /// problem before the request instead of a server validation error.
    var passwordRequirementMessage: String? {
        var missing: [String] = []
        if password.count < 6 { missing.append("6+ characters") }
        if password.count > 128 { return "Password cannot exceed 128 characters." }
        if password.rangeOfCharacter(from: .uppercaseLetters) == nil { missing.append("an uppercase letter") }
        if password.rangeOfCharacter(from: .lowercaseLetters) == nil { missing.append("a lowercase letter") }
        if password.rangeOfCharacter(from: .decimalDigits) == nil { missing.append("a number") }
        if password.rangeOfCharacter(from: .alphanumerics.inverted) == nil { missing.append("a symbol") }
        return missing.isEmpty ? nil : "Password needs " + missing.joined(separator: ", ") + "."
    }

    var isRegisterValid: Bool {
        !trimmedName.isEmpty && trimmedName.count <= 200
            && isEmailValid && passwordRequirementMessage == nil
    }

    func login(session: AuthSession) async {
        guard !isLoading, isLoginValid else { return }
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            try await completeSignIn(email: normalizedEmail, password: password, session: session)
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func register(session: AuthSession) async {
        guard !isLoading else { return }
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            if registeredCredentials == nil {
                guard isRegisterValid else { return }
                // Capture the credentials before either request suspends.
                let credentials = (email: normalizedEmail, password: password)
                try await authService.register(fullName: trimmedName, email: credentials.email,
                                               password: credentials.password)
                registeredCredentials = credentials
                accountCreated = true
            }
            // If the account exists but sign-in failed, "Finish Signing In" retries only this part.
            guard let registeredCredentials else { return }
            try await completeSignIn(email: registeredCredentials.email,
                                     password: registeredCredentials.password, session: session)
        } catch {
            errorMessage = accountCreated
                ? "Your account was created, but sign-in did not finish. Tap Finish Signing In to retry. \(error.localizedDescription)"
                : error.localizedDescription
        }
    }

    private func completeSignIn(email: String, password: String, session: AuthSession) async throws {
        let token = try await authService.login(email: email, password: password)
        try await firebaseAuth.signInOrCreate(email: email, password: password)
        do {
            try session.signIn(token: token, email: email)
        } catch {
            firebaseAuth.signOut()
            throw error
        }
        self.password = ""
        registeredCredentials = nil
    }
}
