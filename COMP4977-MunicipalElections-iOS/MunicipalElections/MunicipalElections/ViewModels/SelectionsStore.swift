import Foundation
import Combine

/// The signed-in user's Firestore candidate selections for the current city.
/// Shared by the Selections tab, Candidate Detail and Profile so all update at once.
/// Also enforces seat limits: no more selections per office than it has seats.
@MainActor
final class SelectionsStore: ObservableObject {
    /// Selections for one office, with its seat count when known.
    struct PositionGroup: Identifiable {
        let name: String
        let seats: Int?
        let selections: [CandidateSelection]
        var id: String { name.lowercased() }

        var chosenSummary: String {
            guard let seats else { return "\(selections.count) chosen" }
            return "\(selections.count) of \(seats) chosen"
        }
    }

    @Published private(set) var selections: [CandidateSelection] = []
    /// Offices for the current city (from the API), used for seat limits.
    @Published private(set) var positions: [Position] = []
    @Published private(set) var isLoading = false
    @Published private(set) var isSaving = false
    @Published var errorMessage: String?
    /// Non-blocking notice, e.g. offline cached data or unreadable records.
    @Published private(set) var dataWarning: String?
    /// Saved selections across every city, or `nil` if the count couldn't be loaded.
    @Published private(set) var allCitiesCount: Int?

    /// Set by the app so successful Firestore reads/writes update "Last synced".
    var syncStatus: SyncStatus?

    private let service: CandidateSelectionService
    private let api: APIClient
    private var email: String?
    private var municipality: Municipality?
    private var contextID = UUID()
    private var currentRequestID = UUID()
    private var mutationContextID: UUID?

    init(service: CandidateSelectionService = .shared, api: APIClient = .shared) {
        self.service = service
        self.api = api
    }

    /// Call whenever the user or city changes. Records from the previous
    /// user/city are cleared immediately and never shown.
    func load(email: String?, municipality: Municipality?) async {
        if email != self.email || municipality?.id != self.municipality?.id {
            contextID = UUID()
            currentRequestID = UUID()
            selections = []
            positions = []
            errorMessage = nil
            dataWarning = nil
            if email != self.email { allCitiesCount = nil }
        }
        self.email = email
        self.municipality = municipality
        if email == nil || municipality == nil { isLoading = false }
        await refresh()
    }

    func refresh() async {
        guard let email, let municipality else { return }
        guard !isSaving || mutationContextID != contextID else { return }
        let requestID = UUID()
        currentRequestID = requestID
        isLoading = true
        errorMessage = nil
        defer { if currentRequestID == requestID { isLoading = false } }

        async let positionsTask: Void = loadPositions(for: municipality, requestID: requestID)
        async let countTask: Void = loadAllCitiesCount(email: email, requestID: requestID)
        do {
            let result = try await service.fetchSelections(email: email, municipalityId: municipality.id)
            // Drop a response for a user/city that is no longer active.
            guard !Task.isCancelled, currentRequestID == requestID,
                  email == self.email, municipality.id == self.municipality?.id else { return }
            selections = result.selections
            if result.fromCache {
                dataWarning = "You appear to be offline. Showing your last saved selections."
            } else {
                syncStatus?.markSynced()
                dataWarning = result.unreadableCount == 0 ? nil
                    : "\(result.unreadableCount) saved record\(result.unreadableCount == 1 ? "" : "s") couldn't be read and \(result.unreadableCount == 1 ? "is" : "are") hidden."
            }
        } catch {
            if !Task.isCancelled && currentRequestID == requestID { errorMessage = error.localizedDescription }
        }
        _ = await (positionsTask, countTask)
    }

    func selection(for candidate: Candidate) -> CandidateSelection? {
        selections.first { $0.candidateId == candidate.id }
    }

    // MARK: Seat limits

    /// Seats for the candidate's office: from the candidate itself, else the city's offices.
    func seats(for candidate: Candidate) -> Int? {
        guard candidate.municipalityId == municipality?.id else { return nil }
        if let position = position(id: candidate.positionId, name: candidate.positionTitle),
           position.municipalityId == municipality?.id, position.seatsAvailable > 0 {
            return position.seatsAvailable
        }
        if let position = candidate.position, position.municipalityId == municipality?.id,
           position.id == (candidate.positionId ?? position.id), position.seatsAvailable > 0 {
            return position.seatsAvailable
        }
        return nil
    }

    /// How many of the user's selections are for this office.
    func chosenCount(positionId: Int?, positionName: String) -> Int {
        selections.filter { Self.matches($0, positionId: positionId, positionName: positionName) }.count
    }

    /// Why the candidate can't be added, or `nil` if a seat is still free.
    func seatLimitMessage(for candidate: Candidate) -> String? {
        guard selection(for: candidate) == nil else { return nil }
        guard candidate.municipalityId == municipality?.id else {
            return "Choose a candidate from your current municipality."
        }
        guard let seats = seats(for: candidate), seats > 0 else {
            return "Seat limits are unavailable. Refresh before adding a selection."
        }
        let positionId = candidate.positionId ?? candidate.position?.id
        let chosen = chosenCount(positionId: positionId, positionName: candidate.positionTitle)
        guard chosen >= seats else { return nil }
        return "You've already chosen \(chosen) of \(seats) \(candidate.positionTitle). "
            + "Remove one in My Candidate Selections to add \(candidate.name)."
    }

