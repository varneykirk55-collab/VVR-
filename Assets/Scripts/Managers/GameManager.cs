using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public PlayerHealth playerHealth;
    public GameObject gameOverCanvas;
    public GameObject victoryCanvas;
    public WaveManager waveManager;
    [Tooltip("Seconds after Game Over before the arena reloads automatically (there is no in-VR restart button). 0 = never.")]
    public float autoRestartDelay = 8f;

    private bool isGameOver = false;
    public bool IsGameOver => isGameOver;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    void Start()
    {
        if (gameOverCanvas != null) gameOverCanvas.SetActive(false);
        if (victoryCanvas != null) victoryCanvas.SetActive(false);

        if (playerHealth == null)
        {
            playerHealth = PlayerHealth.Instance != null
                ? PlayerHealth.Instance
                : Object.FindAnyObjectByType<PlayerHealth>();
        }

        if (waveManager == null)
        {
            waveManager = WaveManager.Instance != null
                ? WaveManager.Instance
                : Object.FindAnyObjectByType<WaveManager>();
        }

        if (playerHealth != null)
        {
            playerHealth.OnDeath.RemoveListener(GameOver);
            playerHealth.OnDeath.AddListener(GameOver);
        }

        // Start the first wave
        if (waveManager != null)
        {
            waveManager.StartGame();
        }
    }

    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (waveManager != null)
        {
            waveManager.StopWaves();
        }

        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
        }

        VVRDiag.Log($"game over wave={(waveManager != null ? waveManager.CurrentWave : -1)} kills={(waveManager != null ? waveManager.TotalKills : -1)}");
        if (autoRestartDelay > 0f && Application.isPlaying)
        {
            Invoke(nameof(RestartGame), autoRestartDelay);
        }
    }

    public void Victory()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (waveManager != null)
        {
            waveManager.StopWaves();
        }

        if (victoryCanvas != null)
        {
            victoryCanvas.SetActive(true);
        }

        VVRDiag.Log("victory");
    }

    public void RestartGame()
    {
        isGameOver = false;
        VVRDiag.Log("restart scene");
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
        }
        else if (!string.IsNullOrEmpty(activeScene.name))
        {
            SceneManager.LoadScene(activeScene.name);
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
