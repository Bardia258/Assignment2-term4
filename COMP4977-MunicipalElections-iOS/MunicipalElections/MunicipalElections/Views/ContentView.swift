import SwiftUI

struct ContentView: View {
    @AppStorage("authToken") var authToken: String = ""
    @EnvironmentObject var guideManager: VotingGuideManager

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
                if authToken.isEmpty {
                    LoginView()
                } else {
                    ProfileView()
                }
            }
            .tabItem {
                Label("Account", systemImage: "person.circle")
            }
        }
        .task(id: authToken) {
            await guideManager.load(token: authToken)
        }
    }
}

#Preview {
    ContentView()
        .environmentObject(VotingGuideManager())
}
