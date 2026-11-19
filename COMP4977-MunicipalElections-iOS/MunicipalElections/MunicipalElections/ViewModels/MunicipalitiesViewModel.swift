import Foundation
import Combine

/// View models for the Municipalities tab. A1 had these views call
/// `APIClient` directly from `@State`; they now follow MVVM like the rest of the app.

@MainActor
final class MunicipalityListViewModel: ObservableObject {
    @Published private(set) var municipalities: [Municipality] = []
    @Published private(set) var isLoading = false
    @Published private(set) var errorMessage: String?

    private let api: APIClient

    init(api: APIClient = .shared) {
        self.api = api
    }

    func load() async {
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            municipalities = try await api.fetchMunicipalities()
        } catch {
            if !Task.isCancelled { errorMessage = error.localizedDescription }
        }
    }
}

@MainActor
final class MunicipalityDetailViewModel: ObservableObject {
    let municipality: Municipality
    @Published private(set) var candidates: [Candidate] = []
    @Published private(set) var isLoading = false
    @Published private(set) var errorMessage: String?

    private let api: APIClient

    init(municipality: Municipality, api: APIClient = .shared) {
        self.municipality = municipality
        self.api = api
    }

    func loadCandidates() async {
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            candidates = try await api.fetchCandidates(for: municipality.id)
        } catch {
            if !Task.isCancelled { errorMessage = error.localizedDescription }
        }
    }
}
