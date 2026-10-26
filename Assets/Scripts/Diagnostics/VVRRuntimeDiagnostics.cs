using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Debug = UnityEngine.Debug;
using InputDevice = UnityEngine.InputSystem.InputDevice;

/// <summary>
/// Cheap VVR_DIAG logging used by gameplay code. Logs are written without a managed stack trace,
/// which is expensive to capture under IL2CPP on Quest. Mark() records what the main thread was last
/// doing so the watchdog can report it if the main thread hangs.
/// </summary>
public static class VVRDiag
{
    private static volatile string lastMarker = "none";

    public static string LastMarker => lastMarker;

    public static void Log(string message)
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "VVR_DIAG {0}", message);
    }

    public static void Warn(string message)
    {
        Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "VVR_DIAG {0}", message);
    }

    public static void Mark(string marker)
    {
        lastMarker = marker;
    }
}

/// <summary>
/// Lightweight on-device breadcrumb logger for freeze investigation.
/// Capture on a Quest with:  adb logcat -s Unity | findstr VVR_DIAG
/// - "beat" heartbeat every 2 s (frames, fixed steps, worst frame, wave, enemies, error count).
/// - "spike" for any frame over 100 ms, with the last gameplay marker.
/// - "HANG" from a background watchdog thread when the main thread has not finished a frame for 1.5 s.
///   If HANG lines appear, the main thread is stuck and the marker says where.
///   If beats keep coming while the headset image is frozen, the main thread is fine and the stall is
///   in rendering / the XR compositor instead.
/// - XR loader, display refresh rate, controller connect/disconnect, and the first errors/exceptions.
/// </summary>
public class VVRRuntimeDiagnostics : MonoBehaviour
{
    private const float HeartbeatSeconds = 2f;
    private const float SpikeSeconds = 0.1f;
    private const long HangMilliseconds = 1500;
    private const long HangRepeatMilliseconds = 5000;

    private float nextHeartbeat;
    private float maxDeltaSinceBeat;
    private int fixedStepsSinceBeat;
    private int framesSinceBeat;

    // Watchdog state shared with the background thread.
    private static readonly Stopwatch clock = Stopwatch.StartNew();
    private static long lastFrameMs;
    private static volatile bool paused;
    private static volatile bool watchdogRunning;
    private static long hangStartMs = -1;
    private Thread watchdogThread;

