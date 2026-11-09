using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Fload.Attribution;
using Fload.Attribution.Editor;

internal static class HostTests
{
    private static int _failures;

    private static void Check(bool ok, string name)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + name);
        if (!ok) _failures++;
    }

    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; }
        catch (T) { return true; }
    }

    private static int Main()
    {
        var calls = NativeBridge.Recording.Calls;
        // Run from the package root (Tools~/host-test-csharp.sh): package.json holds the version.
        var packageVersion = JsonDocument.Parse(File.ReadAllText("package.json")).RootElement.GetProperty("version").GetString();
        Check(FloadAttribution.Version == packageVersion, $"FloadAttribution.Version is package.json's version ({packageVersion})");

        FloadAttribution.Configure("fload_pk_abc");
        Check(calls[calls.Count - 1] == $"configure fload_pk_abc https://ingest.fload.com Warning unity {packageVersion}",
            "defaults: production endpoint, warning; the wrapper names itself unity at the package version");

        FloadAttribution.Configure("fload_pk_abc", new FloadOptions { Endpoint = "http://localhost:3999", LogLevel = FloadLogLevel.Debug });
        Check(calls[calls.Count - 1] == $"configure fload_pk_abc http://localhost:3999 Debug unity {packageVersion}", "endpoint and log level pass through");

        var before = calls.Count;
        Check(Throws<ArgumentException>(() => FloadAttribution.Configure(null)), "null key throws");
        Check(Throws<ArgumentException>(() => FloadAttribution.Configure("  ")), "blank key throws");
        Check(Throws<ArgumentException>(() => FloadAttribution.Configure("k", new FloadOptions { Endpoint = "ftp://x" })), "non-http endpoint throws");
        Check(Throws<ArgumentException>(() => FloadAttribution.Configure("k", new FloadOptions { Endpoint = "not a url" })), "bad endpoint throws");
        Check(Throws<ArgumentException>(() => FloadAttribution.Configure("k", new FloadOptions { LogLevel = (FloadLogLevel)9 })), "unknown log level throws");
        Check(calls.Count == before, "a refused call never reaches the native SDK");

        FloadAttribution.SetUserId("user_123");
        Check(calls[calls.Count - 1] == "setUserId user_123", "SetUserId passes the id");
        Check(Throws<ArgumentNullException>(() => FloadAttribution.SetUserId(null)), "null user id throws");

        Check(FloadAttribution.InstallReference == null, "no reference before the native SDK has one");
        NativeBridge.Recording.Reference = "ref-1234";
        Check(FloadAttribution.InstallReference == "ref-1234", "InstallReference reads the native SDK");

        Check((int)FloadLogLevel.None == 0 && (int)FloadLogLevel.Error == 1 && (int)FloadLogLevel.Warning == 2
            && (int)FloadLogLevel.Info == 3 && (int)FloadLogLevel.Debug == 4, "log level values match the Swift SDK's raw values");

        Check(IosTargetVersion.Problem("15.0") == null, "iOS 15.0 target is fine");
        Check(IosTargetVersion.Problem("17.2") == null, "iOS 17.2 target is fine");
        Check(IosTargetVersion.Problem("16") == null, "iOS 16 (no minor) target is fine");
        Check(IosTargetVersion.Problem("13.0") != null, "iOS 13.0 target is refused");
        Check(IosTargetVersion.Problem("") != null, "unset target is refused");

        // EDM4U takes the Kotlin SDK from Maven Central (FLO-4176), the coordinates a project
        // without EDM4U adds by hand too, so a build never holds the library under two names.
        var dependencies = XDocument.Load("Editor/FloadAttributionDependencies.xml");
        var android = dependencies.Descendants("androidPackage").ToList();
        Check(android.Count == 1 && (string)android[0].Attribute("spec") == "com.fload:attribution:1.1.0",
            "EDM4U adds com.fload:attribution:1.1.0");
        Check(dependencies.Descendants("repository").All(r => !r.Value.Contains("jitpack")),
            "EDM4U asks no JitPack repository");

        Console.WriteLine(_failures == 0 ? "all passed" : _failures + " failed");
        return _failures == 0 ? 0 : 1;
    }
}
