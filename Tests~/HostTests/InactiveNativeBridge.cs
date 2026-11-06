using System.Collections.Generic;

namespace Fload.Attribution
{
    // Stands in for Runtime/NativeBridge.cs (which needs UnityEngine) in the host tests:
    // it records what FloadAttribution passes to the native side.
    internal interface INativeBridge
    {
        void Configure(string key, string endpoint, FloadLogLevel logLevel);
        void SetUserId(string id);
        string InstallReference();
    }

    internal sealed class RecordingBridge : INativeBridge
    {
        internal readonly List<string> Calls = new List<string>();
        internal string Reference;

        public void Configure(string key, string endpoint, FloadLogLevel logLevel) =>
            Calls.Add($"configure {key} {endpoint} {logLevel}");

        public void SetUserId(string id) => Calls.Add($"setUserId {id}");

        public string InstallReference() => Reference;
    }

    internal static class NativeBridge
    {
        internal static readonly RecordingBridge Recording = new RecordingBridge();

        internal static INativeBridge Create() => Recording;
    }
}
