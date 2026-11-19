import SwiftUI

struct CandidateDetailView: View {
    let candidate: Candidate

    @EnvironmentObject private var selections: SelectionsStore
    @State private var editor: EditorRequest?
    @State private var confirmingDelete = false
    @State private var isDeleting = false
    @State private var actionError: String?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                CandidatePhotoView(photoUrl: candidate.photoUrl)
                    .frame(maxWidth: .infinity)
                    .frame(height: 300)
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                    .padding(.horizontal)

                VStack(alignment: .leading, spacing: 8) {
                    Text(candidate.name)
                        .font(.largeTitle)
                        .fontWeight(.bold)

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
                .padding(.horizontal)

                Divider()

                VStack(alignment: .leading, spacing: 10) {
                    Text("Profile")
                        .font(.headline)
                    Text(candidate.profile)
                        .font(.body)
                        .foregroundStyle(.secondary)
                }
                .padding(.horizontal)

                if candidate.websiteURL != nil || candidate.emailURL != nil {
                    Divider()
                    contactSection
                }

                selectionSection
            }
            .padding(.top)
            .padding(.bottom, 30)
        }
        .navigationTitle("Candidate Profile")
        .navigationBarTitleDisplayMode(.inline)
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
        .padding(.horizontal)
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
                HStack(spacing: 12) {
                    Button { editor = .edit(saved) } label: {
                        Label("Edit Selection", systemImage: "pencil").frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.bordered)

                    Button(role: .destructive) { confirmingDelete = true } label: {
                        Label("Delete Selection", systemImage: "trash").frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.bordered)
                    .disabled(isDeleting)
                }
                .controlSize(.large)
            } else {
                Button { editor = .add(candidate) } label: {
                    Label("Add Selection", systemImage: "plus.circle.fill")
                        .font(.headline)
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .disabled(selections.isLoading)
            }

            if let actionError {
                Text(actionError).font(.footnote).foregroundStyle(.red)
            }
        }
        .padding(.horizontal)
        .sheet(item: $editor) { mode in
            SelectionEditorView(mode: mode.editorMode)
        }
        .confirmationDialog("Delete this selection?", isPresented: $confirmingDelete, titleVisibility: .visible) {
            Button("Delete Selection", role: .destructive, action: deleteSelection)
        } message: {
            Text("\(candidate.name) and your notes will be removed from your selections.")
        }
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
