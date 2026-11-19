import SwiftUI

/// Candidate photo that fills whatever frame its parent gives it.
/// The image sits in an overlay, so `scaledToFill` can never widen the
/// layout, and a failed or missing photo shows a placeholder instead of a spinner.
struct CandidatePhotoView: View {
    let photoUrl: String?

    var body: some View {
        Color(.secondarySystemBackground)
            .overlay {
                if let url = APIClient.imageURL(for: photoUrl) {
                    AsyncImage(url: url) { phase in
                        switch phase {
                        case .success(let image):
                            image.resizable().scaledToFill()
                        case .empty:
                            ProgressView()
                        default:
                            placeholder
                        }
                    }
                } else {
                    placeholder
                }
            }
            .clipped()
            .accessibilityHidden(true)
    }

    private var placeholder: some View {
        Image(systemName: "person.crop.circle.fill")
            .resizable()
            .scaledToFit()
            .padding(8)
            .foregroundStyle(.secondary)
            .frame(maxWidth: 120, maxHeight: 120)
    }
}

#Preview {
    CandidatePhotoView(photoUrl: nil)
        .frame(width: 80, height: 80)
        .clipShape(Circle())
}
