import Foundation
import FirebaseFirestore

/// CRUD for Firestore `CandidateSelections`. Each document ID is
/// "{userEmail}_{candidateId}", so a duplicate is impossible at the database
/// level: a second create is rejected by the security rules.
final class CandidateSelectionService {
    static let shared = CandidateSelectionService()

    private var collection: CollectionReference {
        FirebaseService.firestore.collection("CandidateSelections")
    }

    // MARK: Read

    /// Only the signed-in user's selections for one municipality.
    func fetchSelections(email: String, municipalityId: Int) async throws -> [CandidateSelection] {
        do {
            let snapshot = try await collection
                .whereField("userEmail", isEqualTo: email.lowercased())
                .whereField("municipalityId", isEqualTo: municipalityId)
                .getDocuments()
            // Sorted on the device, so no composite Firestore index is needed.
            return snapshot.documents
                .compactMap(Self.selection(from:))
                .sorted { $0.dateModified > $1.dateModified }
        } catch {
            throw FirestoreServiceError(error)
        }
    }

    // MARK: Create

    func create(for candidate: Candidate, municipality: Municipality, email: String,
                notes: String) async throws -> CandidateSelection {
        let userEmail = email.lowercased()
        let id = CandidateSelection.documentID(userEmail: userEmail, candidateId: candidate.id)
        var data: [String: Any] = [
            "id": id,
            "userEmail": userEmail,
            "candidateId": candidate.id,
            "candidateName": candidate.name,
            "positionName": candidate.positionTitle,
            "municipalityName": municipality.name,
            "municipalityId": municipality.id,
            "notes": notes,
            "dateCreated": FieldValue.serverTimestamp(),
            "dateModified": FieldValue.serverTimestamp()
        ]
        if let photoUrl = candidate.photoUrl, !photoUrl.isEmpty { data["photoUrl"] = photoUrl }

        do {
            try await collection.document(id).setData(data)
        } catch let error where FirestoreServiceError.isPermissionDenied(error) {
            // The rules only allow `create` when no document exists yet,
            // so a denial here almost always means it's already saved.
            throw FirestoreServiceError(message: "\(candidate.name) is already in your selections. Pull to refresh to see it.")
        } catch {
            throw FirestoreServiceError(error)
        }

        let now = Date()
        return CandidateSelection(id: id, userEmail: userEmail, candidateId: candidate.id,
                                  candidateName: candidate.name, positionName: candidate.positionTitle,
                                  municipalityName: municipality.name, municipalityId: municipality.id,
                                  photoUrl: data["photoUrl"] as? String, notes: notes,
                                  dateCreated: now, dateModified: now)
    }

    // MARK: Update

    func updateNotes(selectionId: String, notes: String) async throws {
        do {
            try await collection.document(selectionId).updateData([
                "notes": notes,
                "dateModified": FieldValue.serverTimestamp()
            ])
        } catch {
            throw FirestoreServiceError(error)
        }
    }

    // MARK: Delete

    func delete(selectionId: String) async throws {
        do {
            try await collection.document(selectionId).delete()
        } catch {
            throw FirestoreServiceError(error)
        }
    }

    // MARK: Mapping

    private static func selection(from document: QueryDocumentSnapshot) -> CandidateSelection? {
        // `.estimate` fills in server timestamps that are still pending locally.
        let data = document.data(with: .estimate)
        guard let userEmail = data["userEmail"] as? String,
              let candidateId = (data["candidateId"] as? NSNumber)?.intValue,
              let candidateName = data["candidateName"] as? String,
              let municipalityId = (data["municipalityId"] as? NSNumber)?.intValue else { return nil }
        let created = (data["dateCreated"] as? Timestamp)?.dateValue() ?? Date()
        return CandidateSelection(
            id: document.documentID,
            userEmail: userEmail,
            candidateId: candidateId,
            candidateName: candidateName,
            positionName: data["positionName"] as? String ?? "",
            municipalityName: data["municipalityName"] as? String ?? "",
            municipalityId: municipalityId,
            photoUrl: data["photoUrl"] as? String,
            notes: data["notes"] as? String ?? "",
            dateCreated: created,
            dateModified: (data["dateModified"] as? Timestamp)?.dateValue() ?? created)
    }
}
