import Foundation
import Security

/// Stores small secrets (the API JWT and the signed-in email) in the iOS Keychain
/// instead of UserDefaults. Items are device-only and readable after first unlock.
enum KeychainService {
    enum Key: String {
        case authToken
        case userEmail
    }

    private static let service = "BCIT.MunicipalElections"

    static func save(_ value: String, for key: Key) throws {
        let data = Data(value.utf8)
        delete(key)
        var query = baseQuery(for: key)
        query[kSecValueData as String] = data
        query[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        let status = SecItemAdd(query as CFDictionary, nil)
        guard status == errSecSuccess else { throw KeychainError(status: status) }
    }

    static func read(_ key: Key) -> String? {
        var query = baseQuery(for: key)
        query[kSecReturnData as String] = true
        query[kSecMatchLimit as String] = kSecMatchLimitOne
        var result: AnyObject?
        guard SecItemCopyMatching(query as CFDictionary, &result) == errSecSuccess,
              let data = result as? Data else { return nil }
        return String(data: data, encoding: .utf8)
    }

    static func delete(_ key: Key) {
        SecItemDelete(baseQuery(for: key) as CFDictionary)
    }

    private static func baseQuery(for key: Key) -> [String: Any] {
        [kSecClass as String: kSecClassGenericPassword,
         kSecAttrService as String: service,
         kSecAttrAccount as String: key.rawValue]
    }
}

struct KeychainError: LocalizedError {
    let status: OSStatus
    var errorDescription: String? {
        "Your login couldn't be saved securely on this device (Keychain error \(status)). Please try again."
    }
}
