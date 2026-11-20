import Foundation
import Network

/// Runs Firestore calls so the user never waits on an endless spinner.
///
/// Offline, Firestore doesn't fail a write: it queues it locally and the
/// `await` only returns once the server confirms it, which may be never.
/// So writes check connectivity first and fail fast without queuing anything,
/// and every call gives up after `timeout` seconds with an offline message.
enum FirestoreRequest {
    static let timeout: Double = 10

    /// For writes: refuse up front when the device has no connection.
    static func write<T>(_ operation: @escaping () async throws -> T) async throws -> T {
        guard NetworkMonitor.shared.isConnected else { throw FirestoreServiceError.offline }
        return try await withTimeout(operation)
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