    /// Selections grouped by office, in ballot order, each with "x of N chosen".
    var positionGroups: [PositionGroup] {
        var remaining = selections
        var groups: [PositionGroup] = []
        for position in positions {
            let matching = remaining.filter { Self.matches($0, positionId: position.id, positionName: position.name) }
            guard !matching.isEmpty else { continue }
            remaining.removeAll { Self.matches($0, positionId: position.id, positionName: position.name) }
            groups.append(PositionGroup(name: position.name, seats: position.seatsAvailable, selections: matching))
        }
        // Offices not in the API list (or not loaded yet), grouped by saved name.
        let leftovers = Dictionary(grouping: remaining) { $0.positionName }
        for name in leftovers.keys.sorted() {
            groups.append(PositionGroup(name: name.isEmpty ? "Other" : name, seats: nil,
                                        selections: leftovers[name] ?? []))
        }
        return groups
    }

    // MARK: CRUD (throwing so the calling sheet/dialog can show the message)

    func add(_ candidate: Candidate, notes: String) async throws {
        guard !isSaving else { throw FirestoreServiceError(message: "Please wait for your current save to finish.") }
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
        let context = contextID
        isSaving = true
        mutationContextID = context
        currentRequestID = UUID()
        isLoading = false
        defer { isSaving = false; mutationContextID = nil }
        // Re-read the current API seat count; missing data must fail closed.
        let loaded = try await api.fetchPositions(for: municipality)
        guard context == contextID else { throw FirestoreServiceError(message: "Your city or account changed. Please try again.") }
        try Task.checkCancellation()
        positions = loaded
        guard let position = loaded.first(where: {
            $0.id == (candidate.positionId ?? candidate.position?.id)
                && $0.municipalityId == municipality.id && $0.seatsAvailable > 0
        }) else {
            throw FirestoreServiceError(message: "This candidate's position couldn't be verified. Refresh before saving.")
        }
        let created = try await service.create(for: candidate, municipality: municipality,
                                               position: position, email: email, notes: notes)
        guard context == contextID, email == self.email,
              municipality.id == self.municipality?.id else { return }
        syncStatus?.markSynced()
        selections.insert(created, at: 0)
        allCitiesCount = allCitiesCount.map { $0 + 1 }
    }

    func updateNotes(of selection: CandidateSelection, to notes: String) async throws {
        try validateCurrent(selection)
        guard !isSaving else { throw FirestoreServiceError(message: "Please wait for your current save to finish.") }
        let notes = try Self.validated(notes)
        let context = contextID
        isSaving = true
        mutationContextID = context
        currentRequestID = UUID()
        isLoading = false
        defer { isSaving = false; mutationContextID = nil }
        try await service.updateNotes(of: selection, notes: notes)
        guard context == contextID else { return }
        syncStatus?.markSynced()
        guard let index = selections.firstIndex(where: { $0.id == selection.id }) else { return }
        selections[index].notes = notes
        selections[index].dateModified = Date()
        let updated = selections.remove(at: index)
        selections.insert(updated, at: 0)
    }

    func delete(_ selection: CandidateSelection) async throws {
        try validateCurrent(selection)
        guard !isSaving else { throw FirestoreServiceError(message: "Please wait for your current save to finish.") }
        let context = contextID
        isSaving = true
        mutationContextID = context
        currentRequestID = UUID()
        isLoading = false
        defer { isSaving = false; mutationContextID = nil }
        try await service.delete(selection)
        guard context == contextID else { return }
        syncStatus?.markSynced()
        let before = selections.count
        selections.removeAll { $0.id == selection.id }
        if selections.count < before { allCitiesCount = allCitiesCount.map { max(0, $0 - 1) } }
    }

    static func validated(_ notes: String) throws -> String {
        let trimmed = notes.trimmingCharacters(in: .whitespacesAndNewlines)
        try CandidateSelectionService.validateNotes(trimmed)
        return trimmed
    }

    // MARK: Private

    private func validateCurrent(_ selection: CandidateSelection) throws {
        guard selection.userEmail == email?.lowercased(),
              selection.municipalityId == municipality?.id else {
            throw FirestoreServiceError(message: "This selection belongs to another city or account. Reopen your selections.")
        }
    }

    private func position(id: Int?, name: String) -> Position? {
        if let id, let match = positions.first(where: { $0.id == id }) { return match }
        return positions.first { $0.name.caseInsensitiveCompare(name) == .orderedSame }
    }

    /// Records saved before Phase 5 have no `positionId`, so fall back to the office name
    /// (names are unique within a city).
    private static func matches(_ selection: CandidateSelection, positionId: Int?, positionName: String) -> Bool {
        if let positionId, let saved = selection.positionId { return saved == positionId }
        return selection.positionName.caseInsensitiveCompare(positionName) == .orderedSame
    }

    /// Failures are ignored: seat counts then come from each candidate's own `position`.
    private func loadPositions(for municipality: Municipality, requestID: UUID) async {
        guard let loaded = try? await api.fetchPositions(for: municipality),
              !Task.isCancelled, currentRequestID == requestID,
              municipality.id == self.municipality?.id else { return }
        positions = loaded.filter { $0.municipalityId == municipality.id }
    }

    /// Optional extra for Profile; a failure just hides the all-cities total.
    private func loadAllCitiesCount(email: String, requestID: UUID) async {
        guard let count = try? await service.countAllSelections(email: email),
              !Task.isCancelled, currentRequestID == requestID, email == self.email else { return }
        allCitiesCount = count
    }
}
