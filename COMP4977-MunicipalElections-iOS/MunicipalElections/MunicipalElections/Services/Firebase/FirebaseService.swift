import Foundation
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
struct FirestoreServiceError: LocalizedError, CustomNSError {
    let message: String
    var errorDescription: String? { message }

    init(message: String) { self.message = message }

    static var errorDomain: String { "BCIT.MunicipalElections.Firestore" }
    var errorCode: Int { 1 }
    var errorUserInfo: [String: Any] { [NSLocalizedDescriptionKey: message] }

    static let offline = FirestoreServiceError(message: "You appear to be offline, try again.")

    static let invalidData = FirestoreServiceError(
        message: "Some saved data couldn't be read. Refresh and try again. If this continues, contact support.")

    init(_ error: Error) {
        if let already = error as? FirestoreServiceError {
            message = already.message
            return
        }
        let nsError = error as NSError
        if nsError.domain == Self.errorDomain {
            message = nsError.localizedDescription
            return
        }
        if nsError.domain == NSURLErrorDomain {
            message = Self.offline.message
            return
        }
        guard nsError.domain == FirestoreErrorDomain,
              let code = FirestoreErrorCode.Code(rawValue: nsError.code) else {
            message = "Something went wrong saving your data. Please try again."
            return
        }
        switch code {
        case .permissionDenied:
            message = "You don't have permission to access this data. Try logging out and back in."
        case .unauthenticated:
            message = AuthSession.expiredMessage
        case .unavailable, .deadlineExceeded:
            message = Self.offline.message
        case .notFound:
            message = "That record no longer exists. Pull to refresh."
        case .alreadyExists:
            message = "That record already exists. Pull to refresh."
        case .dataLoss:
            message = Self.invalidData.message
        case .resourceExhausted:
            message = "Too many requests right now. Please wait a moment and try again."
        case .invalidArgument, .failedPrecondition:
            message = "Your data couldn't be saved because it was invalid."
        default:
            message = "Something went wrong saving your data. Please try again."
        }
    }
}
