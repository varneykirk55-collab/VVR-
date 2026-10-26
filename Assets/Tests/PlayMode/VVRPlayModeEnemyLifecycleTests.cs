using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Isolates the enemy spawn path in the real VikingArena scene: measures only the frames on which enemies spawn
/// (not scene-load frames), checks that enemies are on the NavMesh and close in on the player, and checks that a
/// killed enemy is returned to the pool and reused with full health. Editor-only evidence; it cannot prove Quest behaviour.
/// </summary>
public class VVRPlayModeEnemyLifecycleTests
{
    [UnityTest]
    public IEnumerator Spawn_FirstWave_SpawnFramesNavMeshAndPoolReuse()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/VikingArena.unity", LoadSceneMode.Single);
        yield return null;

        WaveManager wm = WaveManager.Instance;
        Assert.IsNotNull(wm, "WaveManager must exist in the scene.");
        Assert.IsNotNull(PlayerHealth.Instance, "PlayerHealth must exist in the scene.");
        PlayerHealth.Instance.maxHealth = 100000f;
        PlayerHealth.Instance.ResetHealth();

        // Wait for the first wave to finish spawning, recording only the frames on which the alive count went up.
        int lastAlive = wm.EnemiesAlive;
        float worstSpawnFrame = 0f;
        int spawnFrames = 0;
        float timeout = Time.realtimeSinceStartup + wm.timeBetweenWaves + 10f;
        while ((wm.CurrentWave < 1 || wm.EnemiesRemainingToSpawn > 0) && Time.realtimeSinceStartup < timeout)
        {
            yield return null;
            if (wm.EnemiesAlive > lastAlive)
            {
                spawnFrames++;
                // The spawn happened during the previous frame's update; this frame's delta covers it.
                worstSpawnFrame = Mathf.Max(worstSpawnFrame, Time.unscaledDeltaTime);
            }
            lastAlive = wm.EnemiesAlive;
        }

        Assert.AreEqual(1, wm.CurrentWave, "Wave 1 should have started.");
        Assert.Greater(spawnFrames, 0, "At least one spawn frame should have been observed.");
        Debug.Log($"[VVRDiag] wave 1 spawn frames={spawnFrames} worst spawn frame={worstSpawnFrame * 1000f:F1}ms");
        Assert.Less(worstSpawnFrame, 1.0f, "A single enemy spawn must never stall the main thread for a second or more.");

        EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Assert.Greater(enemies.Length, 0);

        Vector3 playerPos = Camera.main != null ? Camera.main.transform.position : PlayerHealth.Instance.transform.position;
        playerPos.y = 0f;
        float startDistance = 0f;
        foreach (EnemyAI e in enemies)
        {
            NavMeshAgent agent = e.GetComponent<NavMeshAgent>();
            Assert.IsTrue(agent != null && agent.isOnNavMesh, $"{e.name} must be on the NavMesh.");
            Vector3 p = e.transform.position; p.y = 0f;
            startDistance += Vector3.Distance(p, playerPos);
        }

        yield return new WaitForSeconds(2.5f);

        float endDistance = 0f;
        foreach (EnemyAI e in enemies)
        {
            Vector3 p = e.transform.position; p.y = 0f;
            endDistance += Vector3.Distance(p, playerPos);
        }
        Debug.Log($"[VVRDiag] summed enemy distance to player: start={startDistance:F2} after 2.5s={endDistance:F2}");
        Assert.Less(endDistance, startDistance - 1f, "Enemies should path towards the player.");

        // Kill one enemy and confirm it goes back to the pool and comes back clean.
        EnemyAI victim = enemies[0];
        EnemyHealth victimHealth = victim.GetComponent<EnemyHealth>();
        int killsBefore = wm.TotalKills;
        victimHealth.TakeDamage(victimHealth.maxHealth * 10f, victim.transform.position);
        Assert.IsTrue(victim.IsDead);
        Assert.AreEqual(killsBefore + 1, wm.TotalKills);

        yield return new WaitForSeconds(victim.despawnDelay + 0.5f);
        Assert.IsFalse(victim.gameObject.activeSelf, "Dead enemy should be parked (inactive) in the pool after despawnDelay.");

        GameObject reused = wm.SpawnEnemy();
        Assert.IsNotNull(reused);
        yield return null;
        Assert.AreSame(victim.gameObject, reused, "SpawnEnemy should reuse the pooled instance.");
        Assert.IsFalse(victim.IsDead, "Reused enemy must be alive.");
        Assert.AreEqual(victimHealth.maxHealth, victimHealth.currentHealth, "Reused enemy must have full health.");
        Assert.IsTrue(reused.GetComponent<NavMeshAgent>().isOnNavMesh, "Reused enemy must be back on the NavMesh.");
        foreach (Collider c in reused.GetComponentsInChildren<Collider>(true))
        {
            Assert.IsTrue(c.enabled, $"Collider on {c.name} must be re-enabled on reuse.");
        }
    }
}
