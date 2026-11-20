import Foundation
import FirebaseFirestore

/// CRUD for Firestore `CandidateSelections`. Each document ID is
/// "{userEmail}_{candidateId}", so a duplicate is impossible at the database
/// level: a second create is rejected by the security rules.
///
/// Every write is validated here first (same limits as `firestore.rules`), so
/// bad data is caught with a clear message instead of a permission error.
final class CandidateSelectionService {
    static let shared = CandidateSelectionService()

    /// Result of a load. `fromCache` is true when Firestore answered from its
    /// local cache because the server couldn't be reached.
    struct FetchResult {
        let selections: [CandidateSelection]
        let fromCache: Bool
        /// Documents that were missing required fields and couldn't be shown.
        let unreadableCount: Int
    }

    private var collection: CollectionReference {
        FirebaseService.firestore.collection("CandidateSelections")
    }

    // MARK: Read

    /// Only the signed-in user's selections for one municipality.
    func fetchSelections(email: String, municipalityId: Int) async throws -> FetchResult {
        let query = collection
            .whereField("userEmail", isEqualTo: email.lowercased())
            .whereField("municipalityId", isEqualTo: municipalityId)
        let snapshot = try await FirestoreRequest.read { try await query.getDocuments() }
        let mapped = snapshot.documents.compactMap(Self.selection(from:))
        // Sorted on the device, so no composite Firestore index is needed.
        return FetchResult(selections: mapped.sorted { $0.dateModified > $1.dateModified },
                           fromCache: snapshot.metadata.isFromCache,
                           unreadableCount: snapshot.documents.count - mapped.count)
    }

    /// How many selections the user has across every city (server count, no documents downloaded).
    func countAllSelections(email: String) async throws -> Int {
        let query = collection.whereField("userEmail", isEqualTo: email.lowercased()).count
        let snapshot = try await FirestoreRequest.read { try await query.getAggregation(source: .server) }
        return snapshot.count.intValue
    }

    // MARK: Create

    func create(for candidate: Candidate, municipality: Municipality, email: String,
                notes: String) async throws -> CandidateSelection {
        let userEmail = email.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
        try Self.validateCreate(candidate: candidate, municipality: municipality,
                                userEmail: userEmail, notes: notes)

        let id = CandidateSelection.documentID(userEmail: userEmail, candidateId: candidate.id)
        let positionId = candidate.positionId ?? candidate.position?.id
        // Rules cap photoUrl at 500 characters; a longer one is simply not stored.
        let photoUrl = candidate.photoUrl.flatMap { !$0.isEmpty && $0.count <= 500 ? $0 : nil }
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
        if let positionId { data["positionId"] = positionId }
        if let photoUrl { data["photoUrl"] = photoUrl }

        let document = collection.document(id)
        let payload = data
        do {
            try await FirestoreRequest.write { try await document.setData(payload) }
        } catch let error where FirestoreServiceError.isPermissionDenied(error) {
            // The rules only allow `create` when no document exists yet,
            // so a denial here almost always means it's already saved.
            throw FirestoreServiceError(message: "\(candidate.name) is already in your selections. Pull to refresh to see it.")
        }

        let now = Date()
        return CandidateSelection(id: id, userEmail: userEmail, candidateId: candidate.id,
                                  candidateName: candidate.name, positionName: candidate.positionTitle,
                                  positionId: positionId,
                                  municipalityName: municipality.name, municipalityId: municipality.id,
                                  photoUrl: photoUrl, notes: notes,
                                  dateCreated: now, dateModified: now)
    }

    // MARK: Update

    func updateNotes(selectionId: String, notes: String) async throws {
        guard !selectionId.isEmpty else { throw FirestoreServiceError.invalidData }
        try Self.validateNotes(notes)
        let document = collection.document(selectionId)
        try await FirestoreRequest.write {
            try await document.updateData([
                "notes": notes,
                "dateModified": FieldValue.serverTimestamp()
            ])
        }
    }

    // MARK: Delete

    func delete(selectionId: String) async throws {
        guard !selectionId.isEmpty else { throw FirestoreServiceError.invalidData }
        let document = collection.document(selectionId)
        try await FirestoreRequest.write { try await document.delete() }
    }

    // MARK: Validation (mirrors firestore.rules)

    static func validateNotes(_ notes: String) throws {
        guard notes.count <= CandidateSelection.maxNotesLength else {
            throw FirestoreServiceError(message: "Notes can be at most \(CandidateSelection.maxNotesLength) characters.")
        }
    }

    private static func validateCreate(candidate: Candidate, municipality: Municipality,
                                       userEmail: String, notes: String) throws {
        func invalid(_ field: String) -> FirestoreServiceError {
            FirestoreServiceError(message: "This selection can't be saved because the \(field) is missing or too long. Pull to refresh and try again.")
        }
        guard !userEmail.isEmpty else { throw FirestoreServiceError(message: AuthSession.expiredMessage) }
        guard candidate.id > 0 else { throw invalid("candidate") }
        guard (1...200).contains(candidate.name.trimmingCharacters(in: .whitespaces).count) else { throw invalid("candidate name") }
        guard (1...100).contains(candidate.positionTitle.trimmingCharacters(in: .whitespaces).count) else { throw invalid("position") }
        guard municipality.id > 0,
              (1...100).contains(municipality.name.trimmingCharacters(in: .whitespaces).count) else { throw invalid("municipality") }
        try validateNotes(notes)
    }

    // MARK: Mapping

    private static func selection(from document: QueryDocumentSnapshot) -> CandidateSelection? {
        // `.estimate` fills in server timestamps that are still pending locally.
        let data = document.data(with: .estimate)
        guard let userEmail = data["userEmail"] as? String,
              let candidateId = (data["candidateId"] as? NSNumber)?.intValue,
              let candidateName = data["candidateName"] as? String,
              let municipalityId = (data["municipalityId"] as? NSNumber)?.intValue else {
            #if DEBUG
            print("CandidateSelectionService: skipped unreadable document \(document.documentID)")
            #endif
            return nil
        }
        let created = (data["dateCreated"] as? Timestamp)?.dateValue() ?? Date()
        return CandidateSelection(
            id: document.documentID,
            userEmail: userEmail,
            candidateId: candidateId,
            candidateName: candidateName,
            positionName: data["positionName"] as? String ?? "",
            positionId: (data["positionId"] as? NSNumber)?.intValue,
            municipalityName: data["municipalityName"] as? String ?? "",
            municipalityId: municipalityId,
            photoUrl: data["photoUrl"] as? String,
            notes: data["notes"] as? String ?? "",
            dateCreated: created,
            dateModified: (data["dateModified"] as? Timestamp)?.dateValue() ?? created)
    }
}
