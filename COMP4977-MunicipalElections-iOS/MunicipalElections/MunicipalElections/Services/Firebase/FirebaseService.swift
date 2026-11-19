import FirebaseCore
import FirebaseFirestore

/// Single entry point for Firebase. `configure()` runs once at launch from
/// the app delegate; other services get Firestore through `firestore`.
enum FirebaseService {
    static func configure() {
        // Reads GoogleService-Info.plist from the app bundle.
        guard FirebaseApp.app() == nil else { return }
        FirebaseApp.configure()
    }

    static var firestore: Firestore { Firestore.firestore() }
}
