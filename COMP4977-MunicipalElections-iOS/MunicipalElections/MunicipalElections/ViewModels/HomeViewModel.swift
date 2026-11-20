import Foundation
import Combine

/// Offices (positions) and candidates for the user's city, shown on Home.
@MainActor
final class HomeViewModel: ObservableObject {
    struct Office: Identifiable {
        let position: Position
        let candidates: [Candidate]
        var id: Int { position.id }
    }

    @Published private(set) var offices: [Office] = []
    @Published private(set) var isLoading = false
    @Published private(set) var errorMessage: String?

    private let api: APIClient
    private var loadedMunicipalityId: Int?
    private var currentRequestID = UUID()

    init(api: APIClient = .shared) {
        self.api = api
    }

    func load(for municipality: Municipality?) async {
        let requestID = UUID()
        currentRequestID = requestID
        guard let municipality else {
            offices = []
            loadedMunicipalityId = nil
            isLoading = false
            errorMessage = nil
            return
        }
        // Never show the previous city's offices while the new city loads.
        if loadedMunicipalityId != municipality.id { offices = [] }
        isLoading = true
        errorMessage = nil
        defer { if currentRequestID == requestID { isLoading = false } }
        do {
            async let positions = api.fetchPositions(for: municipality)
            async let candidates = api.fetchCandidates(for: municipality)
            let (loadedPositions, loadedCandidates) = try await (positions, candidates)
            try Task.checkCancellation()
            guard currentRequestID == requestID else { return }
            offices = loadedPositions
                .filter { $0.municipalityId == municipality.id }
                .map { position in
                    Office(position: position,
                           candidates: loadedCandidates.filter {
                               $0.municipalityId == municipality.id && $0.positionId == position.id
                           })
                }
            loadedMunicipalityId = municipality.id
        } catch {
            if !Task.isCancelled && currentRequestID == requestID { errorMessage = error.localizedDescription }
        }
    }
}
