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
    public int baseEnemiesPerWave = 3;
    public float difficultyMultiplier = 1.5f;

    [Header("UI References")]
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI enemiesLeftText;
    public TextMeshProUGUI killCountText;

    private int currentWave = 0;
    private int enemiesAlive = 0;
    private int totalKills = 0;
    private bool isWaveActive = false;

    public UnityEvent OnWaveStart;
    public UnityEvent OnWaveEnd;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartGame()
    {
        currentWave = 0;
        totalKills = 0;
        UpdateUI();
        StartCoroutine(StartNextWave());
    }

    IEnumerator StartNextWave()
    {
        isWaveActive = false;
        yield return new WaitForSeconds(timeBetweenWaves);

        currentWave++;
        int enemiesToSpawn = Mathf.RoundToInt(baseEnemiesPerWave * Mathf.Pow(difficultyMultiplier, currentWave - 1));

        OnWaveStart?.Invoke();
        UpdateUI();

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(1f); // Stagger spawning
        }

        isWaveActive = true;
    }

    void SpawnEnemy()
    {
        if (enemyPrefabs.Length == 0 || spawnPoints.Length == 0) return;

        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        enemiesAlive++;
        UpdateUI();
    }

    public void EnemyDefeated()
    {
        enemiesAlive--;
        totalKills++;
        UpdateUI();

        if (isWaveActive && enemiesAlive <= 0)
        {
            OnWaveEnd?.Invoke();
            StartCoroutine(StartNextWave());
        }
    }

    void UpdateUI()
    {
        if (waveText != null) waveText.text = $"Wave: {currentWave}";
        if (enemiesLeftText != null) enemiesLeftText.text = $"Enemies: {enemiesAlive}";
        if (killCountText != null) killCountText.text = $"Kills: {totalKills}";
    }
}
