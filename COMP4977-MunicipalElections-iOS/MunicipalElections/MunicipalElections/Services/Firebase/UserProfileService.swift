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
