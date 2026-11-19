import SwiftUI

struct ContentView: View {
    @EnvironmentObject private var session: AuthSession
    @EnvironmentObject private var guideManager: VotingGuideManager
    @Environment(\.scenePhase) private var scenePhase

    var body: some View {
        TabView {
            MunicipalityListView()
                .tabItem {
                    Label("Municipalities", systemImage: "building.columns")
                }

            CandidateDirectoryView()
                .tabItem {
                    Label("Directory", systemImage: "magnifyingglass")
                }

            VotingGuideView()
                .tabItem {
                    Label("My Guide", systemImage: "checklist")
                }

            Group {
                if session.isLoggedIn {
                    ProfileView()
                } else {
                    LoginView()
                }
            }
            .tabItem {
                Label("Account", systemImage: "person.circle")
            }
        }
        .task(id: session.token) {
            await guideManager.load(for: session.token)
        }
        // Timers don't run while suspended, so re-check the JWT when the app comes back.
        .onChange(of: scenePhase) { _, phase in
            if phase == .active { session.checkExpiry() }
        }
        .alert("Signed Out", isPresented: Binding(
            get: { session.notice != nil },
            set: { if !$0 { session.notice = nil } }
        )) {
            Button("OK", role: .cancel) {}
        } message: {
            Text(session.notice ?? "")
        }
    }
}

#Preview {
    ContentView()
        .environmentObject(AuthSession())
        .environmentObject(VotingGuideManager())
}
