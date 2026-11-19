import SwiftUI

struct ProfileView: View {
    @EnvironmentObject private var session: AuthSession
    @EnvironmentObject private var store: MunicipalityStore
    @State private var confirmingLogout = false
    @State private var changingCity = false

    var body: some View {
        NavigationStack {
            List {
                Section {
                    HStack(spacing: 16) {
                        Image(systemName: "person.crop.circle.fill")
                            .font(.system(size: 56))
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
}

#Preview {
    ProfileView()
        .environmentObject(AuthSession())
        .environmentObject(MunicipalityStore())
}
