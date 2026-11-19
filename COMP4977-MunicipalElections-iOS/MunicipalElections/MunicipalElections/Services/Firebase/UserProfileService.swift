import Foundation
import FirebaseFirestore

/// Reads and writes `users/{email}` in Firestore. The document ID is the
/// lowercased email so the security rules can match it against
/// `request.auth.token.email`.
final class UserProfileService {
    static let shared = UserProfileService()

    private var users: CollectionReference { FirebaseService.firestore.collection("users") }

    /// Returns `nil` when the user has never picked a municipality.
    func fetchProfile(email: String) async throws -> UserProfile? {
        let key = email.lowercased()
        do {
            let snapshot = try await users.document(key).getDocument()
            guard let data = snapshot.data(),
                  let municipalityId = (data["municipalityId"] as? NSNumber)?.intValue,
                  let municipalityName = data["municipalityName"] as? String else { return nil }
            return UserProfile(email: key, municipalityId: municipalityId, municipalityName: municipalityName,
                               updatedAt: (data["updatedAt"] as? Timestamp)?.dateValue())
        } catch {
            throw FirestoreServiceError(error)
        }
    }

    func saveMunicipality(_ municipality: Municipality, email: String) async throws {
        let key = email.lowercased()
        do {
            try await users.document(key).setData([
                "email": key,
                "municipalityId": municipality.id,
                "municipalityName": municipality.name,
                "updatedAt": FieldValue.serverTimestamp()
            ])
        } catch {
            throw FirestoreServiceError(error)
        }
    }
}

/// Turns Firestore errors into messages a user can act on.
struct FirestoreServiceError: LocalizedError {
    let message: String
    var errorDescription: String? { message }

    init(_ error: Error) {
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
