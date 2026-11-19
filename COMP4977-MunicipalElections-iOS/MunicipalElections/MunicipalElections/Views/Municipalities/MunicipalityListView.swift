import SwiftUI

struct MunicipalityListView: View {
    @StateObject private var viewModel = MunicipalityListViewModel()

    var body: some View {
        NavigationStack {
            List(viewModel.municipalities) { municipality in
                NavigationLink(destination: MunicipalityDetailView(municipality: municipality)) {
                    HStack(spacing: 12) {
                        MunicipalityLogo(logoUrl: municipality.logoUrl, size: 44)
                        VStack(alignment: .leading) {
                            Text(municipality.name)
                                .font(.headline)
                            Text(municipality.province)
                                .font(.subheadline)
                                .foregroundStyle(.secondary)
                        }
                    }
                    .padding(.vertical, 4)
                }
            }
            .navigationTitle("Municipalities")
            .overlay {
                if viewModel.isLoading && viewModel.municipalities.isEmpty {
                    ProgressView("Loading...")
                } else if let error = viewModel.errorMessage, viewModel.municipalities.isEmpty {
                    VStack(spacing: 12) {
                        Text(error).foregroundStyle(.red).multilineTextAlignment(.center)
                        Button("Retry") { Task { await viewModel.load() } }
                    }
                    .padding()
                } else if !viewModel.isLoading && viewModel.municipalities.isEmpty {
                    ContentUnavailableView("No Municipalities", systemImage: "building.columns")
                }
            }
            .refreshable { await viewModel.load() }
            .task { await viewModel.load() }
        }
    }
}

#Preview {
    MunicipalityListView()
}
