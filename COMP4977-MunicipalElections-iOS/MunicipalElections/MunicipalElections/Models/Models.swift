import Foundation

struct Municipality: Identifiable, Codable, Hashable {
    let id: Int
    let name: String
    let province: String
    let electionDate: String
    let description: String
    let logoUrl: String?

    var formattedElectionDate: String {
        // Parse the API's calendar date without a device time zone shift.
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "yyyy-MM-dd"
        guard let date = formatter.date(from: String(electionDate.prefix(10))) else { return electionDate }
        formatter.dateStyle = .medium
        formatter.timeStyle = .none
        return formatter.string(from: date)
    }
}

/// An elected office in a municipality (e.g. "Councillor", 8 seats).
struct Position: Identifiable, Codable, Hashable {
    let id: Int
    let name: String
    let seatsAvailable: Int
    let municipalityId: Int
    let municipalityName: String?
}

struct Candidate: Identifiable, Codable, Hashable {
    let id: Int
    let name: String
    let profile: String
    let photoUrl: String?
    let website: String?
    let email: String?
    let positionTitle: String
    let municipalityId: Int
    let municipalityName: String?
    let municipality: Municipality?
    // Returned by the API but ignored in A1; needed for seat limits later.
    let positionId: Int?
    let position: Position?

    var displayMunicipalityName: String {
        municipality?.name ?? municipalityName ?? "Municipality unavailable"
    }

    var municipalityLogoUrl: String? { municipality?.logoUrl }

    /// Websites are stored without a scheme (e.g. "johndoe.ca"), so add https:// if needed.
    var websiteURL: URL? {
        guard let raw = website?.trimmingCharacters(in: .whitespacesAndNewlines), !raw.isEmpty else { return nil }
        let lowered = raw.lowercased()
        let full = lowered.hasPrefix("http://") || lowered.hasPrefix("https://") ? raw : "https://\(raw)"
        return URL(string: full)
    }

    var emailURL: URL? {
        guard let raw = email?.trimmingCharacters(in: .whitespacesAndNewlines), !raw.isEmpty else { return nil }
        return URL(string: "mailto:\(raw)")
    }
}

struct SavedCandidate: Decodable, Identifiable {
    let id: Int
    let candidateId: Int
    let candidate: Candidate
}

#if DEBUG
extension Candidate {
    static let preview = Candidate(
        id: 1, name: "John Doe",
        profile: "Experienced leader with 10 years of city council experience.",
        photoUrl: nil, website: "johndoe.ca", email: "john@example.com",
        positionTitle: "Mayor", municipalityId: 1, municipalityName: "Pitt Meadows",
        municipality: nil, positionId: 1,
        position: Position(id: 1, name: "Mayor", seatsAvailable: 1, municipalityId: 1, municipalityName: "Pitt Meadows"))
}
#endif
