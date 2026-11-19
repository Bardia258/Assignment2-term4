import Foundation
import Combine

@MainActor
final class CandidateDirectoryViewModel: ObservableObject {
    @Published private(set) var candidates: [Candidate] = []
    @Published private(set) var municipalities: [Municipality] = []
    @Published var selectedMunicipalityId: Int?
    @Published var searchText: String = ""
    @Published private(set) var isLoading = false
    @Published private(set) var errorMessage: String? = nil
    @Published private(set) var municipalityErrorMessage: String?

    private let api: APIClient
    private var currentRequestID = UUID()
    /// Which filter `candidates` was loaded for, so a filter change doesn't show stale rows.
    private var loadedMunicipalityId: Int??

    init(api: APIClient = .shared) {
        self.api = api
    }

    var selectedMunicipalityName: String {
        municipalities.first { $0.id == selectedMunicipalityId }?.name ?? "All Municipalities"
    }

    var filteredCandidates: [Candidate] {
        guard !searchText.isEmpty else { return candidates }
        return candidates.filter { $0.name.localizedCaseInsensitiveContains(searchText) }
    }

    /// Full-screen spinner only on first load; pull-to-refresh keeps the list on screen.
    var showsInitialLoading: Bool { isLoading && candidates.isEmpty }

    func loadMunicipalities() async {
        municipalityErrorMessage = nil
        do {
            let loaded = try await api.fetchMunicipalities()
            try Task.checkCancellation()
            municipalities = loaded
            if let selectedMunicipalityId, !loaded.contains(where: { $0.id == selectedMunicipalityId }) {
                self.selectedMunicipalityId = nil
            }
        } catch {
            if !Task.isCancelled { municipalityErrorMessage = error.localizedDescription }
        }
    }

    func fetchCandidates() async {
        let requestID = UUID()
        currentRequestID = requestID
        let municipalityId = selectedMunicipalityId
        if loadedMunicipalityId != .some(municipalityId) { candidates = [] }
        isLoading = true
        errorMessage = nil
        defer { if currentRequestID == requestID { isLoading = false } }
        do {
            let loaded: [Candidate]
            if let municipalityId {
                loaded = try await api.fetchCandidates(for: municipalityId)
            } else {
                loaded = try await api.fetchAllCandidates()
            }
            try Task.checkCancellation()
            guard currentRequestID == requestID, selectedMunicipalityId == municipalityId else { return }
            candidates = loaded
            loadedMunicipalityId = .some(municipalityId)
        } catch {
            if !Task.isCancelled && currentRequestID == requestID && selectedMunicipalityId == municipalityId {
                errorMessage = error.localizedDescription
            }
        }
    }

    func refresh() async {
        await loadMunicipalities()
        await fetchCandidates()
    }
}
