import FirebaseCore
import FirebaseAuth
import FirebaseFirestore

/// Single entry point for Firebase. `configure()` runs once at launch from
/// the app delegate; the accessors also configure on demand so no caller can
/// touch Firebase before it is ready (e.g. a @StateObject built during launch).
enum FirebaseService {
    static func configure() {
        // Reads GoogleService-Info.plist from the app bundle.
        guard FirebaseApp.app() == nil else { return }
        FirebaseApp.configure()
    }

    static var auth: Auth {
        configure()
        return Auth.auth()
    }

    static var firestore: Firestore {
        configure()
        return Firestore.firestore()
    }
}

/// Turns Firestore errors into messages a user can act on.
struct FirestoreServiceError: LocalizedError {
    let message: String
    var errorDescription: String? { message }

    init(message: String) { self.message = message }

    static func isPermissionDenied(_ error: Error) -> Bool {
        let nsError = error as NSError
        return nsError.domain == FirestoreErrorDomain
            && nsError.code == FirestoreErrorCode.Code.permissionDenied.rawValue
    }

    init(_ error: Error) {
        if let already = error as? FirestoreServiceError {
            message = already.message
            return
        }
        let nsError = error as NSError
        guard nsError.domain == FirestoreErrorDomain,
              let code = FirestoreErrorCode.Code(rawValue: nsError.code) else {
            message = "Something went wrong saving your data. Please try again."
            return
        }
        switch code {
        case .permissionDenied:
            message = "You don't have permission to access this data. Try logging out and back in."
        case .unauthenticated:
            message = "Your session has ended. Please log in again."
        case .unavailable, .deadlineExceeded:
            message = "Couldn't reach the server. Check your internet connection and try again."
        case .resourceExhausted:
            message = "Too many requests right now. Please wait a moment and try again."
        case .invalidArgument, .failedPrecondition:
            message = "Your data couldn't be saved because it was invalid."
        default:
            message = "Something went wrong saving your data (\(nsError.localizedDescription))."
        }
    }
}
