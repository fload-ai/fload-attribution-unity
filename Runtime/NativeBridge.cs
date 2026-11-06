using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace Fload.Attribution
{
    internal interface INativeBridge
    {
        void Configure(string key, string endpoint, FloadLogLevel logLevel);
        void SetUserId(string id);
        string InstallReference();
    }

    internal static class NativeBridge
    {
        internal static INativeBridge Create()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new IosBridge();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidBridge();
#else
            return new InactiveBridge();
#endif
        }
    }

    /// <summary>The Editor and every platform other than iOS and Android: nothing to attribute.</summary>
    internal sealed class InactiveBridge : INativeBridge
    {
        private bool _told;

        public void Configure(string key, string endpoint, FloadLogLevel logLevel)
        {
            if (_told) return;
            _told = true;
            Debug.Log("Fload: attribution runs only in iOS and Android builds; nothing is sent from here.");
        }

        public void SetUserId(string id) { }

        public string InstallReference() => null;
    }

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>
    /// iOS: the C functions in Plugins/iOS/FloadAttributionBridge.mm, which call the Swift SDK
    /// through its Objective-C bridge class (Plugins/iOS/FloadAttributionUnityBridge.swift).
    /// </summary>
    internal sealed class IosBridge : INativeBridge
    {
        [DllImport("__Internal")] private static extern void FloadAttribution_Configure(string key, string endpoint, int logLevel);
        [DllImport("__Internal")] private static extern void FloadAttribution_SetUserId(string id);
        // Returns a malloc'd UTF-8 copy (or NULL). The marshaller frees it.
        [DllImport("__Internal")] private static extern string FloadAttribution_InstallReference();

        public void Configure(string key, string endpoint, FloadLogLevel logLevel) =>
            FloadAttribution_Configure(key, endpoint, (int)logLevel);

        public void SetUserId(string id) => FloadAttribution_SetUserId(id);

        public string InstallReference() => FloadAttribution_InstallReference();
    }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// Android: the Kotlin SDK (com.fload.attribution.Fload, from JitPack through the External
    /// Dependency Manager) called over JNI. Its entry points are @JvmStatic.
    /// </summary>
    internal sealed class AndroidBridge : INativeBridge
    {
        private const string FloadClass = "com.fload.attribution.Fload";

        public void Configure(string key, string endpoint, FloadLogLevel logLevel)
        {
            Guarded("Configure", () =>
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var levels = new AndroidJavaClass("com.fload.attribution.FloadLogLevel"))
                using (var level = levels.GetStatic<AndroidJavaObject>(JavaLogLevel(logLevel)))
                using (var options = new AndroidJavaObject("com.fload.attribution.FloadOptions", endpoint, level))
                using (var fload = new AndroidJavaClass(FloadClass))
                {
                    // Fload.configure keeps only the application context.
                    fload.CallStatic("configure", activity, key, options);
                }
            });
        }

        public void SetUserId(string id)
        {
            Guarded("SetUserId", () =>
            {
                using (var fload = new AndroidJavaClass(FloadClass))
                {
                    fload.CallStatic("setUserId", id);
                }
            });
        }

        public string InstallReference()
        {
            string reference = null;
            Guarded("InstallReference", () =>
            {
                using (var fload = new AndroidJavaClass(FloadClass))
                {
                    reference = fload.CallStatic<string>("getInstallReference");
                }
            });
            return reference;
        }

        /// <summary>
        /// Attribution must never take the game down. A JNI failure (most often the Kotlin SDK
        /// missing from the build: EDM4U not resolved, or stripped by Minify) is logged,
        /// without the key or the user id, and the call does nothing.
        /// </summary>
        private static void Guarded(string call, Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                Debug.LogError("Fload: " + call + " failed; is com.github.fload-ai:fload-attribution-android in the build? " + error.GetType().Name);
            }
        }

        private static string JavaLogLevel(FloadLogLevel level)
        {
            switch (level)
            {
                case FloadLogLevel.None: return "NONE";
                case FloadLogLevel.Error: return "ERROR";
                case FloadLogLevel.Info: return "INFO";
                case FloadLogLevel.Debug: return "DEBUG";
                default: return "WARNING";
            }
        }
    }
#endif
}
