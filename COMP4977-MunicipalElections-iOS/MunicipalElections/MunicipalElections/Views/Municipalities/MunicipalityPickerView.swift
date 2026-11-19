import SwiftUI

/// Lets the user choose their home municipality. Shown full screen on first
/// login (can't be skipped) and as a sheet from Profile to change city.
struct MunicipalityPickerView: View {
    enum Mode { case firstLogin, change }

    let mode: Mode
    @EnvironmentObject private var store: MunicipalityStore
    @EnvironmentObject private var session: AuthSession
    @Environment(\.dismiss) private var dismiss
    @State private var selectionId: Int?

    var body: some View {
        NavigationStack {
            List {
                Section {
                    ForEach(store.municipalities) { municipality in
                        Button {
                            selectionId = municipality.id
                        } label: {
                            row(for: municipality)
                        }
                        .buttonStyle(.plain)
                        .accessibilityAddTraits(selectionId == municipality.id ? .isSelected : [])
                    }
                } header: {
                    Text(mode == .firstLogin
                         ? "Pick the municipality you vote in. You'll only see its candidates and offices."
                         : "Candidates, offices and saved selections will switch to the new city.")
                        .textCase(nil)
                } footer: {
                    if mode == .change {
                        Text("Selections saved for other cities are kept, but hidden until you switch back.")
                    }
                }

                if let error = store.saveError {
                    Section {
                        Label(error, systemImage: "exclamationmark.triangle.fill")
                            .foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle(mode == .firstLogin ? "Choose Your City" : "Change City")
            .navigationBarTitleDisplayMode(.inline)
            .overlay {
                if store.municipalities.isEmpty {
                    ContentUnavailableView("No Municipalities", systemImage: "building.columns",
                        description: Text("Municipalities couldn't be loaded."))
                }
            }
            .safeAreaInset(edge: .bottom) { saveButton }
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    if mode == .firstLogin {
                        Button("Log Out") { session.logout() }
                    } else {
                        Button("Cancel") { dismiss() }
                    }
                }
            }
            .interactiveDismissDisabled(store.isSaving)
        }
        .onAppear {
            selectionId = store.current?.id
            store.saveError = nil
        }
    }

    private func row(for municipality: Municipality) -> some View {
        HStack(spacing: 12) {
            MunicipalityLogo(logoUrl: municipality.logoUrl, size: 44)
            VStack(alignment: .leading, spacing: 2) {
                Text(municipality.name).font(.headline)
                Text("\(municipality.province) · Election \(municipality.formattedElectionDate)")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }
            Spacer()
            if selectionId == municipality.id {
                Image(systemName: "checkmark.circle.fill")
                    .foregroundStyle(.tint)
                    .font(.title3)
            }
        }
        .padding(.vertical, 4)
        .contentShape(Rectangle())
    }

    private var selected: Municipality? {
        store.municipalities.first { $0.id == selectionId }
    }

    private var saveButton: some View {
        Button {
            guard let selected else { return }
            Task {
                if await store.select(selected), mode == .change { dismiss() }
            }
        } label: {
            Group {
                if store.isSaving {
                    ProgressView().tint(.white)
                } else {
                    Text(mode == .firstLogin ? "Continue" : "Save City").fontWeight(.bold)
                }
            }
            .frame(maxWidth: .infinity)
        }
        .buttonStyle(.borderedProminent)
        .controlSize(.large)
        .disabled(selected == nil || selected?.id == store.current?.id || store.isSaving)
        .padding()
        .background(.bar)
    }
}

#Preview {
    MunicipalityPickerView(mode: .firstLogin)
        .environmentObject(MunicipalityStore())
        .environmentObject(AuthSession())
}
