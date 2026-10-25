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
    }

    public void RestartGame()
    {
        isGameOver = false;
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
