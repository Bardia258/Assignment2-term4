import SwiftUI

struct MunicipalityLogo: View {
    let logoUrl: String?
    var size: CGFloat = 32

    var body: some View {
        AsyncImage(url: APIClient.imageURL(for: logoUrl)) { phase in
            if let image = phase.image {
                image.resizable().scaledToFit().padding(3)
            } else {
                Image(systemName: "building.columns")
                    .resizable()
                    .scaledToFit()
                    .padding(7)
                    .foregroundColor(.secondary)
            }
        }
        .frame(width: size, height: size)
        .background(Color(.secondarySystemBackground))
        .clipShape(RoundedRectangle(cornerRadius: 6))
        .accessibilityHidden(true)
    }
}

/// Compact "City: Burnaby" toolbar badge for the user's selected municipality.
struct CityBadge: View {
    let municipality: Municipality?

    var body: some View {
        HStack(spacing: 6) {
            MunicipalityLogo(logoUrl: municipality?.logoUrl, size: 22)
            Text("City: \(municipality?.name ?? "Not set")")
                .font(.subheadline.weight(.medium))
                .lineLimit(1)
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("City: \(municipality?.name ?? "Not set")")
    }
}

struct MunicipalityLabel: View {
    let name: String
    let logoUrl: String?
    var size: CGFloat = 28

    var body: some View {
        HStack(spacing: 8) {
            MunicipalityLogo(logoUrl: logoUrl, size: size)
            Text(name)
        }
        .accessibilityElement(children: .combine)
    }
}
