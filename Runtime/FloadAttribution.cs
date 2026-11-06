using System;

namespace Fload.Attribution
{
    /// <summary>
    /// Fload install attribution. Call <see cref="Configure(string, FloadOptions)"/> once, early in
    /// launch. On the app's first launch the native SDK reports the install to Fload (Apple's
    /// AdServices token on iOS, the Play Install Referrer on Android) and keeps it queued on disk
    /// until Fload has it. No IDFA, no advertising id, no tracking prompt.
    /// <code>
    /// FloadAttribution.Configure("fload_pk_…");
    /// FloadAttribution.SetUserId(revenueCatAppUserId);   // optional
    /// </code>
    /// In the Editor and on other platforms every call checks its arguments and does nothing else.
    /// </summary>
    public static class FloadAttribution
    {
        /// <summary>This package's version.</summary>
        public const string Version = "1.0.0";

        private static readonly INativeBridge Bridge = NativeBridge.Create();

        /// <summary>
        /// Starts the SDK. Returns at once; the native SDK does its disk and network work in the
        /// background.
        /// </summary>
        /// <param name="key">The app's public write key (<c>fload_pk_…</c>) from Fload. It names the app only.</param>
        /// <param name="options">Where to send and how much to log. The defaults are right for production.</param>
        /// <exception cref="ArgumentException">The key is empty, or the options are not valid.</exception>
        public static void Configure(string key, FloadOptions options = null)
        {
            var configuration = ArgumentRules.Configuration(key, options);
            Bridge.Configure(configuration.Key, configuration.Endpoint, configuration.LogLevel);
        }

        /// <summary>
        /// Links your app's user id (for example RevenueCat's <c>appUserID</c>) to this install.
        /// Optional. Safe to call before <see cref="Configure(string, FloadOptions)"/> and on every
        /// launch: the same id is sent once.
        /// </summary>
        /// <exception cref="ArgumentNullException">The id is null.</exception>
        public static void SetUserId(string id)
        {
            Bridge.SetUserId(ArgumentRules.UserId(id));
        }

        /// <summary>
        /// This install's random reference, or null before <see cref="Configure(string, FloadOptions)"/>
        /// has loaded the SDK's state. Useful when asking Fload support about one install.
        /// </summary>
        public static string InstallReference => Bridge.InstallReference();
    }

    /// <summary>How the SDK sends and logs.</summary>
    [Serializable]
    public sealed class FloadOptions
    {
        /// <summary>Fload's ingest endpoint. Change it only to test against your own Fload API.</summary>
        public string Endpoint = ArgumentRules.DefaultEndpoint;

        /// <summary>What the native SDK writes to the device log. It never logs the key, the token, the referrer or a user id.</summary>
        public FloadLogLevel LogLevel = FloadLogLevel.Warning;
    }

    /// <summary>The native SDK's log level. The values match both native SDKs' order.</summary>
    public enum FloadLogLevel
    {
        None = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Debug = 4,
    }
}
