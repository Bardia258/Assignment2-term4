import Foundation
import FirebaseAuth

/// Mirrors the API account in Firebase Auth (email/password) so Firestore
/// security rules can identify the user through `request.auth.token.email`.
final class FirebaseAuthService {
    static let shared = FirebaseAuthService()

    private var auth: Auth { FirebaseService.auth }

    var currentEmail: String? { auth.currentUser?.email?.lowercased() }

    /// Signs in to Firebase with the same credentials the API just accepted,
    /// creating the Firebase user the first time.
    func signInOrCreate(email: String, password: String) async throws {
        do {
            _ = try await auth.signIn(withEmail: email, password: password)
        } catch let error as NSError where Self.mayMeanNoAccount(error) {
            do {
                _ = try await auth.createUser(withEmail: email, password: password)
            } catch let createError as NSError where createError.code == AuthErrorCode.Code.emailAlreadyInUse.rawValue {
                // The Firebase user exists but its password differs from the API's.
                throw FirebaseAuthError(message: "You're signed in to the election server, but your saved-selections account uses a different password. Please contact support to reset it.")
            } catch {
                throw FirebaseAuthError(error)
            }
        } catch {
            throw FirebaseAuthError(error)
        }
    }

    func signOut() {
        try? auth.signOut()
    }

    /// With email-enumeration protection (on by default for new projects) a
    /// missing user comes back as `invalidCredential` rather than `userNotFound`.
    private static func mayMeanNoAccount(_ error: NSError) -> Bool {
        guard error.domain == AuthErrorDomain else { return false }
        return error.code == AuthErrorCode.Code.userNotFound.rawValue
            || error.code == AuthErrorCode.Code.invalidCredential.rawValue
    }
}

struct FirebaseAuthError: LocalizedError {
    let message: String
    var errorDescription: String? { message }

    init(message: String) { self.message = message }

    init(_ error: Error) {
        let nsError = error as NSError
        guard nsError.domain == AuthErrorDomain, let code = AuthErrorCode.Code(rawValue: nsError.code) else {
            message = "Couldn't connect your saved selections. Please try again."
            return
        }
        switch code {
        case .networkError:
            message = "You appear to be offline. Check your internet connection and try again."
        case .tooManyRequests:
            message = "Too many sign-in attempts. Please wait a moment and try again."
        case .userDisabled:
            message = "This account has been disabled."
        case .wrongPassword, .invalidCredential:
            message = "Your saved-selections account uses a different password. Please contact support to reset it."
        case .weakPassword:
            message = "That password is too weak. Use at least 6 characters."
        case .invalidEmail:
            message = "That email address isn't valid."
        case .operationNotAllowed:
            message = "Email sign-in isn't enabled for this app's Firebase project."
        default:
            message = "Couldn't connect your saved selections (\(nsError.localizedDescription))."
        }
    }
}
