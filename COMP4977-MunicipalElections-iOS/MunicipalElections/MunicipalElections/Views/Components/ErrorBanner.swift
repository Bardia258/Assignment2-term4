import SwiftUI

/// Inline, retryable error shown above content that is still usable.
struct ErrorBanner: View {
    let message: String
    let retry: () -> Void

    var body: some View {
        HStack(alignment: .firstTextBaseline) {
            Image(systemName: "exclamationmark.triangle.fill")
            Text(message).frame(maxWidth: .infinity, alignment: .leading)
            Button("Retry", action: retry)
        }
        .font(.caption)
        .foregroundStyle(.red)
        .padding(.horizontal)
        .padding(.vertical, 8)
        .background(Color.red.opacity(0.08))
    }
}
