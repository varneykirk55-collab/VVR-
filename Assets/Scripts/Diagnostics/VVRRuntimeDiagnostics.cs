using UnityEngine;

/// <summary>
/// Lightweight on-device breadcrumb logger for freeze investigation.
/// Capture on a Quest with:  adb logcat -s Unity:I | findstr VVR_DIAG
/// A heartbeat is written every 2 s. If the app freezes on the main thread, the heartbeat stops and the last
/// VVR_DIAG line before the gap shows what ran last. Frame spikes (&gt;100 ms) are logged individually.
/// </summary>
public class VVRRuntimeDiagnostics : MonoBehaviour
{
    private const float HeartbeatSeconds = 2f;
    private const float SpikeSeconds = 0.1f;

    private float nextHeartbeat;
    private float maxDeltaSinceBeat;
    private int fixedStepsSinceBeat;
    private int framesSinceBeat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<VVRRuntimeDiagnostics>() != null) return;
        GameObject go = new GameObject("VVRRuntimeDiagnostics");
        DontDestroyOnLoad(go);
        go.AddComponent<VVRRuntimeDiagnostics>();
        Debug.Log($"VVR_DIAG start fixedDelta={Time.fixedDeltaTime:F4} maxStep={Time.maximumDeltaTime:F4} " +
                  $"targetFps={Application.targetFrameRate} gfx={SystemInfo.graphicsDeviceType} " +
                  $"cpuCount={SystemInfo.processorCount} mem={SystemInfo.systemMemorySize}MB");
    }

    private void FixedUpdate()
    {
        fixedStepsSinceBeat++;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        framesSinceBeat++;
        if (dt > maxDeltaSinceBeat) maxDeltaSinceBeat = dt;

        if (dt > SpikeSeconds)
        {
            Debug.Log($"VVR_DIAG spike frame={Time.frameCount} dt={dt * 1000f:F0}ms t={Time.realtimeSinceStartup:F2}");
        }

        if (Time.realtimeSinceStartup >= nextHeartbeat)
        {
            nextHeartbeat = Time.realtimeSinceStartup + HeartbeatSeconds;
            int alive = WaveManager.Instance != null ? WaveManager.Instance.EnemiesAlive : -1;
            int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : -1;
            Debug.Log($"VVR_DIAG beat t={Time.realtimeSinceStartup:F1} frames={framesSinceBeat} " +
                      $"fixedSteps={fixedStepsSinceBeat} maxDt={maxDeltaSinceBeat * 1000f:F0}ms wave={wave} enemies={alive}");
            maxDeltaSinceBeat = 0f;
            fixedStepsSinceBeat = 0;
            framesSinceBeat = 0;
        }
    }
}
