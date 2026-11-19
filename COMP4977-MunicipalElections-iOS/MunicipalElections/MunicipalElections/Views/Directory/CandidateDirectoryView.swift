import SwiftUI

struct CandidateDirectoryView: View {
    @StateObject private var viewModel = CandidateDirectoryViewModel()

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                if let error = viewModel.municipalityErrorMessage {
                    ErrorBanner(message: "Municipality filter: \(error)") {
                        Task { await viewModel.loadMunicipalities() }
                    }
                }
                if let error = viewModel.errorMessage, !viewModel.candidates.isEmpty {
                    ErrorBanner(message: error) { Task { await viewModel.fetchCandidates() } }
                }
                content
            }
            .searchable(text: $viewModel.searchText, prompt: "Search candidates by name")
            .navigationTitle("Directory")
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) { municipalityMenu }
            }
            .task {
                await viewModel.loadMunicipalities()
            }
            .task(id: viewModel.selectedMunicipalityId) {
                await viewModel.fetchCandidates()
            }
        }
    }

    @ViewBuilder
    private var content: some View {
        if viewModel.showsInitialLoading {
            ProgressView("Loading Directory...")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else if let error = viewModel.errorMessage, viewModel.candidates.isEmpty {
            VStack(spacing: 12) {
                Text(error).foregroundStyle(.red).multilineTextAlignment(.center)
                Button("Retry") { Task { await viewModel.fetchCandidates() } }
            }
            .padding()
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else {
            List(viewModel.filteredCandidates) { candidate in
                NavigationLink(destination: CandidateDetailView(candidate: candidate)) {
                    CandidateRow(candidate: candidate)
                }
            }
            .refreshable { await viewModel.refresh() }
            .overlay {
                if viewModel.filteredCandidates.isEmpty {
                    if viewModel.searchText.isEmpty {
                        ContentUnavailableView("No Candidates", systemImage: "person.3",
                            description: Text("No candidates are listed for this municipality yet."))
                    } else {
                        ContentUnavailableView.search(text: viewModel.searchText)
                    }
                }
            }
        }
    }

    private var municipalityMenu: some View {
        Menu {
            Picker("Municipality", selection: $viewModel.selectedMunicipalityId) {
                Text("All Municipalities").tag(nil as Int?)
                ForEach(viewModel.municipalities) { municipality in
                    Text(municipality.name).tag(Optional(municipality.id))
                }
            }
        } label: {
            HStack(spacing: 4) {
                Text(viewModel.selectedMunicipalityName).lineLimit(1)
                Image(systemName: "chevron.down")
            }
        }
        .disabled(viewModel.municipalities.isEmpty)
        .accessibilityLabel("Filter by municipality")
        .accessibilityValue(viewModel.selectedMunicipalityName)
    }
}

private struct CandidateRow: View {
    let candidate: Candidate
    @ScaledMetric(relativeTo: .headline) private var photoSize: CGFloat = 50

    var body: some View {
        HStack(spacing: 15) {
            CandidatePhotoView(photoUrl: candidate.photoUrl)
                .frame(width: photoSize, height: photoSize)
                .clipShape(Circle())

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
        }
        .padding(.vertical, 4)
    }
}

#Preview {
    CandidateDirectoryView()
        .environmentObject(VotingGuideManager())
        .environmentObject(AuthSession())
}
