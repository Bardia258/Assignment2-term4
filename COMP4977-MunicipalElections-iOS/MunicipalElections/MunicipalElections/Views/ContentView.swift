import SwiftUI

/// Root flow: Login → (first login) pick municipality → main tabs.
struct ContentView: View {
    @EnvironmentObject private var session: AuthSession
    @EnvironmentObject private var store: MunicipalityStore
    @EnvironmentObject private var selections: SelectionsStore
    @EnvironmentObject private var syncStatus: SyncStatus
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
            // Wired here (before any load) so Firestore successes update "Last synced".
            store.syncStatus = syncStatus
            selections.syncStatus = syncStatus
            syncStatus.setUser(session.isLoggedIn ? session.email : nil)
            await store.load(for: session.isLoggedIn ? session.email : nil)
        }
        // Selections follow both the user and the city; changing either reloads them.
        .task(id: "\(session.email ?? "")|\(store.current?.id ?? 0)") {
            await selections.load(email: session.isLoggedIn ? session.email : nil,
                                  municipality: store.current)
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

/// The four main sections, shared by the iPhone tab bar and the iPad sidebar.
enum AppSection: String, CaseIterable, Identifiable, Hashable {
    case home, directory, selections, profile

    var id: Self { self }

    var title: String {
        switch self {
        case .home: return "Home"
        case .directory: return "Directory"
        case .selections: return "Selections"
        case .profile: return "Profile"
        }
    }

    var systemImage: String {
        switch self {
        case .home: return "house"
        case .directory: return "magnifyingglass"
        case .selections: return "checklist"
        case .profile: return "person.circle"
        }
    }

    /// The section's root screen. Each container wraps it in its own NavigationStack.
    @MainActor @ViewBuilder
    var rootView: some View {
        switch self {
        case .home: HomeView()
        case .directory: CandidateDirectoryView()
        case .selections: SelectionsView()
        case .profile: ProfileView()
        }
    }
}

/// iPhone: a tab bar with one NavigationStack per tab.
/// iPad: a NavigationSplitView with a sidebar (shown beside the content in
/// landscape, as an overlay in portrait).
/// The container is chosen by device, so rotating never rebuilds it: the
/// selected section, pushed screens, open sheets and typed text all survive.
struct MainTabView: View {
    @State private var tab: AppSection = .home
    @State private var sidebarSelection: AppSection? = .home
    @EnvironmentObject private var selections: SelectionsStore
    @EnvironmentObject private var store: MunicipalityStore

    var body: some View {
        if DeviceKind.isPad {
            splitView
        } else {
            tabView
        }
    }

    private var tabView: some View {
        TabView(selection: $tab) {
            ForEach(AppSection.allCases) { section in
                NavigationStack { section.rootView }
                    .tabItem { Label(section.title, systemImage: section.systemImage) }
                    .tag(section)
            }
        }
    }

    private var splitView: some View {
        NavigationSplitView {
            List(selection: $sidebarSelection) {
                Section {
                    ForEach(AppSection.allCases) { section in
                        NavigationLink(value: section) {
                            Label(section.title, systemImage: section.systemImage)
                        }
                        .badge(section == .selections ? selections.selections.count : 0)
                    }
                } header: {
                    CityBadge(municipality: store.current)
                        .textCase(nil)
                        .padding(.bottom, 4)
                }
            }
            .navigationTitle("Elections")
        } detail: {
            let section = sidebarSelection ?? .home
            NavigationStack { section.rootView }
                // A fresh stack per section, so screens pushed in one section
                // don't appear under another.
                .id(section)
        }
    }
}

#Preview {
    ContentView()
        .environmentObject(AuthSession())
        .environmentObject(MunicipalityStore())
        .environmentObject(SelectionsStore())
        .environmentObject(SyncStatus())
}
