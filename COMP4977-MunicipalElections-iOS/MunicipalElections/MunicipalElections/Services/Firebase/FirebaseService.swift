import FirebaseCore
import FirebaseAuth
import FirebaseFirestore

/// Single entry point for Firebase. `configure()` runs once at launch from
/// the app delegate; the accessors also configure on demand so no caller can
/// touch Firebase before it is ready (e.g. a @StateObject built during launch).
enum FirebaseService {
    static func configure() {
        // Reads GoogleService-Info.plist from the app bundle.
        guard FirebaseApp.app() == nil else { return }
        FirebaseApp.configure()
    }

    static var auth: Auth {
        configure()
        return Auth.auth()
    }

    static var firestore: Firestore {
        configure()
        return Firestore.firestore()
    }
}
