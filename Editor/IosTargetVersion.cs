using System;

namespace Fload.Attribution.Editor
{
    /// <summary>The Swift SDK's minimum iOS version, checked without UnityEditor so it can be tested on its own.</summary>
    internal static class IosTargetVersion
    {
        internal static readonly Version MinimumIos = new Version(15, 0);

        /// <summary>The problem with this target iOS version, or null when it is fine.</summary>
        internal static string Problem(string targetOSVersion)
        {
            if (Version.TryParse(Normalise(targetOSVersion), out var target) && target >= MinimumIos) return null;
            return "Fload Attribution needs iOS 15.0 or later. Set Project Settings > Player > iOS > "
                + "Other Settings > Target minimum iOS Version to 15.0 (it is "
                + (string.IsNullOrEmpty(targetOSVersion) ? "unset" : targetOSVersion) + ").";
        }

        private static string Normalise(string version)
        {
            if (string.IsNullOrEmpty(version)) return "0.0";
            return version.Contains(".") ? version : version + ".0";
        }
    }
}
