import Foundation
import FirebaseFirestore

/// Selections stay in Firestore; election information comes from the Azure API.
/// A profile revision serializes app writes across devices so a seat check
/// cannot race another app save.
final class CandidateSelectionService {
    static let shared = CandidateSelectionService()

    struct FetchResult {
        let selections: [CandidateSelection]
        let fromCache: Bool
        let unreadableCount: Int
    }

    private var collection: CollectionReference {
        FirebaseService.firestore.collection("CandidateSelections")
    }

    func fetchSelections(email: String, municipalityId: Int,
                         serverOnly: Bool = false) async throws -> FetchResult {
        try FirestoreRequest.requireIdentity(email: email)
        guard municipalityId > 0 else { throw FirestoreServiceError.invalidData }
        let query = collection
            .whereField("userEmail", isEqualTo: email.lowercased())
            .whereField("municipalityId", isEqualTo: municipalityId)
        let snapshot = try await FirestoreRequest.read {
            try await query.getDocuments(source: serverOnly ? .server : .default)
        }
        let mapped = snapshot.documents.compactMap(Self.selection(from:))
        guard mapped.allSatisfy({ $0.userEmail == email.lowercased()
            && $0.municipalityId == municipalityId }) else { throw FirestoreServiceError.invalidData }
        return FetchResult(selections: mapped.sorted { $0.dateModified > $1.dateModified },
                           fromCache: snapshot.metadata.isFromCache,
                           unreadableCount: snapshot.documents.count - mapped.count)
    }

    func countAllSelections(email: String) async throws -> Int {
        try FirestoreRequest.requireIdentity(email: email)
        let query = collection.whereField("userEmail", isEqualTo: email.lowercased()).count
        let snapshot = try await FirestoreRequest.read { try await query.getAggregation(source: .server) }
        return snapshot.count.intValue
    }

    func create(for candidate: Candidate, municipality: Municipality, position: Position,
                email: String, notes: String) async throws -> CandidateSelection {
        let userEmail = email.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
        try FirestoreRequest.requireIdentity(email: userEmail)
        try Self.validateCreate(candidate: candidate, municipality: municipality,
                                position: position, userEmail: userEmail, notes: notes)
        let id = CandidateSelection.documentID(userEmail: userEmail, candidateId: candidate.id)
        let photoUrl = candidate.photoUrl.flatMap { !$0.isEmpty && $0.count <= 500 ? $0 : nil }
        var data: [String: Any] = [
            "id": id, "userEmail": userEmail,
            "candidateId": candidate.id, "candidateName": candidate.name,
            "positionId": position.id, "positionName": position.name,
            "municipalityName": municipality.name, "municipalityId": municipality.id,
            "notes": notes,
            "dateCreated": FieldValue.serverTimestamp(),
            "dateModified": FieldValue.serverTimestamp()
        ]
        if let photoUrl { data["photoUrl"] = photoUrl }
        let payload = data
        let document = collection.document(id)
        let profile = FirebaseService.firestore.collection("users").document(userEmail)

        // Apple's SDK cannot query a collection inside a transaction.
        // Read the revision BEFORE the server query, then compare it inside
        // the transaction. Rules require every selection write to advance it.
        for _ in 0..<3 {
            try FirestoreRequest.requireIdentity(email: userEmail)
            try Task.checkCancellation()
            let before = try await FirestoreRequest.read { try await profile.getDocument(source: .server) }
            let expectedRevision = try Self.revision(from: before)
            let latest = try await fetchSelections(email: userEmail,
                municipalityId: municipality.id, serverOnly: true)
            guard latest.unreadableCount == 0 else {
                throw FirestoreServiceError(message: "Some saved selections couldn't be checked. Refresh your selections or contact support before adding more.")
            }
            guard !latest.selections.contains(where: { $0.candidateId == candidate.id }) else {
                throw FirestoreServiceError(message: "\(candidate.name) is already in your selections. Pull to refresh.")
            }
            let chosen = latest.selections.filter {
                if let savedPosition = $0.positionId { return savedPosition == position.id }
                return $0.positionName.caseInsensitiveCompare(position.name) == .orderedSame
            }.count
            guard chosen < position.seatsAvailable else {
                throw FirestoreServiceError(message: "You've already chosen \(chosen) of \(position.seatsAvailable) \(position.name). Remove a selection before adding another.")
            }
            let committed = try await FirestoreRequest.transaction(email: userEmail) { transaction -> Any? in
                let current = try transaction.getDocument(profile)
                guard try Self.revision(from: current) == expectedRevision else { return false }
                guard (current.data()?["municipalityId"] as? NSNumber)?.intValue == municipality.id else {
                    throw FirestoreServiceError(message: "Your municipality changed on another device. Log out and sign in again to refresh it.")
                }
                transaction.setData(payload, forDocument: document)
                transaction.updateData(["selectionsRevision": expectedRevision + 1,
                    "updatedAt": FieldValue.serverTimestamp()], forDocument: profile)
                return true
            }
            if committed as? Bool == true {
                let now = Date()
                return CandidateSelection(id: id, userEmail: userEmail, candidateId: candidate.id,
                    candidateName: candidate.name, positionName: position.name, positionId: position.id,
                    municipalityName: municipality.name, municipalityId: municipality.id,
                    photoUrl: photoUrl, notes: notes, dateCreated: now, dateModified: now)
            }
        }
        throw FirestoreServiceError(message: "Your selections changed on another device. Refresh and try again.")
    }

