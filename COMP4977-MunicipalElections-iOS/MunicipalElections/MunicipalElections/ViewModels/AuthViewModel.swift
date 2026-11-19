import Foundation
import Combine

@MainActor
final class AuthViewModel: ObservableObject {
    @Published var email = ""
    @Published var password = ""
    @Published var name = ""

    @Published private(set) var isLoading = false
    @Published var errorMessage: String? = nil
    @Published private(set) var accountCreated = false

    private let authService: AuthService
    private var registeredCredentials: (email: String, password: String)?

    init(authService: AuthService = .shared) {
        self.authService = authService
    }

    var trimmedEmail: String { email.trimmingCharacters(in: .whitespacesAndNewlines) }
    private var trimmedName: String { name.trimmingCharacters(in: .whitespacesAndNewlines) }

    var isEmailValid: Bool {
        trimmedEmail.count <= 100
            && trimmedEmail.range(of: #"^[^@\s]+@[^@\s]+\.[^@\s]+$"#, options: .regularExpression) != nil
    }

    var isLoginValid: Bool {
        isEmailValid && !password.isEmpty
    }

    /// Mirrors ASP.NET Identity's default password rules so the user sees the
    /// problem before the request instead of a server validation error.
    var passwordRequirementMessage: String? {
        var missing: [String] = []
        if password.count < 6 { missing.append("6+ characters") }
        if password.rangeOfCharacter(from: .uppercaseLetters) == nil { missing.append("an uppercase letter") }
        if password.rangeOfCharacter(from: .lowercaseLetters) == nil { missing.append("a lowercase letter") }
        if password.rangeOfCharacter(from: .decimalDigits) == nil { missing.append("a number") }
        if password.rangeOfCharacter(from: .alphanumerics.inverted) == nil { missing.append("a symbol") }
        return missing.isEmpty ? nil : "Password needs " + missing.joined(separator: ", ") + "."
    }

    var isRegisterValid: Bool {
        !trimmedName.isEmpty && isEmailValid && passwordRequirementMessage == nil
    }

    func login() async -> String? {
        guard !isLoading, isLoginValid else { return nil }
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            return try await authService.login(email: trimmedEmail, password: password)
        } catch {
            errorMessage = error.localizedDescription
            return nil
        }
    }

    func register() async -> String? {
        guard !isLoading else { return nil }
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            if registeredCredentials == nil {
                guard isRegisterValid else { return nil }
                // Capture the credentials before either request suspends.
                let credentials = (email: trimmedEmail, password: password)
                try await authService.register(fullName: trimmedName, email: credentials.email,
                                               password: credentials.password)
                registeredCredentials = credentials
                accountCreated = true
            }
            // Retry only login if account creation succeeded but sign-in failed.
            guard let registeredCredentials else { return nil }
            return try await authService.login(email: registeredCredentials.email,
                                               password: registeredCredentials.password)
        } catch {
            errorMessage = accountCreated
                ? "Your account was created, but sign-in did not finish. Tap Finish Signing In to retry. \(error.localizedDescription)"
                : error.localizedDescription
            return nil
        }
    }
}
