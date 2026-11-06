import Foundation

/// The Objective-C face of fload-attribution-ios for Unity. Swift's `Fload`
/// enum cannot be called from Objective-C, so this class wraps its three calls
/// with `@objc` names that FloadAttributionBridge.mm can reach through the
/// generated `UnityFramework-Swift.h` header.
///
/// The Swift SDK's sources sit beside this file (Plugins/iOS/FloadAttribution),
/// copied from the tagged release, so they compile into the same module.
@objc(FloadAttributionUnityBridge)
public final class FloadAttributionUnityBridge: NSObject {
    /// `logLevel` is the C# `FloadLogLevel` value: 0 none, 1 error, 2 warning, 3 info, 4 debug.
    @objc public static func configure(key: String, endpoint: String?, logLevel: Int) {
        var options = FloadOptions()
        if let endpoint, let url = URL(string: endpoint) {
            options.endpoint = url
        }
        options.logLevel = FloadLogLevel(rawValue: logLevel) ?? .warning
        Fload.configure(key: key, options: options)
    }

    @objc public static func setUserId(_ userId: String) {
        Fload.setUserId(userId)
    }

    @objc public static func installReference() -> String? {
        Fload.installReference
    }
}