    // Errors captured from any thread; reported on the heartbeat to avoid logging inside the log callback.
    private static readonly object errorLock = new object();
    private static int errorCount;
    private static string lastError;
    private static bool lastErrorReported = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<VVRRuntimeDiagnostics>() != null) return;
        GameObject go = new GameObject("VVRRuntimeDiagnostics");
        DontDestroyOnLoad(go);
        go.AddComponent<VVRRuntimeDiagnostics>();
        VVRDiag.Log($"start app={Application.version} unity={Application.unityVersion} device={SystemInfo.deviceModel} " +
                    $"fixedDelta={Time.fixedDeltaTime:F4} maxStep={Time.maximumDeltaTime:F4} " +
                    $"targetFps={Application.targetFrameRate} gfx={SystemInfo.graphicsDeviceType} " +
                    $"cpuCount={SystemInfo.processorCount} mem={SystemInfo.systemMemorySize}MB");
    }

    private void OnEnable()
    {
        Application.logMessageReceivedThreaded += OnLogThreaded;
        InputSystem.onDeviceChange += OnDeviceChange;
        Interlocked.Exchange(ref lastFrameMs, clock.ElapsedMilliseconds);

        watchdogRunning = true;
        watchdogThread = new Thread(WatchdogLoop) { IsBackground = true, Name = "VVR_DIAG watchdog" };
        watchdogThread.Start();
    }

    private void OnDisable()
    {
        Application.logMessageReceivedThreaded -= OnLogThreaded;
        InputSystem.onDeviceChange -= OnDeviceChange;
        watchdogRunning = false;
    }

    private IEnumerator Start()
    {
        foreach (InputDevice device in InputSystem.devices)
        {
            if (device is UnityEngine.InputSystem.XR.XRController || device is UnityEngine.InputSystem.XR.XRHMD)
            {
                VVRDiag.Log($"input present {DescribeDevice(device)}");
            }
        }

        // XR Management initialises during startup; give it a few frames before reporting.
        for (int i = 0; i < 10; i++) yield return null;
        LogXRState();
    }

    private static void LogXRState()
    {
        XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        string loader = manager != null && manager.activeLoader != null ? manager.activeLoader.name : "NONE";

        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        bool displayRunning = false;
        float refreshRate = 0f;
        foreach (XRDisplaySubsystem display in displays)
        {
            if (!display.running) continue;
            displayRunning = true;
            display.TryGetDisplayRefreshRate(out refreshRate);
        }

        if (manager == null || manager.activeLoader == null || !displayRunning)
        {
            VVRDiag.Warn($"xr NOT READY loader={loader} displayRunning={displayRunning}");
        }
        else
        {
            VVRDiag.Log($"xr ready loader={loader} refreshRate={refreshRate:F0}Hz device={XRSettings.loadedDeviceName}");
        }
    }

    private static string DescribeDevice(InputDevice device)
    {
        string usages = device.usages.Count > 0 ? string.Join("|", device.usages) : "-";
        return $"name={device.name} layout={device.layout} usages={usages}";
    }

    private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!(device is UnityEngine.InputSystem.XR.XRController) && !(device is UnityEngine.InputSystem.XR.XRHMD)) return;
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed ||
            change == InputDeviceChange.Reconnected || change == InputDeviceChange.Disconnected ||
            change == InputDeviceChange.UsageChanged)
        {
            VVRDiag.Log($"input {change} {DescribeDevice(device)}");
        }
    }

    private static void OnLogThreaded(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
        if (condition != null && condition.StartsWith("VVR_DIAG")) return;

        lock (errorLock)
        {
            errorCount++;
            if (lastError == null || errorCount <= 5)
            {
                string text = condition ?? "";
                if (text.Length > 200) text = text.Substring(0, 200);
                string where = "";
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    int newline = stackTrace.IndexOf('\n');
                    where = newline > 0 ? stackTrace.Substring(0, newline) : stackTrace;
                }
                lastError = $"{type}: {text} @ {where}";
                lastErrorReported = false;
            }
        }
    }

    private static void WatchdogLoop()
    {
        while (watchdogRunning)
        {
            Thread.Sleep(250);
            if (paused) continue;

            long now = clock.ElapsedMilliseconds;
            long stalledFor = now - Interlocked.Read(ref lastFrameMs);
            if (stalledFor < HangMilliseconds)
            {
                hangStartMs = -1;
                continue;
            }

            if (hangStartMs < 0 || now - hangStartMs >= HangRepeatMilliseconds)
            {
                hangStartMs = now;
                // Logging from a worker thread is allowed; this is the only line that can appear while the main thread is stuck.
                VVRDiag.Warn($"HANG main thread has not completed a frame for {stalledFor} ms; lastMarker={VVRDiag.LastMarker}");
            }
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        paused = isPaused;
        Interlocked.Exchange(ref lastFrameMs, clock.ElapsedMilliseconds);
        VVRDiag.Log($"app pause={isPaused}");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Quest drops focus when the system menu / guardian overlay is up; the app may legitimately stop rendering.
        paused = !hasFocus;
        Interlocked.Exchange(ref lastFrameMs, clock.ElapsedMilliseconds);
        VVRDiag.Log($"app focus={hasFocus}");
    }

    private void FixedUpdate()
    {
        fixedStepsSinceBeat++;
    }

    private void Update()
    {
        long nowMs = clock.ElapsedMilliseconds;
        long previousMs = Interlocked.Exchange(ref lastFrameMs, nowMs);
        if (!paused && nowMs - previousMs >= HangMilliseconds)
        {
            VVRDiag.Warn($"HANG recovered after {nowMs - previousMs} ms; lastMarker={VVRDiag.LastMarker}");
        }

        float dt = Time.unscaledDeltaTime;
        framesSinceBeat++;
        if (dt > maxDeltaSinceBeat) maxDeltaSinceBeat = dt;

        if (dt > SpikeSeconds)
        {
            VVRDiag.Log($"spike frame={Time.frameCount} dt={dt * 1000f:F0}ms t={Time.realtimeSinceStartup:F2} lastMarker={VVRDiag.LastMarker}");
        }

        if (Time.realtimeSinceStartup >= nextHeartbeat)
        {
            nextHeartbeat = Time.realtimeSinceStartup + HeartbeatSeconds;
            int alive = WaveManager.Instance != null ? WaveManager.Instance.EnemiesAlive : -1;
            int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : -1;
            float hp = PlayerHealth.Instance != null ? PlayerHealth.Instance.currentHealth : -1f;

            int errors;
            string errorToReport = null;
            lock (errorLock)
            {
                errors = errorCount;
                if (!lastErrorReported)
                {
                    errorToReport = lastError;
                    lastErrorReported = true;
                }
            }

            VVRDiag.Log($"beat t={Time.realtimeSinceStartup:F1} frames={framesSinceBeat} " +
                        $"fixedSteps={fixedStepsSinceBeat} maxDt={maxDeltaSinceBeat * 1000f:F0}ms wave={wave} enemies={alive} " +
                        $"playerHp={hp:F0} errors={errors}");
            if (errorToReport != null)
            {
                VVRDiag.Warn($"error {errorToReport}");
            }

            maxDeltaSinceBeat = 0f;
            fixedStepsSinceBeat = 0;
            framesSinceBeat = 0;
        }
    }
}
