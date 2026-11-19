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

/// The signed-in user's Firestore document: `users/{lowercased email}`.
struct UserProfile: Codable, Equatable {
    let email: String
    let municipalityId: Int
    let municipalityName: String
    let updatedAt: Date?
}

/// A candidate the user saved, stored in Firestore `CandidateSelections/{id}`.
/// `id` is "{userEmail}_{candidateId}", so one user can't save the same candidate twice.
struct CandidateSelection: Identifiable, Codable, Equatable {
    static let maxNotesLength = 500

    let id: String
    let userEmail: String
    let candidateId: Int
    let candidateName: String
    let positionName: String
    let municipalityName: String
    let municipalityId: Int
    let photoUrl: String?
    var notes: String
    let dateCreated: Date
    var dateModified: Date

    static func documentID(userEmail: String, candidateId: Int) -> String {
        "\(userEmail.lowercased())_\(candidateId)"
    }

    var wasEdited: Bool { dateModified.timeIntervalSince(dateCreated) > 1 }
}

#if DEBUG
extension Municipality {
    static let preview = Municipality(id: 1, name: "Pitt Meadows", province: "BC",
        electionDate: "2026-10-17T00:00:00", description: "Sample municipality for previews.", logoUrl: nil)
}

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
