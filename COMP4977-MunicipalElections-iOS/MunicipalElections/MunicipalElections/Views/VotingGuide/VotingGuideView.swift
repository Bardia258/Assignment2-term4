import SwiftUI

struct VotingGuideView: View {
    @EnvironmentObject var guideManager: VotingGuideManager
    @AppStorage("authToken") private var authToken = ""

    var body: some View {
        NavigationStack {
            Group {
                if authToken.isEmpty {
                    ContentUnavailableView("Not Logged In", systemImage: "person.crop.circle.badge.questionmark",
                        description: Text("Log in from the Account tab to view and save your voting guide."))
                } else if guideManager.isLoading && guideManager.selectedCandidates.isEmpty {
                    ProgressView("Loading Guide...")
                } else if let error = guideManager.errorMessage, guideManager.selectedCandidates.isEmpty {
                    VStack(spacing: 12) {
                        Text(error).foregroundStyle(.red).multilineTextAlignment(.center)
                        Button("Retry") { Task { await guideManager.load(token: authToken) } }
                    }
                    .padding()
                } else if guideManager.selectedCandidates.isEmpty {
                    ContentUnavailableView {
                        Label("Your guide is empty", systemImage: "checklist")
                    } description: {
                        Text("Browse municipalities and add candidates to build your personalized voting guide.")
                    } actions: {
                        Button("Refresh") { Task { await guideManager.load(token: authToken) } }
                    }
                } else {
                    guideList
                }
            }
            .navigationTitle("My Voting Guide")
        }
    }

    private var guideList: some View {
        List {
            // A failed add/remove no longer hides the whole guide.
            if let error = guideManager.errorMessage {
                Text(error).font(.footnote).foregroundStyle(.red)
            }
            ForEach(guideManager.selectedCandidates) { candidate in
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
                let ids = offsets.map { guideManager.selectedCandidates[$0].id }
                Task { await guideManager.remove(candidateIDs: ids, token: authToken) }
            }
            .deleteDisabled(guideManager.isLoading)
        }
        .refreshable { await guideManager.load(token: authToken) }
        .toolbar {
            EditButton().disabled(guideManager.isLoading)
        }
    }
}
