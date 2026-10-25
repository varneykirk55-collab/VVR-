using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public PlayerHealth playerHealth;
    public GameObject gameOverCanvas;
    public GameObject victoryCanvas; // If there's an end condition
    public WaveManager waveManager;

    private bool isGameOver = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (gameOverCanvas != null) gameOverCanvas.SetActive(false);
        if (victoryCanvas != null) victoryCanvas.SetActive(false);

        if (playerHealth != null)
        {
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

        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
        }

        // Disable player input or mechanics if necessary
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
