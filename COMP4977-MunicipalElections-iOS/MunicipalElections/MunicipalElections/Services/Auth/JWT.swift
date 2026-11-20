import Foundation

/// Reads the `exp` claim from the API's JWT so the app can log out *before*
/// sending a request with a dead token. The signature is not checked here;
/// the server stays the authority and a 401 still forces a logout.
enum JWT {
    /// Server allows 30 s of clock skew; expire slightly early to be safe.
    static let leeway: TimeInterval = 30

    static func expirationDate(of token: String) -> Date? {
        let parts = token.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 3, parts.allSatisfy({ !$0.isEmpty }) else { return nil }
        var base64 = String(parts[1])
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        while base64.count % 4 != 0 { base64 += "=" }
        guard let data = Data(base64Encoded: base64),
              let payload = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let exp = (payload["exp"] as? NSNumber)?.doubleValue,
              exp.isFinite, exp > 0 else { return nil }
        return Date(timeIntervalSince1970: exp)
    }

    /// Missing or malformed expiry claims fail closed.
    static func isExpired(_ token: String, now: Date = Date()) -> Bool {
        guard let expiry = expirationDate(of: token) else { return true }
        return expiry.addingTimeInterval(-leeway) <= now
    }
}
