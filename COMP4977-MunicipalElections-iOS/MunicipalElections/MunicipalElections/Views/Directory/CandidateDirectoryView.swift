import SwiftUI

/// Candidates for the user's city, laid out per size class:
/// - iPhone portrait: one-column list.
/// - iPhone landscape: list rows with more detail (profile excerpt, saved status).
/// - iPad portrait / landscape: adaptive card grid; the wider landscape screen
///   simply fits more columns.
struct CandidateDirectoryView: View {
    @EnvironmentObject private var store: MunicipalityStore
    @EnvironmentObject private var selections: SelectionsStore
    @StateObject private var viewModel = CandidateDirectoryViewModel()
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @Environment(\.verticalSizeClass) private var verticalSizeClass
    /// Minimum card width; scales with Dynamic Type so large text gets fewer, wider cards.
    @ScaledMetric(relativeTo: .body) private var minCardWidth: CGFloat = 260

    private var layout: AdaptiveLayout {
        AdaptiveLayout(horizontal: horizontalSizeClass, vertical: verticalSizeClass)
    }

    var body: some View {
        VStack(spacing: 0) {
            if let error = viewModel.errorMessage, !viewModel.candidates.isEmpty {
                ErrorBanner(message: error) { Task { await viewModel.fetchCandidates() } }
            }
            content
        }
        .searchable(text: $viewModel.searchText, prompt: "Search candidates by name")
        .navigationTitle("Directory")
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                CityBadge(municipality: store.current)
            }
        }
        .task(id: store.current?.id) {
            await viewModel.load(for: store.current)
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
            Group {
                if layout.isPad {
                    grid
                } else {
                    list
                }
            }
            .refreshable { await viewModel.fetchCandidates() }
            .overlay {
                if viewModel.filteredCandidates.isEmpty {
                    if viewModel.searchText.isEmpty {
                        ContentUnavailableView("No Candidates", systemImage: "person.3",
                            description: Text("No candidates are listed for \(store.current?.name ?? "this city") yet."))
                    } else {
                        ContentUnavailableView.search(text: viewModel.searchText)
                    }
                }
            }
        }
    }

    private var list: some View {
        List(viewModel.filteredCandidates) { candidate in
            NavigationLink(destination: CandidateDetailView(candidate: candidate)) {
                CandidateRow(candidate: candidate,
                             detailed: layout == .phoneLandscape,
                             isSaved: selections.selection(for: candidate) != nil)
            }
        }
    }

    private var grid: some View {
        ScrollView {
            LazyVGrid(columns: [GridItem(.adaptive(minimum: minCardWidth), spacing: 16, alignment: .top)],
                      spacing: 16) {
                ForEach(viewModel.filteredCandidates) { candidate in
                    NavigationLink(destination: CandidateDetailView(candidate: candidate)) {
                        CandidateCard(candidate: candidate,
                                      isSaved: selections.selection(for: candidate) != nil)
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding()
        }
        .background(Color(.systemGroupedBackground))
    }
}

/// List row. `detailed` (iPhone landscape) uses the extra width for the
/// municipality, a profile excerpt and the saved status beside the name.
private struct CandidateRow: View {
    let candidate: Candidate
    var detailed = false
    var isSaved = false
    @ScaledMetric(relativeTo: .headline) private var photoSize: CGFloat = 50

    var body: some View {
        HStack(alignment: detailed ? .top : .center, spacing: 15) {
            CandidatePhotoView(photoUrl: candidate.photoUrl)
                .frame(width: photoSize, height: photoSize)
                .clipShape(Circle())

            if detailed {
                HStack(alignment: .top, spacing: 16) {
                    VStack(alignment: .leading, spacing: 4) {
                        nameAndPosition
                        MunicipalityLabel(name: candidate.displayMunicipalityName, logoUrl: candidate.municipalityLogoUrl)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)

                    Text(candidate.profile)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .lineLimit(3)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
            } else {
                VStack(alignment: .leading, spacing: 4) {
                    nameAndPosition
                    MunicipalityLabel(name: candidate.displayMunicipalityName, logoUrl: candidate.municipalityLogoUrl)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Spacer(minLength: 0)
                if isSaved { SavedBadge(compact: true) }
            }
        }
        .padding(.vertical, 4)
    }

    private var nameAndPosition: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(spacing: 6) {
                Text(candidate.name).font(.headline)
                if detailed && isSaved { SavedBadge(compact: false) }
            }
            Text(candidate.positionTitle)
                .font(.subheadline)
                .foregroundStyle(.blue)
        }
    }
}

/// iPad grid card: photo on top, then name, office, city and a profile excerpt.
private struct CandidateCard: View {
    let candidate: Candidate
    let isSaved: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            CandidatePhotoView(photoUrl: candidate.photoUrl)
                .aspectRatio(4 / 3, contentMode: .fit)
                .overlay(alignment: .topTrailing) {
                    if isSaved { SavedBadge(compact: false).padding(8) }
                }

            VStack(alignment: .leading, spacing: 6) {
                Text(candidate.name)
                    .font(.headline)
                    .foregroundStyle(.primary)
                Text(candidate.positionTitle)
                    .font(.subheadline)
                    .foregroundStyle(.blue)
                MunicipalityLabel(name: candidate.displayMunicipalityName, logoUrl: candidate.municipalityLogoUrl)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text(candidate.profile)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(3)
                    .multilineTextAlignment(.leading)
            }
            .padding()
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .background(Color(.secondarySystemGroupedBackground))
        .clipShape(RoundedRectangle(cornerRadius: 14))
        .contentShape(RoundedRectangle(cornerRadius: 14))
        .accessibilityElement(children: .combine)
        .accessibilityHint("Opens the candidate profile")
    }
}

/// "Saved" marker for candidates already in My Candidate Selections.
private struct SavedBadge: View {
    let compact: Bool

    var body: some View {
        Group {
            if compact {
                Image(systemName: "checkmark.circle.fill")
                    .foregroundStyle(.green)
            } else {
                Label("Saved", systemImage: "checkmark.circle.fill")
                    .font(.caption.weight(.semibold))
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                    .background(.thinMaterial, in: Capsule())
                    .foregroundStyle(.green)
            }
        }
        .accessibilityLabel("Saved to your selections")
    }
}

#Preview {
    NavigationStack {
        CandidateDirectoryView()
            .environmentObject(MunicipalityStore())
            .environmentObject(SelectionsStore())
    }
}
