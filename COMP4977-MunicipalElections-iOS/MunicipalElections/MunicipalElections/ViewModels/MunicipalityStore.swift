import Foundation
import Combine

/// The signed-in user's home municipality, shared by every screen.
/// Loaded from Firestore `users/{email}` after login; screens reload their
/// candidates/offices whenever `current` changes.
@MainActor
final class MunicipalityStore: ObservableObject {
    enum State: Equatable {
        case signedOut
        case loading
        /// First login (or the saved city no longer exists): the user must pick one.
        case needsSelection
        case ready
        case failed(String)
    }

    @Published private(set) var state: State = .signedOut
    @Published private(set) var current: Municipality?
    @Published private(set) var municipalities: [Municipality] = []
    @Published private(set) var isSaving = false
    @Published var saveError: String?

    private let api: APIClient
    private let profiles: UserProfileService
    private var activeEmail: String?

    init(api: APIClient = .shared, profiles: UserProfileService = .shared) {
        self.api = api
        self.profiles = profiles
    }

    /// "City: Burnaby", used in titles, Home and Profile.
    var cityLabel: String { "City: \(current?.name ?? "Not set")" }

    /// Call whenever the signed-in user changes (`nil` = logged out).
    func load(for email: String?) async {
        activeEmail = email
        current = nil
        saveError = nil
        guard let email else {
            state = .signedOut
            municipalities = []
            return
        }
        state = .loading
        do {
            async let list = api.fetchMunicipalities()
            async let profile = profiles.fetchProfile(email: email)
            let (loaded, saved) = try await (list, profile)
            guard activeEmail == email else { return }
            municipalities = loaded
            if let saved, let match = loaded.first(where: { $0.id == saved.municipalityId }) {
                current = match
                state = .ready
            } else {
                state = .needsSelection
            }
        } catch {
            guard activeEmail == email, !Task.isCancelled else { return }
            state = .failed(error.localizedDescription)
        }
    }

    func retry() async {
        await load(for: activeEmail)
    }

    /// Saves to Firestore first; the app only switches city once the save succeeds.
    @discardableResult
    func select(_ municipality: Municipality) async -> Bool {
        guard let email = activeEmail, !isSaving else { return false }
        isSaving = true
        saveError = nil
        defer { isSaving = false }
        do {
            try await profiles.saveMunicipality(municipality, email: email)
            guard activeEmail == email else { return false }
            current = municipality
            state = .ready
            return true
        } catch {
            saveError = error.localizedDescription
            return false
        }
    }
}
