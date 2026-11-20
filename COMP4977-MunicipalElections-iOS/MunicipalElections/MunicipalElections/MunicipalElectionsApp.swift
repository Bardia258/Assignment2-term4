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
        NetworkMonitor.start()
        return true
    }
}

@main
struct MunicipalElectionsApp: App {
    @UIApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var session = AuthSession()
    @StateObject private var municipalityStore = MunicipalityStore()
    @StateObject private var selections = SelectionsStore()
    @StateObject private var syncStatus = SyncStatus()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(session)
                .environmentObject(municipalityStore)
                .environmentObject(selections)
                .environmentObject(syncStatus)
        }
    }
}
