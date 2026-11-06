# Fload Attribution for Unity

Counts your game's installs in Fload and attributes them: to Apple Ads through
Apple's AdServices token on iOS, and to the Google Play Install Referrer on
Android. No IDFA, no advertising ID, no App Tracking Transparency prompt, no
events.

This package is a thin bridge over Fload's native SDKs,
[fload-attribution-ios](https://github.com/fload-ai/fload-attribution-ios) and
[fload-attribution-android](https://github.com/fload-ai/fload-attribution-android).
Everything they do (the offline queue, retries, what is sent) is described in
their READMEs.

Unity 2021.3 or later. iOS 15 or later, Android 7 (API 24) or later.

## Install

1. **The package.** Window > Package Manager > + > Add package from git URL:

   ```
   https://github.com/fload-ai/fload-attribution-unity.git#1.0.0
   ```

   Or in `Packages/manifest.json`:

   ```json
   "com.fload.attribution": "https://github.com/fload-ai/fload-attribution-unity.git#1.0.0"
   ```

2. **Android: the External Dependency Manager.** The Kotlin SDK is a Gradle
   dependency, which the package declares in `Editor/FloadAttributionDependencies.xml`
   for the [External Dependency Manager for Unity](https://github.com/googlesamples/unity-jar-resolver)
   (EDM4U). Most projects that use Firebase, AdMob or another Google SDK already
   have it. If yours does not, install it (its README lists the ways, including
   OpenUPM), then run Assets > External Dependency Manager > Android Resolver >
   Resolve. It adds `com.github.fload-ai:fload-attribution-android:1.0.1` from
   JitPack.

   Without EDM4U, add the dependency yourself: tick Project Settings > Player >
   Android > Publishing Settings > Custom Main Gradle Template and Custom Settings
   Gradle Template, then add

   ```groovy
   // Assets/Plugins/Android/settingsTemplate.gradle, inside dependencyResolutionManagement { repositories { … } }
   maven { url 'https://jitpack.io'; content { includeGroup 'com.github.fload-ai' } }

   // Assets/Plugins/Android/mainTemplate.gradle, inside dependencies { … }
   implementation 'com.github.fload-ai:fload-attribution-android:1.0.1'
   ```

3. **iOS: the minimum version.** Set Project Settings > Player > iOS > Other
   Settings > Target minimum iOS Version to 15.0. The build stops with that
   message if it is lower. The Swift SDK's sources ship inside the package
   (`Plugins/iOS`), so there is no CocoaPods step: the iOS SDK is not a pod,
   because the CocoaPods trunk turns read-only on 2 December 2026.

4. **Android with Minify.** If Project Settings > Player > Android > Publishing
   Settings > Minify is on for a build, keep the SDK's entry points, which C#
   reaches through JNI: tick Custom Proguard File and add to
   `Assets/Plugins/Android/proguard-user.txt`

   ```
   -keep class com.fload.attribution.Fload { public *; }
   -keep class com.fload.attribution.FloadOptions { public *; }
   -keep class com.fload.attribution.FloadLogLevel { *; }
   ```

## Use

```csharp
using Fload.Attribution;

// Once, early (for example in the first scene's Awake):
FloadAttribution.Configure("fload_pk_…");

// Optional: link your user id (e.g. RevenueCat's appUserID) to the install.
FloadAttribution.SetUserId(appUserId);
```

The key is your app's public write key from Fload. It can only report installs
for that one app, so it is safe to ship in the build.

| Call | What it does |
| --- | --- |
| `Configure(key, options = null)` | Starts the SDK. Returns at once; the native SDK works in the background. A second call is ignored. Throws `ArgumentException` for an empty key or a bad option. |
| `SetUserId(id)` | Links your user id to this install. Safe before `Configure` and on every launch; each id is sent once. |
| `InstallReference` | This install's random id, or `null` until the SDK has loaded. For support questions. |

`new FloadOptions { Endpoint = …, LogLevel = FloadLogLevel.Debug }` is for
testing against your own server. The SDKs never log the key, Apple's token, the
referrer or a user id. In the Editor and on other platforms the calls check
their arguments and do nothing else.

## Privacy

What the SDK collects and how to answer Apple's privacy questions and Google
Play's Data safety form: see the
[iOS](https://github.com/fload-ai/fload-attribution-ios#privacy) and
[Android](https://github.com/fload-ai/fload-attribution-android#google-play-data-safety)
READMEs. The package ships the iOS SDK's `PrivacyInfo.xcprivacy` in
`Plugins/iOS/FloadAttribution/Resources`, enabled for iOS.

## How it is built

- **iOS.** `Runtime/NativeBridge.cs` calls C functions in
  `Plugins/iOS/FloadAttributionBridge.mm` (`[DllImport("__Internal")]`). They
  call `FloadAttributionUnityBridge`, a small `@objc` Swift class, which calls
  the Swift SDK. Its sources are in `Plugins/iOS/FloadAttribution`, copied
  byte for byte from the `fload-attribution-ios` tag (each file name gets a
  `FloadAttribution.` prefix, so it cannot clash with another plugin's Swift
  file in Unity's single UnityFramework module).
- **Android.** `Runtime/NativeBridge.cs` calls `com.fload.attribution.Fload`
  through `AndroidJavaClass`.

## Example

Package Manager > Fload Attribution > Samples > Example > Import. The scene
sends to a local stub of Fload's ingest, never to production: run
`node Tools~/stub-ingest.mjs --port 3999` from the package folder, and tick
Player > Other Settings > Allow downloads over HTTP for the sample build (the
stub is plain http). The Android emulator reaches your machine at `10.0.2.2`,
the iOS simulator at `localhost`.

## Development

Unity ignores the `~` folders, which hold the tooling:

- `sh Tools~/host-test-ios.sh`: builds the Swift SDK, the `@objc` bridge and
  the `.mm` for the host Mac as a module named `UnityFramework`, calls the C
  functions the way `[DllImport]` does, and checks what a local stub ingest
  received. Needs only the Command Line Tools and Node.
- `sh Tools~/host-test-csharp.sh`: compiles the C# for iOS, Android and the
  Editor against Unity's reference assemblies (downloaded from NuGet), and runs
  the argument and build-check tests on .NET.
- `node Tools~/vendor-ios-sdk.mjs <tag> Plugins/iOS/FloadAttribution --prefix=FloadAttribution.`
  refreshes the Swift sources (`--check` compares them), then
  `node Tools~/generate-meta.mjs` writes the `.meta` files Unity needs.

## License

MIT. See [LICENSE.md](LICENSE.md).
