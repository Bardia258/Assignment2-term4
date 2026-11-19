import Foundation
import Combine

/// Candidates for the user's selected city, with name search.
@MainActor
final class CandidateDirectoryViewModel: ObservableObject {
    @Published private(set) var candidates: [Candidate] = []
    @Published var searchText: String = ""
    @Published private(set) var isLoading = false
    @Published private(set) var errorMessage: String? = nil

    private let api: APIClient
    private var currentRequestID = UUID()
    private var municipality: Municipality?

    init(api: APIClient = .shared) {
        self.api = api
    }

    var filteredCandidates: [Candidate] {
        guard !searchText.isEmpty else { return candidates }
        return candidates.filter { $0.name.localizedCaseInsensitiveContains(searchText) }
    }

    /// Full-screen spinner only on first load; pull-to-refresh keeps the list on screen.
    var showsInitialLoading: Bool { isLoading && candidates.isEmpty }

    /// Called when the user's city changes. Old-city rows are cleared immediately.
    func load(for municipality: Municipality?) async {
        if municipality?.id != self.municipality?.id {
            candidates = []
            searchText = ""
        }
        self.municipality = municipality
        await fetchCandidates()
    }

    func fetchCandidates() async {
        guard let municipality else { return }
        let requestID = UUID()
        currentRequestID = requestID
        isLoading = true
        errorMessage = nil
        defer { if currentRequestID == requestID { isLoading = false } }
        do {
            let loaded = try await api.fetchCandidates(for: municipality)
            try Task.checkCancellation()
            // Discard a response that arrives after the user switched city.
            guard currentRequestID == requestID, self.municipality?.id == municipality.id else { return }
            candidates = loaded.filter { $0.municipalityId == municipality.id }
        } catch {
            if !Task.isCancelled && currentRequestID == requestID {
                errorMessage = error.localizedDescription
            }
        }
    }
}