    func updateNotes(of selection: CandidateSelection, notes: String) async throws {
        try Self.validateNotes(notes)
        try await mutate(selection, notes: notes)
    }

    func delete(_ selection: CandidateSelection) async throws {
        try await mutate(selection, notes: nil)
    }

    private func mutate(_ selection: CandidateSelection, notes: String?) async throws {
        try FirestoreRequest.requireIdentity(email: selection.userEmail)
        guard !selection.id.isEmpty, selection.municipalityId > 0,
              selection.id == CandidateSelection.documentID(userEmail: selection.userEmail,
                                                           candidateId: selection.candidateId) else {
            throw FirestoreServiceError.invalidData
        }
        let document = collection.document(selection.id)
        let profile = FirebaseService.firestore.collection("users").document(selection.userEmail)
        _ = try await FirestoreRequest.transaction(email: selection.userEmail) { transaction in
            let savedProfile = try transaction.getDocument(profile)
            let saved = try transaction.getDocument(document)
            if !saved.exists {
                if notes == nil { return nil }
                throw FirestoreServiceError(message: "That selection no longer exists. Pull to refresh.")
            }
            guard let data = saved.data(), data["userEmail"] as? String == selection.userEmail,
                  (data["municipalityId"] as? NSNumber)?.intValue == selection.municipalityId,
                  (data["candidateId"] as? NSNumber)?.intValue == selection.candidateId else {
                throw FirestoreServiceError.invalidData
            }
            let revision = try Self.revision(from: savedProfile)
            guard (savedProfile.data()?["municipalityId"] as? NSNumber)?.intValue == selection.municipalityId else {
                throw FirestoreServiceError(message: "Your municipality changed on another device. Log out and sign in again to refresh it.")
            }
            if let notes {
                transaction.updateData(["notes": notes, "dateModified": FieldValue.serverTimestamp()],
                                       forDocument: document)
            } else {
                transaction.deleteDocument(document)
            }
            transaction.updateData(["selectionsRevision": revision + 1,
                "updatedAt": FieldValue.serverTimestamp()], forDocument: profile)
            return nil
        }
    }

    private static func revision(from snapshot: DocumentSnapshot) throws -> Int {
        guard snapshot.exists, let data = snapshot.data() else {
            throw FirestoreServiceError(message: "Choose your municipality again before saving selections.")
        }
        guard let stored = data["selectionsRevision"] else { return 0 }
        guard let value = stored as? NSNumber, value.int64Value >= 0,
              value.int64Value < Int64.max, value.doubleValue == Double(value.int64Value) else {
            throw FirestoreServiceError.invalidData
        }
        return value.intValue
    }

    static func validateNotes(_ notes: String) throws {
        guard notes.count <= CandidateSelection.maxNotesLength else {
            throw FirestoreServiceError(message: "Notes can be at most \(CandidateSelection.maxNotesLength) characters.")
        }
    }

    private static func validateCreate(candidate: Candidate, municipality: Municipality,
                                       position: Position, userEmail: String, notes: String) throws {
        guard !userEmail.isEmpty else { throw FirestoreServiceError(message: AuthSession.expiredMessage) }
        guard candidate.id > 0,
              (1...200).contains(candidate.name.trimmingCharacters(in: .whitespacesAndNewlines).count),
              (1...100).contains(position.name.trimmingCharacters(in: .whitespacesAndNewlines).count),
              municipality.id > 0,
              (1...100).contains(municipality.name.trimmingCharacters(in: .whitespacesAndNewlines).count),
              candidate.municipalityId == municipality.id,
              position.id > 0, position.municipalityId == municipality.id,
              position.seatsAvailable > 0,
              (candidate.positionId ?? candidate.position?.id) == position.id,
              candidate.position == nil || candidate.position?.municipalityId == municipality.id else {
            throw FirestoreServiceError(message: "This candidate's municipality or position couldn't be verified. Refresh and try again.")
        }
        try validateNotes(notes)
    }

    private static func selection(from document: QueryDocumentSnapshot) -> CandidateSelection? {
        let data = document.data(with: .estimate)
        guard let userEmail = data["userEmail"] as? String, !userEmail.isEmpty,
              let candidateId = (data["candidateId"] as? NSNumber)?.intValue, candidateId > 0,
              document.documentID == CandidateSelection.documentID(userEmail: userEmail, candidateId: candidateId),
              data["id"] as? String == document.documentID,
              let candidateName = data["candidateName"] as? String, !candidateName.isEmpty,
              let positionName = data["positionName"] as? String, !positionName.isEmpty,
              let municipalityName = data["municipalityName"] as? String, !municipalityName.isEmpty,
              let municipalityId = (data["municipalityId"] as? NSNumber)?.intValue, municipalityId > 0,
              let notes = data["notes"] as? String, notes.count <= CandidateSelection.maxNotesLength,
              let created = (data["dateCreated"] as? Timestamp)?.dateValue(),
              let modified = (data["dateModified"] as? Timestamp)?.dateValue() else { return nil }
        return CandidateSelection(id: document.documentID, userEmail: userEmail,
            candidateId: candidateId, candidateName: candidateName, positionName: positionName,
            positionId: (data["positionId"] as? NSNumber)?.intValue,
            municipalityName: municipalityName, municipalityId: municipalityId,
            photoUrl: data["photoUrl"] as? String, notes: notes,
            dateCreated: created, dateModified: modified)
    }
}
