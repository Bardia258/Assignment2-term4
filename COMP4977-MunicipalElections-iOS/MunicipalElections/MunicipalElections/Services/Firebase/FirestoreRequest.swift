import Foundation
import Network
import FirebaseFirestore

/// Shared Firestore session checks, bounded reads and online transactions.
///
/// Reads may use the cache and time out. Writes use online transactions and
/// await the SDK's actual result. A UI timer cannot cancel a queued SDK write.
enum FirestoreRequest {
    static let timeout: Double = 10

    /// Validate both sessions before accessing a user's Firestore documents.
    /// The API JWT is not a Firebase credential; both identities must exist.
    static func requireIdentity(email: String) throws {
        let expected = email.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        guard !expected.isEmpty, !expected.contains("/"),
              KeychainService.read(.userEmail)?.lowercased() == expected,
              FirebaseService.auth.currentUser?.email?.lowercased() == expected else {
            throw FirestoreServiceError(message: "Your account changed. Please reopen this screen.")
        }
        guard let token = KeychainService.read(.authToken) else {
            throw FirestoreServiceError(message: AuthSession.expiredMessage)
        }
        guard !JWT.isExpired(token) else {
            NotificationCenter.default.post(name: .sessionExpired, object: token)
            throw FirestoreServiceError(message: AuthSession.expiredMessage)
        }
    }

    /// Transactions fail offline instead of persisting an unacknowledged write.
    /// Do not race writes against a timer: a timeout is not proof of failure.
    static func transaction(email: String,
                            _ update: @escaping (Transaction) throws -> Any?) async throws -> Any? {
        try requireIdentity(email: email)
        guard NetworkMonitor.shared.isConnected else { throw FirestoreServiceError.offline }
        try Task.checkCancellation()
        do {
            return try await FirebaseService.firestore.runTransaction { transaction, errorPointer in
                do {
                    try requireIdentity(email: email)
                    return try update(transaction)
                } catch {
                    errorPointer?.pointee = error as NSError
                    return nil
                }
            }
        } catch {
            throw FirestoreServiceError(error)
        }
    }

    /// For reads: Firestore may still answer from its local cache while offline.
    static func read<T>(_ operation: @escaping () async throws -> T) async throws -> T {
        try await withTimeout(operation)
    }

    /// Races the operation against a timer. A task group can't be used here:
    /// it waits for every child, and Firestore's awaits ignore cancellation.
    private static func withTimeout<T>(_ operation: @escaping () async throws -> T) async throws -> T {
        try await withCheckedThrowingContinuation { continuation in
            let gate = ResumeOnce()
            Task {
                do {
                    let value = try await operation()
                    if gate.claim() { continuation.resume(returning: value) }
                } catch {
                    if gate.claim() { continuation.resume(throwing: FirestoreServiceError(error)) }
                }
            }
            Task {
                try? await Task.sleep(for: .seconds(timeout))
                if gate.claim() { continuation.resume(throwing: FirestoreServiceError.offline) }
            }
        }
    }
}

/// Makes sure a continuation is resumed exactly once.
private final class ResumeOnce: @unchecked Sendable {
    private let lock = NSLock()
    private var resumed = false

    func claim() -> Bool {
        lock.lock()
        defer { lock.unlock() }
        guard !resumed else { return false }
        resumed = true
        return true
    }
}

/// Current connectivity from `NWPathMonitor`, used to fail Firestore writes fast.
final class NetworkMonitor: @unchecked Sendable {
    static let shared = NetworkMonitor()

    private let monitor = NWPathMonitor()
    private let lock = NSLock()
    private var connected = true

    var isConnected: Bool {
        lock.lock()
        defer { lock.unlock() }
        return connected
    }

    private init() {
        monitor.pathUpdateHandler = { [weak self] path in
            guard let self else { return }
            self.lock.lock()
            self.connected = path.status == .satisfied
            self.lock.unlock()
        }
        monitor.start(queue: DispatchQueue(label: "MunicipalElections.NetworkMonitor"))
    }

    /// Starts monitoring early so the first write sees the real status.
    static func start() { _ = shared }
}
