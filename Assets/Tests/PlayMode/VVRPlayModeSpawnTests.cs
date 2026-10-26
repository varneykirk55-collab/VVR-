using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Task 5 diagnostics: loads the real VikingArena scene in Play Mode, lets the game loop run, forces repeated
/// enemy spawns, and records frame-time spikes, logged errors/exceptions and NavMesh state.
/// This runs on the Editor (Mono, Windows) and therefore CANNOT prove anything about the Quest 2 (IL2CPP, ARM64, Vulkan).
/// </summary>
public class VVRPlayModeSpawnTests
{
    private readonly List<string> logged = new List<string>();

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning)
            logged.Add($"[{type}] {condition}");
    }

    [UnityTest]
    public IEnumerator Spawn_RealScene_NavMeshStateAndFrameTimes()
    {
        logged.Clear();
        Application.logMessageReceived += OnLog;
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/VikingArena.unity", LoadSceneMode.Single);
        yield return null;

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        Debug.Log($"[VVRDiag] NavMesh triangulation: vertices={tri.vertices.Length}, indices={tri.indices.Length}");

        WaveManager wm = Object.FindAnyObjectByType<WaveManager>();
        Assert.IsNotNull(wm);

        float maxFrame = 0f;
        float start = Time.realtimeSinceStartup;
        int spawned = 0;
        // Run ~12 s: first wave starts after timeBetweenWaves (3 s) and spawns every 0.75 s.
        while (Time.realtimeSinceStartup - start < 12f)
        {
            maxFrame = Mathf.Max(maxFrame, Time.unscaledDeltaTime);
            yield return null;
        }

        EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        int onNav = 0;
        foreach (EnemyAI e in enemies)
        {
            NavMeshAgent a = e.GetComponent<NavMeshAgent>();
            if (a != null && a.isOnNavMesh) onNav++;
        }
        Debug.Log($"[VVRDiag] After 12s: wave={wm.CurrentWave}, enemiesAlive={wm.EnemiesAlive}, enemyObjects={enemies.Length}, onNavMesh={onNav}, maxFrame={maxFrame * 1000f:F1}ms");

        // Stress: force 30 extra spawns at once and time the frames afterwards.
        float spikeMax = 0f;
        for (int i = 0; i < 30; i++) { if (wm.SpawnEnemy() != null) spawned++; }
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 3f)
        {
            spikeMax = Mathf.Max(spikeMax, Time.unscaledDeltaTime);
            yield return null;
        }
        Debug.Log($"[VVRDiag] Forced spawns={spawned}, max frame after burst={spikeMax * 1000f:F1}ms, totalEnemies={Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).Length}");

        Application.logMessageReceived -= OnLog;
        var unique = new HashSet<string>(logged);
        foreach (string s in unique) Debug.Log("[VVRDiag] LOGGED: " + s);
        System.IO.File.WriteAllLines("Temp/vvr_playmode_diag.txt", new List<string>(unique));
        Assert.Pass($"diag complete; maxFrame={maxFrame * 1000f:F1}ms; uniqueLogs={unique.Count}");
    }
}
