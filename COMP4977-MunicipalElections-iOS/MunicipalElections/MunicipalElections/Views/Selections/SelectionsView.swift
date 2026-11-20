import SwiftUI

/// "My Candidate Selections": the signed-in user's saved candidates for the current city.
struct SelectionsView: View {
    @EnvironmentObject private var selections: SelectionsStore
    @EnvironmentObject private var store: MunicipalityStore
    @State private var editing: CandidateSelection?
    @State private var pendingDelete: CandidateSelection?
    @State private var actionError: String?

    var body: some View {
        NavigationStack {
            Group {
                if selections.isLoading && selections.selections.isEmpty {
                    ProgressView("Loading Selections...")
                } else if let error = selections.errorMessage, selections.selections.isEmpty {
                    ContentUnavailableView {
                        Label("Couldn't Load Selections", systemImage: "exclamationmark.icloud")
                    } description: {
                        Text(error)
                    } actions: {
                        Button("Retry") { Task { await selections.refresh() } }
                    }
                } else if selections.selections.isEmpty {
                    ScrollView {
                        ContentUnavailableView {
                            Label("No Selections Yet", systemImage: "checklist")
                        } description: {
                            Text("Open a candidate from \(store.current?.name ?? "your city") and tap Add Selection.")
                        }
                        .padding(.top, 60)
                    }
                    .refreshable { await selections.refresh() }
                } else {
                    list
                }
            }
            .navigationTitle("My Candidate Selections")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) { CityBadge(municipality: store.current) }
            }
            .sheet(item: $editing) { selection in
                SelectionEditorView(mode: .edit(selection))
            }
            .confirmationDialog("Delete this selection?", isPresented: Binding(
                get: { pendingDelete != nil }, set: { if !$0 { pendingDelete = nil } }
            ), titleVisibility: .visible, presenting: pendingDelete) { selection in
                Button("Delete \(selection.candidateName)", role: .destructive) { delete(selection) }
            } message: { selection in
                Text("\(selection.candidateName) and your notes will be removed.")
            }
            .alert("Something Went Wrong", isPresented: Binding(
                get: { actionError != nil }, set: { if !$0 { actionError = nil } }
            )) {
                Button("OK", role: .cancel) {}
            } message: {
                Text(actionError ?? "")
            }
        }
    }

    private var list: some View {
        List {
            if let error = selections.errorMessage {
                Label(error, systemImage: "exclamationmark.triangle.fill")
                    .font(.footnote)
                    .foregroundStyle(.red)
            }
            if let warning = selections.dataWarning {
                Label(warning, systemImage: "wifi.exclamationmark")
                    .font(.footnote)
                    .foregroundStyle(.orange)
            }
            ForEach(selections.positionGroups) { group in
                Section {
                    ForEach(group.selections) { selection in
                        SelectionRow(selection: selection,
                                     onEdit: { editing = selection },
                                     onDelete: { pendingDelete = selection })
                            .swipeActions(edge: .trailing) {
                                Button("Delete", role: .destructive) { pendingDelete = selection }
                                Button("Edit") { editing = selection }.tint(.blue)
                            }
                    }
                } header: {
                    HStack {
                        Text(group.name)
                        Spacer()
                        Text(group.chosenSummary)
                            .foregroundStyle(group.seats.map { group.selections.count >= $0 } == true ? .green : .secondary)
                    }
                    .accessibilityElement(children: .combine)
                }
            }
        }
        .refreshable { await selections.refresh() }
    }

    private func delete(_ selection: CandidateSelection) {
        Task {
            do {
                try await selections.delete(selection)
            } catch {
                actionError = error.localizedDescription
            }
        }
    }
}

private struct SelectionRow: View {
    let selection: CandidateSelection
    let onEdit: () -> Void
    let onDelete: () -> Void
    @ScaledMetric(relativeTo: .headline) private var photoSize: CGFloat = 64

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .top, spacing: 12) {
                CandidatePhotoView(photoUrl: selection.photoUrl)
                    .frame(width: photoSize, height: photoSize)
                    .clipShape(RoundedRectangle(cornerRadius: 10))

                VStack(alignment: .leading, spacing: 3) {
                    Text(selection.candidateName).font(.headline)
                    Text(selection.positionName).font(.subheadline).foregroundStyle(.blue)
                    Label(selection.municipalityName, systemImage: "building.columns")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Text("Saved \(selection.dateCreated.formatted(date: .abbreviated, time: .shortened))"
                         + (selection.wasEdited ? " · edited" : ""))
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }

            if !selection.notes.isEmpty {
                Text(selection.notes)
                    .font(.callout)
                    .padding(10)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(Color(.secondarySystemBackground))
                    .clipShape(RoundedRectangle(cornerRadius: 8))
            }

            HStack {
                Button(action: onEdit) { Label("Edit", systemImage: "pencil") }
                Spacer()
                Button(role: .destructive, action: onDelete) { Label("Delete", systemImage: "trash") }
            }
            .buttonStyle(.borderless)
            .font(.subheadline)
        }
        .padding(.vertical, 6)
    }
}

#Preview {
    SelectionsView()
        .environmentObject(SelectionsStore())
        .environmentObject(MunicipalityStore())
}
