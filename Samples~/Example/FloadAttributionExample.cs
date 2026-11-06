using Fload.Attribution;
using UnityEngine;

/// <summary>
/// What an app does at launch: configure once, then name the user. This sample sends to the
/// local stub ingest (Tools~/stub-ingest.mjs in the package), never to production. The Android
/// emulator reaches the host's localhost at 10.0.2.2; the iOS simulator shares the host's network.
/// The stub is plain http, so the sample's build needs Player Settings > Allow downloads over HTTP.
/// </summary>
public sealed class FloadAttributionExample : MonoBehaviour
{
    [SerializeField] private string writeKey = "fload_pk_example";
    [SerializeField] private string iosEndpoint = "http://localhost:3999";
    [SerializeField] private string androidEndpoint = "http://10.0.2.2:3999";
    [SerializeField] private string userId = "example-user-1";

    private string _installReference;
    private string _log = "";
    private float _nextPoll;

    private void Start()
    {
        var endpoint = Application.platform == RuntimePlatform.Android ? androidEndpoint : iosEndpoint;
        try
        {
            FloadAttribution.Configure(writeKey, new FloadOptions { Endpoint = endpoint, LogLevel = FloadLogLevel.Debug });
            FloadAttribution.SetUserId(userId);
            Log("configured, user id set");
        }
        catch (System.ArgumentException error)
        {
            Log(error.Message);
        }
    }

    private void Update()
    {
        // The native SDK loads its state on its own thread, so the reference appears a moment later.
        if (_installReference != null || Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + 0.5f;
        _installReference = FloadAttribution.InstallReference;
        if (_installReference != null) Log("install reference " + _installReference);
    }

    private void OnGUI()
    {
        var scale = Screen.dpi > 0 ? Screen.dpi / 160f : 1f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        GUILayout.BeginArea(new Rect(16, 48, Screen.width / scale - 32, Screen.height / scale - 64));
        GUILayout.Label("Fload Attribution " + FloadAttribution.Version);
        GUILayout.Label("Install reference: " + (_installReference ?? "(none yet)"));
        GUILayout.Label("User id");
        userId = GUILayout.TextField(userId);
        if (GUILayout.Button("SetUserId"))
        {
            FloadAttribution.SetUserId(userId);
            Log("user id set: " + userId);
        }
        GUILayout.Label(_log);
        GUILayout.EndArea();
    }

    private void Log(string line)
    {
        Debug.Log("FloadAttributionExample: " + line);
        _log = line + "\n" + _log;
    }
}
