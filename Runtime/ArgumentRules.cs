using System;

namespace Fload.Attribution
{
    /// <summary>
    /// The argument checks every Fload wrapper makes before it calls the native SDK. Free of
    /// UnityEngine so it can be tested on its own.
    /// </summary>
    internal static class ArgumentRules
    {
        internal const string DefaultEndpoint = "https://ingest.fload.com";

        internal struct Checked
        {
            internal string Key;
            internal string Endpoint;
            internal FloadLogLevel LogLevel;
        }

        internal static Checked Configuration(string key, FloadOptions options)
        {
            if (key == null || key.Trim().Length == 0)
            {
                throw new ArgumentException("Fload: Configure needs the app's write key (fload_pk_…).", nameof(key));
            }

            var endpoint = options?.Endpoint ?? DefaultEndpoint;
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                || string.IsNullOrEmpty(uri.Host))
            {
                throw new ArgumentException("Fload: Endpoint must be an http(s) URL.", nameof(options));
            }

            var level = options?.LogLevel ?? FloadLogLevel.Warning;
            if (!Enum.IsDefined(typeof(FloadLogLevel), level))
            {
                throw new ArgumentException("Fload: LogLevel is not a FloadLogLevel value.", nameof(options));
            }

            return new Checked { Key = key, Endpoint = endpoint, LogLevel = level };
        }

        /// <summary>The native SDK trims the id and ignores one that is empty or over 256 characters.</summary>
        internal static string UserId(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id), "Fload: SetUserId needs an id.");
            }
            return id;
        }
    }
}
