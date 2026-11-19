import Foundation
import Combine

/// The signed-in user's Firestore candidate selections for the current city.
/// Shared by the Selections tab and Candidate Detail so both update at once.
@MainActor
final class SelectionsStore: ObservableObject {
    @Published private(set) var selections: [CandidateSelection] = []
    @Published private(set) var isLoading = false
    @Published var errorMessage: String?

    private let service: CandidateSelectionService
    private var email: String?
    private var municipality: Municipality?

    init(service: CandidateSelectionService = .shared) {
        self.service = service
    }

    /// Call whenever the user or city changes. Records from the previous
    /// user/city are cleared immediately and never shown.
    func load(email: String?, municipality: Municipality?) async {
        if email != self.email || municipality?.id != self.municipality?.id {
            selections = []
            errorMessage = nil
        }
        self.email = email
        self.municipality = municipality
        await refresh()
    }

    func refresh() async {
        guard let email, let municipality else { return }
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            let loaded = try await service.fetchSelections(email: email, municipalityId: municipality.id)
            // Drop a response for a user/city that is no longer active.
            guard email == self.email, municipality.id == self.municipality?.id else { return }
            selections = loaded
        } catch {
            if !Task.isCancelled { errorMessage = error.localizedDescription }
        }
    }

    func selection(for candidate: Candidate) -> CandidateSelection? {
        selections.first { $0.candidateId == candidate.id }
    }

    // MARK: CRUD (throwing so the calling sheet/dialog can show the message)

    func add(_ candidate: Candidate, notes: String) async throws {
        guard let email, let municipality else {
            throw FirestoreServiceError(message: "Choose your municipality before saving selections.")
        }
        let notes = try Self.validated(notes)
        guard selection(for: candidate) == nil else {
            throw FirestoreServiceError(message: "\(candidate.name) is already in your selections.")
        }
        guard candidate.municipalityId == municipality.id else {
            throw FirestoreServiceError(message: "You can only save candidates from \(municipality.name).")
        }
        let created = try await service.create(for: candidate, municipality: municipality,
                                               email: email, notes: notes)
        guard email == self.email, municipality.id == self.municipality?.id else { return }
        selections.insert(created, at: 0)
    }

    func updateNotes(of selection: CandidateSelection, to notes: String) async throws {
        let notes = try Self.validated(notes)
        try await service.updateNotes(selectionId: selection.id, notes: notes)
        guard let index = selections.firstIndex(where: { $0.id == selection.id }) else { return }
        selections[index].notes = notes
        selections[index].dateModified = Date()
        let updated = selections.remove(at: index)
        selections.insert(updated, at: 0)
    }

    func delete(_ selection: CandidateSelection) async throws {
        try await service.delete(selectionId: selection.id)
        selections.removeAll { $0.id == selection.id }
    }

    static func validated(_ notes: String) throws -> String {
        let trimmed = notes.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.count <= CandidateSelection.maxNotesLength else {
            throw FirestoreServiceError(message: "Notes can be at most \(CandidateSelection.maxNotesLength) characters.")
        }
        return trimmed
    }
}
