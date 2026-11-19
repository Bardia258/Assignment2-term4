import Foundation
import Combine

/// A1 voting guide backed by `/api/savedcandidates` (JWT-authorized).
/// Replaced by Firestore CandidateSelections in Phase 4.
@MainActor
final class VotingGuideManager: ObservableObject {
    @Published private var savedCandidates: [SavedCandidate] = []
    @Published private(set) var isLoading = false
    @Published private(set) var errorMessage: String?

    private let api: APIClient
    /// Identifies whose guide is loaded. Results that arrive after a logout or
    /// account switch are discarded instead of leaking into the next session.
    private var activeUser: String?

    init(api: APIClient = .shared) {
        self.api = api
    }

    var selectedCandidates: [Candidate] { savedCandidates.map(\.candidate) }

    /// Call whenever the signed-in user changes (`nil` = logged out).
    func load(for user: String?) async {
        if activeUser != user {
            savedCandidates = []
            activeUser = user
            isLoading = false
        }
        errorMessage = nil
        guard user != nil, !isLoading else { return }
        isLoading = true
        defer { if activeUser == user { isLoading = false } }
        do {
            let saved = try await api.fetchSavedCandidates()
            guard activeUser == user else { return }
            savedCandidates = saved
        } catch {
            if !Task.isCancelled { handle(error, user: user) }
        }
    }

    func reload() async {
        await load(for: activeUser)
    }

    func add(candidate: Candidate) async {
        let user = activeUser
        guard user != nil, !isLoading, !isInGuide(candidate: candidate) else { return }
        isLoading = true
        errorMessage = nil
        defer { if activeUser == user { isLoading = false } }
        do {
            let saved = try await api.saveCandidate(id: candidate.id)
            guard activeUser == user else { return }
            savedCandidates.append(saved)
        } catch let error as APIError where error.statusCode == 409 {
            // Another device may have already saved this candidate.
            do {
                let saved = try await api.fetchSavedCandidates()
                if activeUser == user { savedCandidates = saved }
            } catch { handle(error, user: user) }
        } catch { handle(error, user: user) }
    }

    func remove(candidateIDs: [Int]) async {
        let user = activeUser
        guard user != nil, !isLoading else { return }
        let records = savedCandidates.filter { candidateIDs.contains($0.candidateId) }
        isLoading = true
        errorMessage = nil
        defer { if activeUser == user { isLoading = false } }
        do {
            for saved in records {
                // DELETE expects the saved record ID, not the candidate ID.
                try await api.deleteSavedCandidate(id: saved.id)
                guard activeUser == user else { return }
                savedCandidates.removeAll { $0.id == saved.id }
            }
        } catch { handle(error, user: user) }
    }

    func isInGuide(candidate: Candidate) -> Bool {
        savedCandidates.contains { $0.candidateId == candidate.id }
    }

    private func handle(_ error: Error, user: String?) {
        guard activeUser == user else { return }
        // On 401 the APIClient already told AuthSession to log out; just drop the data.
        if let error = error as? APIError, error.isUnauthorized {
            savedCandidates = []
            return
        }
        errorMessage = error.localizedDescription
    }
}
