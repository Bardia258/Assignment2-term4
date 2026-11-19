import SwiftUI

/// Home tab: the user's city, its election date and the offices on the ballot.
struct HomeView: View {
    @EnvironmentObject private var store: MunicipalityStore
    @StateObject private var viewModel = HomeViewModel()

    var body: some View {
        NavigationStack {
            List {
                if let municipality = store.current {
                    Section {
                        MunicipalityLabel(name: store.cityLabel, logoUrl: municipality.logoUrl, size: 48)
                            .font(.title3.weight(.semibold))
                        Text(municipality.description)
                        Label("Election day: \(municipality.formattedElectionDate)", systemImage: "calendar")
                            .font(.subheadline)
                            .foregroundStyle(.red)
                    }
                }

                Section("Offices on the Ballot") {
                    if viewModel.isLoading && viewModel.offices.isEmpty {
                        ProgressView()
                    } else if let error = viewModel.errorMessage, viewModel.offices.isEmpty {
                        Text(error).foregroundStyle(.red)
                        Button("Retry") { Task { await viewModel.load(for: store.current) } }
                    } else if viewModel.offices.isEmpty {
                        Text("No offices listed yet.").foregroundStyle(.secondary)
                    } else {
                        ForEach(viewModel.offices) { office in
                            NavigationLink(destination: OfficeCandidatesView(office: office)) {
                                OfficeRow(office: office)
                            }
                        }
                    }
                }
            }
            .navigationTitle(store.current?.name ?? "Home")
            .refreshable { await viewModel.load(for: store.current) }
            .task(id: store.current?.id) { await viewModel.load(for: store.current) }
        }
    }
}

private struct OfficeRow: View {
    let office: HomeViewModel.Office

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(office.position.name).font(.headline)
            HStack(spacing: 12) {
                Label(office.position.seatsAvailable == 1 ? "1 seat" : "\(office.position.seatsAvailable) seats",
                      systemImage: "chair")
                Label(office.candidates.count == 1 ? "1 candidate" : "\(office.candidates.count) candidates",
                      systemImage: "person.2")
            }
            .font(.subheadline)
            .foregroundStyle(.secondary)
        }
        .padding(.vertical, 4)
        .accessibilityElement(children: .combine)
    }
}

private struct OfficeCandidatesView: View {
    let office: HomeViewModel.Office

    var body: some View {
        List {
            if office.candidates.isEmpty {
                Text("No candidates for this office yet.").foregroundStyle(.secondary)
            }
            ForEach(office.candidates) { candidate in
                NavigationLink(destination: CandidateDetailView(candidate: candidate)) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text(candidate.name).font(.headline)
                        Text(candidate.profile)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .lineLimit(2)
                    }
                    .padding(.vertical, 4)
                }
            }
        }
        .navigationTitle(office.position.name)
    }
}

#Preview {
    HomeView()
        .environmentObject(MunicipalityStore())
}
