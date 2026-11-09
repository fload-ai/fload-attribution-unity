# Changelog

## 1.1.1

- Android: the External Dependency Manager adds the Kotlin SDK from Maven
  Central, `com.fload:attribution:1.1.0`, instead of JitPack's
  `com.github.fload-ai:fload-attribution-android:1.1.0` (FLO-4176). It is the
  same library, now under the coordinates a project without EDM4U adds by
  hand, so a build never holds it twice. No JitPack repository is needed.
  If you added the JitPack coordinates to `mainTemplate.gradle` yourself,
  replace them with `com.fload:attribution:1.1.0`.
- No code change: the iOS SDK and the C# API are the same as 1.1.0. Installs
  report the wrapper as `unity` 1.1.1.

## 1.1.0

- native SDKs 1.1.0; installs name this wrapper (FLO-3957). Wraps
  fload-attribution-ios 1.1.0 (vendored in `Plugins/iOS/FloadAttribution`)
  and fload-attribution-android 1.1.0. Every native configure call passes
  `unity` and the package version, which the native SDKs send as
  `sdk.wrapper`.
- The iOS host and simulator checks assert that `sdk.wrapper` reaches the
  stub ingest as `{"unity", <package.json version>}`; the C# host tests hold
  `FloadAttribution.Version` to `package.json`.

## 1.0.0 (2026-11-06)

- First release. Wraps fload-attribution-ios 1.0.0 and fload-attribution-android 1.0.1:
  configure, setUserId and the install reference.
