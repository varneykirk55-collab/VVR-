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

        Debug.Log($"VVR_DIAG spawn begin wave={currentWave} point={spawnPoint.name} t={Time.realtimeSinceStartup:F2}");
        GameObject spawned = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        Debug.Log($"VVR_DIAG spawn instantiated t={Time.realtimeSinceStartup:F2}");
        enemiesAlive++;
        UpdateUI();
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
