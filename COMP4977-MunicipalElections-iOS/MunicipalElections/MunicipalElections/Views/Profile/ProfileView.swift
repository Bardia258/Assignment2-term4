import SwiftUI

struct ProfileView: View {
    @EnvironmentObject private var session: AuthSession
    @EnvironmentObject private var store: MunicipalityStore
    @EnvironmentObject private var selections: SelectionsStore
    @EnvironmentObject private var syncStatus: SyncStatus
    @State private var confirmingLogout = false
    @State private var changingCity = false
    @ScaledMetric(relativeTo: .largeTitle) private var avatarSize: CGFloat = 56

    var body: some View {
        List {
            Section {
                HStack(spacing: 16) {
                    Image(systemName: "person.crop.circle.fill")
                        .font(.system(size: avatarSize))
                        .foregroundStyle(.blue)
                        .accessibilityHidden(true)
                    VStack(alignment: .leading, spacing: 4) {
                        Text(session.email ?? "")
                            .font(.headline)
                        Label("Signed in", systemImage: "checkmark.seal.fill")
                            .font(.subheadline)
                            .foregroundStyle(.green)
                    }
                }
                .padding(.vertical, 8)
            }

            Section("Municipality") {
                HStack {
                    MunicipalityLabel(name: store.cityLabel, logoUrl: store.current?.logoUrl, size: 32)
                    Spacer()
                    Button("Change") { changingCity = true }
                }
                if let municipality = store.current {
                    LabeledContent("Election day", value: municipality.formattedElectionDate)
                }
            }

            Section {
                LabeledContent("Email", value: session.email ?? "")
                LabeledContent("Current city", value: store.current?.name ?? "Not set")
            } header: {
                Text("Account")
            }

            Section {
                LabeledContent("In \(store.current?.name ?? "this city")",
                               value: selections.isLoading && selections.selections.isEmpty
                                   ? "…" : "\(selections.selections.count)")
                if let total = selections.allCitiesCount {
                    LabeledContent("All cities", value: "\(total)")
                }
                LabeledContent("Last synced") {
                    if let date = syncStatus.lastSynced {
                        VStack(alignment: .trailing, spacing: 2) {
                            Text(date.formatted(date: .abbreviated, time: .shortened))
                            Text(date, style: .relative).font(.caption) + Text(" ago").font(.caption)
                        }
                    } else {
                        Text("Not yet")
                    }
                }
                Button("Sync Now") { Task { await selections.refresh() } }
                    .disabled(selections.isLoading)
            } header: {
                Text("Saved Selections")
            } footer: {
                if let error = selections.errorMessage {
                    Text(error).foregroundStyle(.red)
                } else if let warning = selections.dataWarning {
                    Text(warning).foregroundStyle(.orange)
                } else {
                    Text("Updated after every successful load or save to Firestore.")
                }
            }

            Section {
                Button("Log Out", role: .destructive) { confirmingLogout = true }
            }

            #if DEBUG
            Section {
                Button("Simulate Session Expiry") { session.simulateExpiry() }
            } header: {
                Text("Testing")
            } footer: {
                Text("Debug builds only. Shows the same logout and message as a real expired token.")
            }
            #endif
        }
        .navigationTitle("My Profile")
        .sheet(isPresented: $changingCity) {
            MunicipalityPickerView(mode: .change)
        }
        .confirmationDialog("Log out of your account?", isPresented: $confirmingLogout, titleVisibility: .visible) {
            Button("Log Out", role: .destructive) { session.logout() }
        }
    }
}

#Preview {
    NavigationStack {
        ProfileView()
            .environmentObject(AuthSession())
            .environmentObject(MunicipalityStore())
            .environmentObject(SelectionsStore())
            .environmentObject(SyncStatus())
    }
}
