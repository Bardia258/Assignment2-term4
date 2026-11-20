import Foundation
import Combine

/// When the app last completed a Firestore load or write that reached the
/// server (cache-only reads while offline don't count). Shown on Profile as
/// "Last synced" and remembered per user between launches.
@MainActor
final class SyncStatus: ObservableObject {
    @Published private(set) var lastSynced: Date?

    private var email: String?
    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    /// Call when the signed-in user changes (`nil` = logged out).
    func setUser(_ email: String?) {
        let key = email?.lowercased()
        guard key != self.email else { return }
        self.email = key
        lastSynced = key.flatMap { defaults.object(forKey: Self.storageKey(for: $0)) as? Date }
    }

    func markSynced(at date: Date = Date()) {
        guard let email else { return }
        lastSynced = date
        defaults.set(date, forKey: Self.storageKey(for: email))
    }

    private static func storageKey(for email: String) -> String {
        "lastFirestoreSync.\(email)"
    }
}
