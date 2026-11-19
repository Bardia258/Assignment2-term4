import SwiftUI

struct MunicipalityDetailView: View {
    @StateObject private var viewModel: MunicipalityDetailViewModel

    init(municipality: Municipality) {
        _viewModel = StateObject(wrappedValue: MunicipalityDetailViewModel(municipality: municipality))
    }

    private var municipality: Municipality { viewModel.municipality }

    var body: some View {
        List {
            Section("About") {
                MunicipalityLabel(name: municipality.name, logoUrl: municipality.logoUrl, size: 48)
                    .font(.headline)
                Text(municipality.description)
                Text("Election: \(municipality.formattedElectionDate)")
                    .font(.footnote)
                    .foregroundStyle(.red)
            }

            Section("Candidates") {
                if viewModel.isLoading && viewModel.candidates.isEmpty {
                    ProgressView()
                } else if let error = viewModel.errorMessage {
                    Text(error).foregroundStyle(.red)
                    Button("Retry") { Task { await viewModel.loadCandidates() } }
                } else if viewModel.candidates.isEmpty {
                    Text("No candidates yet.")
                        .foregroundStyle(.secondary)
                } else {
                    ForEach(viewModel.candidates) { candidate in
                        NavigationLink(destination: CandidateDetailView(candidate: candidate)) {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(candidate.name)
                                    .font(.headline)
                                Text(candidate.positionTitle)
                                    .font(.subheadline)
                                    .foregroundStyle(.blue)
                                Text(candidate.profile)
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                                    .lineLimit(2)
                            }
                            .padding(.vertical, 4)
                        }
                    }
                }
            }
        }
        .navigationTitle(municipality.name)
        .refreshable { await viewModel.loadCandidates() }
        .task { await viewModel.loadCandidates() }
    }
}
