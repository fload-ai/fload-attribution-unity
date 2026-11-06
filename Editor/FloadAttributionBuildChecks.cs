using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Fload.Attribution.Editor
{
    /// <summary>
    /// Fails an iOS build early, with the fix, when the project targets an iOS version older than
    /// the Swift SDK's minimum. Without it the build would fail later inside Xcode, on an
    /// "only available in iOS 15" error in the SDK's sources.
    /// </summary>
    internal sealed class FloadAttributionBuildChecks : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var message = IosTargetVersion.Problem(PlayerSettings.iOS.targetOSVersionString);
            if (message != null) throw new BuildFailedException(message);
        }
    }
}
