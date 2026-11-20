import SwiftUI

/// Sheet for adding a selection (with optional notes) or editing its notes.
/// Used from both Candidate Detail and the Selections tab.
struct SelectionEditorView: View {
    enum Mode {
        case add(Candidate)
        case edit(CandidateSelection)
    }

    let mode: Mode
    @EnvironmentObject private var selections: SelectionsStore
    @Environment(\.dismiss) private var dismiss
    @State private var notes: String
    @State private var isSaving = false
    @State private var errorMessage: String?
    /// About five lines of text at the current Dynamic Type size.
    @ScaledMetric(relativeTo: .body) private var notesMinHeight: CGFloat = 120

    init(mode: Mode) {
        self.mode = mode
        switch mode {
        case .add: _notes = State(initialValue: "")
        case .edit(let selection): _notes = State(initialValue: selection.notes)
        }
    }

    private var candidateName: String {
        switch mode {
        case .add(let candidate): return candidate.name
        case .edit(let selection): return selection.candidateName
        }
    }

    private var positionName: String {
        switch mode {
        case .add(let candidate): return candidate.positionTitle
        case .edit(let selection): return selection.positionName
        }
    }

    private var isAdd: Bool {
        if case .add = mode { return true }
        return false
    }

    private var isTooLong: Bool { notes.count > CandidateSelection.maxNotesLength }

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    LabeledContent("Candidate", value: candidateName)
                    LabeledContent("Position", value: positionName)
                }

                Section {
                    TextEditor(text: $notes)
                        .frame(minHeight: notesMinHeight)
                        .accessibilityLabel("Notes")
                } header: {
                    Text("Notes (optional)")
                } footer: {
                    Text("\(notes.count)/\(CandidateSelection.maxNotesLength) characters")
                        .foregroundStyle(isTooLong ? .red : .secondary)
                }

                if let errorMessage {
                    Section {
                        Label(errorMessage, systemImage: "exclamationmark.triangle.fill")
                            .foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle(isAdd ? "Add Selection" : "Edit Selection")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }.disabled(isSaving)
                }
                ToolbarItem(placement: .confirmationAction) {
                    if isSaving {
                        ProgressView()
                    } else {
                        Button(isAdd ? "Save" : "Update", action: save)
                            .disabled(isTooLong)
                    }
                }
            }
            .interactiveDismissDisabled(isSaving)
        }
    }

    private func save() {
        isSaving = true
        errorMessage = nil
        Task {
            do {
                switch mode {
                case .add(let candidate): try await selections.add(candidate, notes: notes)
                case .edit(let selection): try await selections.updateNotes(of: selection, to: notes)
                }
                dismiss()
            } catch {
                errorMessage = error.localizedDescription
            }
            isSaving = false
        }
    }
}

#Preview {
    SelectionEditorView(mode: .add(.preview))
        .environmentObject(SelectionsStore())
}
