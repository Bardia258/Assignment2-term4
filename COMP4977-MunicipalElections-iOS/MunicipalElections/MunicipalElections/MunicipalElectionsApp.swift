//
//  MunicipalElectionsApp.swift
//  MunicipalElections
//
//  Created by Omar Mohamed on 2026-10-17.
//

import SwiftUI
import UIKit

/// Firebase's recommended SwiftUI setup: configure in the app delegate so
/// Firebase is ready before any view touches Auth or Firestore.
final class AppDelegate: NSObject, UIApplicationDelegate {
    func application(_ application: UIApplication,
                     didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]? = nil) -> Bool {
        FirebaseService.configure()
        return true
    }
}

@main
struct MunicipalElectionsApp: App {
    @UIApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var guideManager = VotingGuideManager()

    init() {
        // Existing installations must sign in again with a real account.
        if UserDefaults.standard.string(forKey: "authToken")?.hasPrefix("mock-") == true {
            UserDefaults.standard.removeObject(forKey: "authToken")
            UserDefaults.standard.removeObject(forKey: "userEmail")
        }
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(guideManager)
        }
    }
}
