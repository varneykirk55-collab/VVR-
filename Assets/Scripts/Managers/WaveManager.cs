using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Wave Settings")]
    public GameObject[] enemyPrefabs;
    public Transform[] spawnPoints;
    public float timeBetweenWaves = 5f;
    public float spawnStaggerDelay = 1f;
    public int baseEnemiesPerWave = 3;
    public float difficultyMultiplier = 1.5f;
    public int maxWaves = 0; // 0 = endless waves; > 0 triggers victory after maxWaves
    [Tooltip("Upper bound on simultaneously alive enemies. Remaining enemies of the wave spawn as others die. 0 = no cap.")]
    public int maxConcurrentEnemies = 6;

    [Header("Pooling")]
    [Tooltip("Enemies created and briefly activated during the opening countdown so first-use costs (Animator, NavMeshAgent, physics shapes, first render) are paid before combat.")]
    public int prewarmCount = 3;
    [Tooltip("Frames a pre-warmed enemy stays active before it is parked in the pool.")]
    public int prewarmActiveFrames = 2;

    [Header("UI References")]
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI enemiesLeftText;
    public TextMeshProUGUI killCountText;

    private int currentWave = 0;
    private int enemiesAlive = 0;
    private int enemiesRemainingToSpawn = 0;
    private int totalKills = 0;
    private bool isWaveActive = false;
    private bool isTransitioningWave = false;
    private Coroutine waveCoroutine;
    private bool prewarmed = false;
    private Transform poolRoot;
    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> instanceToPrefab = new Dictionary<GameObject, GameObject>();

    public int CurrentWave => currentWave;
    public int EnemiesAlive => enemiesAlive;
    public int EnemiesRemainingToSpawn => enemiesRemainingToSpawn;
    public int TotalKills => totalKills;
    public bool IsWaveActive => isWaveActive;

    public UnityEvent OnWaveStart = new UnityEvent();
    public UnityEvent OnWaveEnd = new UnityEvent();
    public UnityEvent OnAllWavesCompleted = new UnityEvent();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    void Start()
    {
        if (!prewarmed && prewarmCount > 0)
        {
            StartCoroutine(PrewarmPool());
        }
    }

    /// <summary>
    /// Instantiates a few enemies at the spawn points during the opening countdown, lets them run for a couple of frames
    /// (Awake/OnEnable/Start, NavMeshAgent registration, Animator binding, physics shape creation, first draw), then parks
    /// them inactive in the pool. The first real spawn then only re-activates an already initialised object.
    /// </summary>
    private IEnumerator PrewarmPool()
    {
        prewarmed = true;
        if (enemyPrefabs == null || enemyPrefabs.Length == 0 || spawnPoints == null || spawnPoints.Length == 0) yield break;

        float startTime = Time.realtimeSinceStartup;
        VVRDiag.Mark("prewarm:instantiate");
        Transform hiddenPoint = GetSpawnPointBehindPlayer();
        var warmed = new List<GameObject>();
        for (int i = 0; i < prewarmCount; i++)
        {
            GameObject prefab = enemyPrefabs[i % enemyPrefabs.Length];
            Transform spawnPoint = hiddenPoint != null ? hiddenPoint : spawnPoints[i % spawnPoints.Length];
            if (prefab == null || spawnPoint == null) continue;

            GameObject instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            instance.name = $"{prefab.name}_{instanceToPrefab.Count}";
            instanceToPrefab[instance] = prefab;
            warmed.Add(instance);
        }
        VVRDiag.Log($"prewarm instantiated={warmed.Count} took={(Time.realtimeSinceStartup - startTime) * 1000f:F1}ms");

        for (int f = 0; f < Mathf.Max(1, prewarmActiveFrames); f++)
        {
            yield return null;
        }

        VVRDiag.Mark("prewarm:park");
        foreach (GameObject instance in warmed)
        {
            if (instance != null) ParkInPool(instance);
        }
        VVRDiag.Mark("prewarm:done");
        VVRDiag.Log($"prewarm pooled={warmed.Count} totalTime={(Time.realtimeSinceStartup - startTime) * 1000f:F1}ms");
    }

    /// <summary>
    /// The spawn point furthest behind the headset's view, so the brief pre-warm activation is not seen.
    /// </summary>
    private Transform GetSpawnPointBehindPlayer()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        Transform best = null;
        float bestDot = float.MaxValue;
        foreach (Transform point in spawnPoints)
        {
            if (point == null) continue;
            Vector3 toPoint = point.position - cam.transform.position;
            toPoint.y = 0f;
            float dot = Vector3.Dot(forward.normalized, toPoint.normalized);
            if (dot < bestDot)
            {
                bestDot = dot;
                best = point;
            }
        }
        return best;
    }

    private Transform GetPoolRoot()
    {
        if (poolRoot == null)
        {
            poolRoot = new GameObject("EnemyPool").transform;
        }
        return poolRoot;
    }

    private void ParkInPool(GameObject instance)
    {
        if (!instanceToPrefab.TryGetValue(instance, out GameObject prefab)) return;

        instance.SetActive(false);
        instance.transform.SetParent(GetPoolRoot(), false);
        if (!pools.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            pools[prefab] = queue;
        }
        if (!queue.Contains(instance)) queue.Enqueue(instance);
    }

    /// <summary>
    /// Returns a dead enemy to the pool (or destroys it if it was not created by this WaveManager).
    /// </summary>
    public void ReleaseEnemy(GameObject enemy)
    {
        if (enemy == null) return;
        if (instanceToPrefab.ContainsKey(enemy))
        {
            ParkInPool(enemy);
        }
        else
        {
            Destroy(enemy);
        }
    }

    private GameObject AcquireEnemy(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (pools.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            while (queue.Count > 0)
            {
                GameObject pooled = queue.Dequeue();
                if (pooled == null) continue;

                pooled.transform.SetParent(null, false);
                pooled.transform.SetPositionAndRotation(position, rotation);
                pooled.SetActive(true);

                EnemyHealth pooledHealth = pooled.GetComponent<EnemyHealth>();
                if (pooledHealth != null) pooledHealth.ResetHealth();
                EnemyAI pooledAI = pooled.GetComponent<EnemyAI>();
                if (pooledAI != null) pooledAI.ResetForSpawn(position, rotation);
                return pooled;
            }
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        instance.name = $"{prefab.name}_{instanceToPrefab.Count}";
        instanceToPrefab[instance] = prefab;
        return instance;
    }

    public void StartGame()
    {
        StopWaves();
        currentWave = 0;
        enemiesAlive = 0;
        enemiesRemainingToSpawn = 0;
        totalKills = 0;
        UpdateUI();
        waveCoroutine = StartCoroutine(StartNextWave());
    }

    public void StopWaves()
    {
        if (waveCoroutine != null)
        {
            StopCoroutine(waveCoroutine);
            waveCoroutine = null;
        }
        isWaveActive = false;
        isTransitioningWave = false;
        enemiesRemainingToSpawn = 0;
    }

    IEnumerator StartNextWave()
    {
        isWaveActive = false;
        isTransitioningWave = true;

        if (timeBetweenWaves > 0f)
        {
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        currentWave++;
        int enemiesToSpawn = Mathf.Max(1, Mathf.RoundToInt(baseEnemiesPerWave * Mathf.Pow(difficultyMultiplier, currentWave - 1)));
        enemiesRemainingToSpawn = enemiesToSpawn;
        isWaveActive = true;
        isTransitioningWave = false;

        OnWaveStart?.Invoke();
        UpdateUI();

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            while (maxConcurrentEnemies > 0 && enemiesAlive >= maxConcurrentEnemies)
            {
                yield return null;
            }

            SpawnEnemy();
            enemiesRemainingToSpawn = Mathf.Max(0, enemiesRemainingToSpawn - 1);
            UpdateUI();

            if (i < enemiesToSpawn - 1 && spawnStaggerDelay > 0f)
            {
                yield return new WaitForSeconds(spawnStaggerDelay);
            }
        }

        // Check if all enemies were already defeated during the staggered spawning loop
        CheckWaveCompletion();
    }

    public GameObject SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0 || spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (prefab == null || spawnPoint == null) return null;

        float spawnStart = Time.realtimeSinceStartup;
        VVRDiag.Mark($"spawn:wave{currentWave}");
        bool fromPool = pools.TryGetValue(prefab, out Queue<GameObject> queue) && queue.Count > 0;
        GameObject spawned = AcquireEnemy(prefab, spawnPoint.position, spawnPoint.rotation);
        enemiesAlive++;
        UpdateUI();
        VVRDiag.Mark($"spawn:done wave{currentWave}");
        VVRDiag.Log($"spawn wave={currentWave} enemy={spawned.name} point={spawnPoint.name} pooled={fromPool} " +
                    $"alive={enemiesAlive} took={(Time.realtimeSinceStartup - spawnStart) * 1000f:F1}ms t={Time.realtimeSinceStartup:F2}");
        return spawned;
    }

    public void EnemyDefeated()
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);
        totalKills++;
        UpdateUI();

        CheckWaveCompletion();
    }

    private void CheckWaveCompletion()
    {
        if (isWaveActive && !isTransitioningWave && enemiesRemainingToSpawn <= 0 && enemiesAlive <= 0)
        {
            isWaveActive = false;
            OnWaveEnd?.Invoke();

            if (maxWaves > 0 && currentWave >= maxWaves)
            {
                OnAllWavesCompleted?.Invoke();
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.Victory();
                }
            }
            else
            {
                waveCoroutine = StartCoroutine(StartNextWave());
            }
        }
    }

    public void UpdateUI()
    {
        int totalRemainingInWave = enemiesAlive + enemiesRemainingToSpawn;
        if (waveText != null) waveText.text = $"Wave: {currentWave}";
        if (enemiesLeftText != null) enemiesLeftText.text = $"Enemies: {totalRemainingInWave}";
        if (killCountText != null) killCountText.text = $"Kills: {totalKills}";
    }
}
