import Foundation

struct LoginRequest: Encodable {
    let username: String
    let password: String
}

struct RegisterRequest: Encodable {
    let username: String
    let email: String
    let password: String
    let fullName: String
}

struct AuthResponse: Decodable {
    let token: String
}

/// Talks to the API's `/api/auth` endpoints. View models depend on this
/// service instead of building auth requests themselves.
final class AuthService {
    static let shared = AuthService()

    private let api: APIClient

    init(api: APIClient = .shared) {
        self.api = api
    }

    /// Signs in and returns the JWT. The backend accepts an email in its `username` field.
    func login(email: String, password: String) async throws -> String {
        let response: AuthResponse = try await api.post("api/auth/login",
            body: LoginRequest(username: email, password: password))
        guard !response.token.isEmpty else {
            throw APIError(statusCode: 0, message: "The server did not return a login token.")
        }
        return response.token
    }

    func register(fullName: String, email: String, password: String) async throws {
        try await api.post("api/auth/register",
            body: RegisterRequest(username: email, email: email, password: password, fullName: fullName))
    }

    // TODO(api-auth-me): add the session check once the partner deploys GET /api/auth/me.
    //   func currentUser() async throws -> CurrentUserResponse {
    //       try await api.get("api/auth/me", requiresAuth: true)
    //   }
    // AuthSession.validateWithServer() (see AuthSession.swift) will call it at launch.
}
