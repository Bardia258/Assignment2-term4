import Foundation
import FirebaseFirestore

/// Reads and writes `users/{email}` in Firestore. The document ID is the
/// lowercased email so the security rules can match it against
/// `request.auth.token.email`.
final class UserProfileService {
    static let shared = UserProfileService()

    private var users: CollectionReference { FirebaseService.firestore.collection("users") }

    /// `profile` is `nil` when the user has never picked a municipality.
    /// `fromCache` is true when Firestore answered offline from its local cache.
    func fetchProfile(email: String) async throws -> (profile: UserProfile?, fromCache: Bool) {
        let key = email.lowercased()
        try FirestoreRequest.requireIdentity(email: key)
        let document = users.document(key)
        do {
            let snapshot = try await FirestoreRequest.read { try await document.getDocument() }
            let fromCache = snapshot.metadata.isFromCache
            guard snapshot.exists else { return (nil, fromCache) }
            guard let data = snapshot.data(),
                  data["email"] as? String == key,
                  let municipalityId = (data["municipalityId"] as? NSNumber)?.intValue,
                  municipalityId > 0,
                  let municipalityName = data["municipalityName"] as? String,
                  !municipalityName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
                throw FirestoreServiceError.invalidData
            }
            let profile = UserProfile(email: key, municipalityId: municipalityId, municipalityName: municipalityName,
                                      updatedAt: (data["updatedAt"] as? Timestamp)?.dateValue())
            return (profile, fromCache)
        } catch {
            throw FirestoreServiceError(error)
        }
    }

    func saveMunicipality(_ municipality: Municipality, email: String) async throws {
        let key = email.lowercased()
        try FirestoreRequest.requireIdentity(email: key)
        // Same limits as firestore.rules, checked before the write.
        let name = municipality.name.trimmingCharacters(in: .whitespaces)
        guard !key.isEmpty else { throw FirestoreServiceError(message: AuthSession.expiredMessage) }
        guard municipality.id > 0, (1...100).contains(name.count) else {
            throw FirestoreServiceError(message: "That municipality can't be saved. Pull to refresh and try again.")
        }
        let document = users.document(key)
        do {
            _ = try await FirestoreRequest.transaction(email: key) { transaction in
                // Read first so concurrent selection/profile changes retry together.
                _ = try transaction.getDocument(document)
                transaction.setData([
                    "email": key,
                    "municipalityId": municipality.id,
                    "municipalityName": municipality.name,
                    "updatedAt": FieldValue.serverTimestamp()
                ], forDocument: document, merge: true)
                return nil
            }
        } catch {
            throw FirestoreServiceError(error)
        }
    }
}
