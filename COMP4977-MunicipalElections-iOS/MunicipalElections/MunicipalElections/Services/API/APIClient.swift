import Foundation

/// Thin URLSession wrapper for the ASP.NET API hosted on Azure App Service.
/// Every request funnels through `send`, which turns transport, HTTP and
/// decoding failures into an `APIError` with a user-friendly message.
final class APIClient {
    static let shared = APIClient()
    static let baseURL = URL(string: "https://municipal-elections-d8ffazd0dfevdpc6.westus3-01.azurewebsites.net/")!

    private let session: URLSession
    private let decoder = JSONDecoder()
    private let encoder = JSONEncoder()

    init(session: URLSession = .shared) {
        self.session = session
    }

    static func imageURL(for path: String?) -> URL? {
        guard let path, !path.isEmpty else { return nil }
        return URL(string: path, relativeTo: baseURL)?.absoluteURL
    }

    // MARK: - Directory

    func fetchMunicipalities() async throws -> [Municipality] {
        try await get("api/municipalities")
    }

    /// Candidates for the user's city only.
    func fetchCandidates(for municipality: Municipality) async throws -> [Candidate] {
        try await get("api/candidates", query: Self.cityQuery(for: municipality))
    }

    /// Offices (with seat counts) for the user's city only.
    func fetchPositions(for municipality: Municipality) async throws -> [Position] {
        try await get("api/positions", query: Self.cityQuery(for: municipality))
    }

    /// Every directory call is scoped to one city. `municipalityId` is what the
    /// deployed API filters on today; `city` is sent too for the backend's city
    /// filter. The backend rejects the pair only if they disagree, and both come
    /// from the same municipality record.
    static func cityQuery(for municipality: Municipality) -> [URLQueryItem] {
        [URLQueryItem(name: "municipalityId", value: String(municipality.id)),
         URLQueryItem(name: "city", value: municipality.name)]
    }

    // MARK: - Request helpers

    func get<Response: Decodable>(_ path: String, query: [URLQueryItem] = [],
                                  requiresAuth: Bool = false) async throws -> Response {
        try decode(try await send(path: path, query: query, requiresAuth: requiresAuth))
    }

    func post<Body: Encodable, Response: Decodable>(_ path: String, body: Body,
                                                    requiresAuth: Bool = false) async throws -> Response {
        try decode(try await send(path: path, method: "POST", body: try encoder.encode(body),
                                  requiresAuth: requiresAuth))
    }

    func post<Body: Encodable>(_ path: String, body: Body, requiresAuth: Bool = false) async throws {
        _ = try await send(path: path, method: "POST", body: try encoder.encode(body), requiresAuth: requiresAuth)
    }

    /// `requiresAuth` attaches the Keychain JWT as `Authorization: Bearer`.
    /// A missing/expired token or a 401 posts `.sessionExpired` so `AuthSession` logs out.
    func send(path: String, method: String = "GET", query: [URLQueryItem] = [],
              body: Data? = nil, requiresAuth: Bool = false) async throws -> Data {
        var token: String?
        if requiresAuth {
            guard let stored = KeychainService.read(.authToken), !JWT.isExpired(stored) else {
                NotificationCenter.default.post(name: .sessionExpired, object: nil)
                throw APIError.sessionExpired
            }
            token = stored
        }

        var components = URLComponents(url: Self.baseURL.appendingPathComponent(path),
                                       resolvingAgainstBaseURL: true)
        if !query.isEmpty { components?.queryItems = query }
        guard let url = components?.url else {
            throw APIError(statusCode: 0, message: "The app built an invalid request. Please try again.")
        }

        var request = URLRequest(url: url)
        request.httpMethod = method
        request.httpBody = body
        request.timeoutInterval = 30
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        if body != nil { request.setValue("application/json", forHTTPHeaderField: "Content-Type") }
        if let token { request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization") }

        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await session.data(for: request)
        } catch let error as URLError where error.code != .cancelled {
            throw APIError.network(error)
        }

        guard let http = response as? HTTPURLResponse else {
            throw APIError.invalidResponse
        }
        guard (200..<300).contains(http.statusCode) else {
            if http.statusCode == 401 && requiresAuth {
                NotificationCenter.default.post(name: .sessionExpired, object: nil)
                throw APIError.sessionExpired
            }
            throw APIError.http(statusCode: http.statusCode, data: data, decoder: decoder,
                                isAuthenticated: requiresAuth)
        }
        return data
    }

    private func decode<Response: Decodable>(_ data: Data) throws -> Response {
        do {
            return try decoder.decode(Response.self, from: data)
        } catch {
            #if DEBUG
            print("APIClient decode \(Response.self) failed: \(error)")
            #endif
            throw APIError.invalidResponse
        }
    }
}

struct APIError: LocalizedError {
    let statusCode: Int
    let message: String
    var errorDescription: String? { message }

    var isUnauthorized: Bool { statusCode == 401 }

    static let sessionExpired = APIError(statusCode: 401, message: AuthSession.expiredMessage)

    static let invalidResponse = APIError(statusCode: 0,
        message: "The server sent a response the app couldn't read. Please try again later.")

    static func network(_ error: URLError) -> APIError {
        let message: String
        switch error.code {
        case .notConnectedToInternet, .networkConnectionLost, .dataNotAllowed:
            message = "You appear to be offline. Check your internet connection and try again."
        case .timedOut:
            message = "The server took too long to respond. Please try again."
        case .cannotFindHost, .cannotConnectToHost, .dnsLookupFailed:
            message = "Couldn't reach the election server. Please try again later."
        case .secureConnectionFailed, .serverCertificateUntrusted, .serverCertificateHasBadDate:
            message = "A secure connection to the server couldn't be made."
        default:
            message = "A network error occurred. Please try again."
        }
        return APIError(statusCode: 0, message: message)
    }

    static func http(statusCode: Int, data: Data, decoder: JSONDecoder, isAuthenticated: Bool) -> APIError {
        let problem = try? decoder.decode(APIProblem.self, from: data)
        let validation = problem?.errors?
            .sorted(by: { $0.key < $1.key })
            .flatMap { $0.value }
            .joined(separator: "\n")
        let fallback: String
        switch statusCode {
        case 401:
            fallback = isAuthenticated ? "Your session has expired. Please log in again."
                                       : "Invalid email or password."
        case 403: fallback = "You don't have permission to do that."
        case 404: fallback = "The requested item couldn't be found."
        case 500...: fallback = "The server is having trouble right now (HTTP \(statusCode)). Please try again later."
        default: fallback = "Request failed (HTTP \(statusCode)). Please try again."
        }
        let message = [validation, problem?.message, problem?.detail, problem?.title]
            .compactMap { $0 }
            .first(where: { !$0.isEmpty }) ?? fallback
        return APIError(statusCode: statusCode, message: message)
    }
}

/// ASP.NET `ProblemDetails` / `ValidationProblemDetails` plus the `{ message }` shape used by AuthController.
private struct APIProblem: Decodable {
    let message: String?
    let title: String?
    let detail: String?
    let errors: [String: [String]]?
}
