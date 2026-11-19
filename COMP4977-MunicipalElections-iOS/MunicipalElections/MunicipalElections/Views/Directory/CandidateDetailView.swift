import SwiftUI

struct CandidateDetailView: View {
    let candidate: Candidate

    @EnvironmentObject private var guideManager: VotingGuideManager
    @EnvironmentObject private var session: AuthSession

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

                guideSection
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
    private var guideSection: some View {
        let isInGuide = guideManager.isInGuide(candidate: candidate)

        if !session.isLoggedIn {
            Text("Log in from the Account tab to save candidates to your voting guide.")
                .font(.footnote)
                .foregroundStyle(.secondary)
                .padding(.horizontal)
        }

        if let error = guideManager.errorMessage {
            Text(error)
                .foregroundStyle(.red)
                .padding(.horizontal)
        }

        Button {
            Task { await guideManager.add(candidate: candidate) }
        } label: {
            Text(guideManager.isLoading ? "Updating Guide..." : (isInGuide ? "Added to Guide!" : "Add to My Voting Guide"))
                .font(.headline)
                .frame(maxWidth: .infinity)
        }
        .buttonStyle(.borderedProminent)
        .controlSize(.large)
        .tint(isInGuide ? .gray : .green)
        .padding(.horizontal)
        .disabled(!session.isLoggedIn || guideManager.isLoading || isInGuide)
    }
}

#Preview {
    NavigationStack {
        CandidateDetailView(candidate: .preview)
            .environmentObject(VotingGuideManager())
            .environmentObject(AuthSession())
    }
}
