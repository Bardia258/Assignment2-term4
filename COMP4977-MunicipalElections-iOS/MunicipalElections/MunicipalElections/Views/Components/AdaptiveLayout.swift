import SwiftUI

/// Layout class for the four supported layouts, from size classes, so every
/// screen re-lays itself out live when the device rotates.
///
/// | Device / orientation | horizontal | vertical | layout            |
/// |----------------------|------------|----------|-------------------|
/// | iPhone portrait      | compact    | regular  | `.phonePortrait`  |
/// | iPhone landscape     | compact*   | compact  | `.phoneLandscape` |
/// | iPad portrait        | regular    | regular  | `.pad`            |
/// | iPad landscape       | regular    | regular  | `.pad`            |
///
/// *Plus/Pro Max iPhones report regular width in landscape; they still count as
/// phone landscape because their height is compact. iPad portrait and landscape
/// share size classes; screens tell them apart by available width instead
/// (adaptive grid columns, `containerRelativeFrame`), so landscape gets more columns.
enum AdaptiveLayout: Equatable {
    case phonePortrait, phoneLandscape, pad

    init(horizontal: UserInterfaceSizeClass?, vertical: UserInterfaceSizeClass?) {
        if vertical == .compact {
            self = .phoneLandscape
        } else if horizontal == .regular {
            self = .pad
        } else {
            self = .phonePortrait
        }
    }

    /// Enough room to put things side by side (photo beside info, wider rows).
    var isWide: Bool { self != .phonePortrait }
    var isPad: Bool { self == .pad }
}

/// The root container type is picked by device, never by size class, so a
/// rotation can't swap NavigationStack <-> NavigationSplitView and throw away
/// navigation state. (NavigationSplitView collapses by itself in iPad Slide Over.)
enum DeviceKind {
    @MainActor static var isPad: Bool { UIDevice.current.userInterfaceIdiom == .pad }
}

/// Caps forms (Login, Register) at a comfortable reading width on iPad and in
/// landscape. The width follows Dynamic Type instead of being a fixed size.
struct ReadableWidth: ViewModifier {
    @ScaledMetric(relativeTo: .body) private var maxWidth: CGFloat = 520

    func body(content: Content) -> some View {
        content
            .frame(maxWidth: maxWidth)
            .frame(maxWidth: .infinity)
    }
}

extension View {
    func readableWidth() -> some View { modifier(ReadableWidth()) }
}
