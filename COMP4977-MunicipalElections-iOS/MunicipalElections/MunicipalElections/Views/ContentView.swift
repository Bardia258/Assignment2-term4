import SwiftUI

/// Root flow: Login → (first login) pick municipality → main tabs.
struct ContentView: View {
    @EnvironmentObject private var session: AuthSession
    @EnvironmentObject private var store: MunicipalityStore
    @EnvironmentObject private var guideManager: VotingGuideManager
    @Environment(\.scenePhase) private var scenePhase

    var body: some View {
        Group {
            if !session.isLoggedIn {
                LoginView()
            } else {
                switch store.state {
                case .signedOut, .loading:
                    ProgressView("Loading your municipality...")
                case .failed(let message):
                    ContentUnavailableView {
                        Label("Couldn't Load Your Profile", systemImage: "exclamationmark.triangle")
                    } description: {
                        Text(message)
                    } actions: {
                        Button("Retry") { Task { await store.retry() } }
                            .buttonStyle(.borderedProminent)
                        Button("Log Out", role: .destructive) { session.logout() }
                    }
                case .needsSelection:
                    MunicipalityPickerView(mode: .firstLogin)
                case .ready:
                    MainTabView()
                }
            }
        }
        .task(id: session.token) {
            await store.load(for: session.isLoggedIn ? session.email : nil)
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

struct MainTabView: View {
    var body: some View {
        TabView {
            HomeView()
                .tabItem { Label("Home", systemImage: "house") }

            CandidateDirectoryView()
                .tabItem { Label("Directory", systemImage: "magnifyingglass") }

            VotingGuideView()
                .tabItem { Label("My Guide", systemImage: "checklist") }

            ProfileView()
                .tabItem { Label("Profile", systemImage: "person.circle") }
        }
    }
}

#Preview {
    ContentView()
        .environmentObject(AuthSession())
        .environmentObject(MunicipalityStore())
        .environmentObject(VotingGuideManager())
}
