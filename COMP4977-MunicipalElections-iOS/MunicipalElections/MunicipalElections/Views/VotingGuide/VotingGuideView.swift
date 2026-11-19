import SwiftUI

struct VotingGuideView: View {
    @EnvironmentObject private var guideManager: VotingGuideManager
    @EnvironmentObject private var session: AuthSession
    @EnvironmentObject private var store: MunicipalityStore

    /// Selections from other cities stay saved but are hidden.
    private var cityCandidates: [Candidate] {
        guideManager.selectedCandidates.filter { $0.municipalityId == store.current?.id }
    }

    var body: some View {
        NavigationStack {
            Group {
                if !session.isLoggedIn {
                    ContentUnavailableView("Not Logged In", systemImage: "person.crop.circle.badge.questionmark",
                        description: Text("Log in from the Account tab to view and save your voting guide."))
                } else if guideManager.isLoading && cityCandidates.isEmpty {
                    ProgressView("Loading Guide...")
                } else if let error = guideManager.errorMessage, cityCandidates.isEmpty {
                    VStack(spacing: 12) {
                        Text(error).foregroundStyle(.red).multilineTextAlignment(.center)
                        Button("Retry") { Task { await guideManager.reload() } }
                    }
                    .padding()
                } else if cityCandidates.isEmpty {
                    ContentUnavailableView {
                        Label("Your guide is empty", systemImage: "checklist")
                    } description: {
                        Text("Add candidates from \(store.current?.name ?? "your city") to build your personalized voting guide.")
                    } actions: {
                        Button("Refresh") { Task { await guideManager.reload() } }
                    }
                } else {
                    guideList
                }
            }
            .navigationTitle("My Voting Guide")
            .toolbar {
                ToolbarItem(placement: .topBarLeading) { CityBadge(municipality: store.current) }
            }
        }
    }

    private var guideList: some View {
        List {
            // A failed add/remove no longer hides the whole guide.
            if let error = guideManager.errorMessage {
                Text(error).font(.footnote).foregroundStyle(.red)
            }
            ForEach(cityCandidates) { candidate in
                NavigationLink(destination: CandidateDetailView(candidate: candidate)) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text(candidate.name)
                            .font(.headline)
                        Text(candidate.positionTitle)
                            .font(.subheadline)
                            .foregroundStyle(.blue)
                        MunicipalityLabel(name: candidate.displayMunicipalityName, logoUrl: candidate.municipalityLogoUrl)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    .padding(.vertical, 4)
                }
            }
            .onDelete { offsets in
                let ids = offsets.map { cityCandidates[$0].id }
                Task { await guideManager.remove(candidateIDs: ids) }
            }
            .deleteDisabled(guideManager.isLoading)
        }
        .refreshable { await guideManager.reload() }
        .toolbar {
            EditButton().disabled(guideManager.isLoading)
        }
    }
}
