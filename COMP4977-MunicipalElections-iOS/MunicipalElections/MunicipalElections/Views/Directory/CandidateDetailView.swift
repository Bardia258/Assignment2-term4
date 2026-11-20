import SwiftUI

/// Candidate profile. Compact width (iPhone portrait): photo stacked above the
/// info. Wide layouts (iPad, iPhone landscape): photo beside the info, sized as a
/// fraction of the screen width so it adapts on rotation.
struct CandidateDetailView: View {
    let candidate: Candidate

    @EnvironmentObject private var selections: SelectionsStore
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @Environment(\.verticalSizeClass) private var verticalSizeClass
    @State private var editor: EditorRequest?
    @State private var confirmingDelete = false
    @State private var isDeleting = false
    @State private var actionError: String?

    private var layout: AdaptiveLayout {
        AdaptiveLayout(horizontal: horizontalSizeClass, vertical: verticalSizeClass)
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                if layout.isWide {
                    HStack(alignment: .top, spacing: 24) {
                        photo
                            .aspectRatio(layout == .phoneLandscape ? 1 : 3 / 4, contentMode: .fit)
                            .containerRelativeFrame(.horizontal) { width, _ in
                                width * (layout == .phoneLandscape ? 0.3 : 0.38)
                            }
                        info
                    }
                } else {
                    photo
                        .aspectRatio(4 / 3, contentMode: .fit)
                        .frame(maxWidth: .infinity)
                    info
                }
                selectionSection
            }
            .padding(.horizontal)
            .padding(.top)
            .padding(.bottom, 30)
        }
        .navigationTitle("Candidate Profile")
        .navigationBarTitleDisplayMode(.inline)
    }

    private var photo: some View {
        CandidatePhotoView(photoUrl: candidate.photoUrl)
            .clipShape(RoundedRectangle(cornerRadius: 12))
    }

    private var info: some View {
        VStack(alignment: .leading, spacing: 20) {
            VStack(alignment: .leading, spacing: 8) {
                Text(candidate.name)
                    .font(.largeTitle)
                    .fontWeight(.bold)
                    .fixedSize(horizontal: false, vertical: true)

                Text(candidate.positionTitle)
                    .font(.title3)
                    .foregroundStyle(.blue)

                Text("Municipality")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                MunicipalityLabel(name: candidate.displayMunicipalityName,
                                  logoUrl: candidate.municipalityLogoUrl, size: 44)
                    .font(.headline)
            }

            Divider()

            VStack(alignment: .leading, spacing: 10) {
                Text("Profile")
                    .font(.headline)
                Text(candidate.profile)
                    .font(.body)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }

            if candidate.websiteURL != nil || candidate.emailURL != nil {
                Divider()
                contactSection
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var contactSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Contact Information")
                .font(.headline)

            if let url = candidate.websiteURL, let website = candidate.website {
                Link(destination: url) {
                    Label(website, systemImage: "globe")
                }
            }
            if let url = candidate.emailURL, let email = candidate.email {
                Link(destination: url) {
                    Label(email, systemImage: "envelope")
                }
            }
        }
    }

    @ViewBuilder
    private var selectionSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Divider()
            Text("My Selection").font(.headline)

            if let saved = selections.selection(for: candidate) {
                Label("Saved \(saved.dateCreated.formatted(date: .abbreviated, time: .shortened))",
                      systemImage: "checkmark.circle.fill")
                    .foregroundStyle(.green)
                if !saved.notes.isEmpty {
                    Text(saved.notes)
                        .font(.callout)
                        .padding(10)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(Color(.secondarySystemBackground))
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                }
                // Side by side when they fit; stacked with large text or narrow widths.
                ViewThatFits(in: .horizontal) {
                    HStack(spacing: 12) { editButton(saved); deleteButton }
                    VStack(spacing: 12) { editButton(saved); deleteButton }
                }
                .controlSize(.large)
            } else {
                let seatLimitMessage = selections.seatLimitMessage(for: candidate)
                Button { editor = .add(candidate) } label: {
                    Label("Add Selection", systemImage: "plus.circle.fill")
                        .font(.headline)
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .disabled(selections.isLoading || seatLimitMessage != nil)
                .accessibilityHint(seatLimitMessage ?? "")

                if let seatLimitMessage {
                    Label(seatLimitMessage, systemImage: "person.crop.circle.badge.exclamationmark")
                        .font(.footnote)
                        .foregroundStyle(.orange)
                        .fixedSize(horizontal: false, vertical: true)
                } else if let seats = selections.seats(for: candidate) {
                    let chosen = selections.chosenCount(positionId: candidate.positionId ?? candidate.position?.id,
                                                        positionName: candidate.positionTitle)
                    Text("\(chosen) of \(seats) \(candidate.positionTitle) chosen")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
            }

            if let actionError {
                Text(actionError).font(.footnote).foregroundStyle(.red)
            }
        }
        .sheet(item: $editor) { mode in
            SelectionEditorView(mode: mode.editorMode)
        }
        .confirmationDialog("Delete this selection?", isPresented: $confirmingDelete, titleVisibility: .visible) {
            Button("Delete Selection", role: .destructive, action: deleteSelection)
        } message: {
            Text("\(candidate.name) and your notes will be removed from your selections.")
        }
    }

    private func editButton(_ saved: CandidateSelection) -> some View {
        Button { editor = .edit(saved) } label: {
            Label("Edit Selection", systemImage: "pencil")
                .lineLimit(1)
                .frame(maxWidth: .infinity)
        }
        .buttonStyle(.bordered)
    }

    private var deleteButton: some View {
        Button(role: .destructive) { confirmingDelete = true } label: {
            Label("Delete Selection", systemImage: "trash")
                .lineLimit(1)
                .frame(maxWidth: .infinity)
        }
        .buttonStyle(.bordered)
        .disabled(isDeleting)
    }

    private func deleteSelection() {
        guard let saved = selections.selection(for: candidate) else { return }
        isDeleting = true
        actionError = nil
        Task {
            do {
                try await selections.delete(saved)
            } catch {
                actionError = error.localizedDescription
            }
            isDeleting = false
        }
    }
}

/// Identifiable wrapper so `.sheet(item:)` can present the editor in add or edit mode.
private enum EditorRequest: Identifiable {
    case add(Candidate)
    case edit(CandidateSelection)

    var id: String {
        switch self {
        case .add(let candidate): return "add-\(candidate.id)"
        case .edit(let selection): return "edit-\(selection.id)"
        }
    }

    var editorMode: SelectionEditorView.Mode {
        switch self {
        case .add(let candidate): return .add(candidate)
        case .edit(let selection): return .edit(selection)
        }
    }
}

#Preview {
    NavigationStack {
        CandidateDetailView(candidate: .preview)
            .environmentObject(SelectionsStore())
    }
}
