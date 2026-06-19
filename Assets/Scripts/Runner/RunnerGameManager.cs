using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  RUNNER GAME MANAGER
//  Mengelola state permainan (Restart, Victory, UI panels).
// ============================================================
public class RunnerGameManager : MonoBehaviour
{
    public static RunnerGameManager Instance { get; private set; }

    [Header("UI Panels")]
    [Tooltip("Panel UI yang muncul saat player mati (Game Over)")]
    [SerializeField] private GameObject gameOverPanel;

    [Tooltip("Panel UI yang muncul saat player mencapai garis finish (Victory)")]
    [SerializeField] private GameObject victoryPanel;

    [Tooltip("Panel UI yang muncul saat game dijeda (Pause)")]
    [SerializeField] private GameObject pausePanel;

    [Tooltip("Panel UI Settings")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings")]
    [Tooltip("Jeda waktu sebelum memunculkan UI Game Over (agar animasi mati selesai)")]
    [SerializeField] private float gameOverDelay = 1.5f;

    public bool IsPaused { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Reset time scale ke normal (jika scene sebelumnya ter-pause saat reload)
        Time.timeScale = 1f;

        // Pastikan panel UI mati saat game mulai
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void OnEnable()
    {
        RunnerEvents.OnDied += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        RunnerEvents.OnDied -= HandlePlayerDeath;
    }

    // ── Respon Terhadap Kematian Player ──
    private void HandlePlayerDeath()
    {
        Debug.Log("[RunnerGameManager] Player mati! Memulai hitung mundur Game Over...");
        StartCoroutine(ShowGameOverRoutine());
    }

    private IEnumerator ShowGameOverRoutine()
    {
        yield return new WaitForSeconds(gameOverDelay);
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            Debug.Log("[RunnerGameManager] Menampilkan Panel Game Over.");
        }
        else
        {
            // Jika user belum membuat UI, langsung restart otomatis agar game tidak terhenti
            Debug.LogWarning("[RunnerGameManager] gameOverPanel tidak di-assign! Restarting otomatis...");
            RestartLevel();
        }
    }

    // ── Respon Terhadap Kemenangan (Finish Line) ──
    public void CompleteLevel()
    {
        Debug.Log("[RunnerGameManager] Player berhasil menyentuh garis finish!");
        
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[RunnerGameManager] victoryPanel tidak di-assign! Memulai ulang level...");
            RestartLevel();
        }
    }

    // ── Jeda Permainan (Pause) ──

    /// <summary>
    /// Jeda permainan (Pause)
    /// </summary>
    public void PauseGame()
    {
        if (IsPaused) return;

        IsPaused = true;
        Time.timeScale = 0f; // Hentikan waktu permainan (animasi, fisika, update)
        
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
        Debug.Log("[RunnerGameManager] Game Dijeda (Paused).");
    }

    /// <summary>
    /// Lanjutkan permainan (Resume)
    /// </summary>
    public void ResumeGame()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f; // Kembalikan waktu permainan ke normal
        
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        Debug.Log("[RunnerGameManager] Game Dilanjutkan (Resumed).");
    }

    // ── Settings Panel ──

    /// <summary>
    /// Buka panel settings (jeda game juga)
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            // Sembunyikan pause panel jika terbuka
            if (pausePanel != null) pausePanel.SetActive(false);

            settingsPanel.SetActive(true);
            IsPaused = true;
            Time.timeScale = 0f;
            Debug.Log("[RunnerGameManager] Settings Panel dibuka.");
        }
        else
        {
            Debug.LogWarning("[RunnerGameManager] settingsPanel belum di-assign!");
        }
    }

    /// <summary>
    /// Tutup panel settings (lanjutkan game)
    /// </summary>
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        IsPaused = false;
        Time.timeScale = 1f;
        Debug.Log("[RunnerGameManager] Settings Panel ditutup.");
    }

    // ── Fungsi Public untuk Tombol UI ──

    /// <summary>
    /// Memuat ulang scene/level saat ini (Respawn)
    /// </summary>
    public void RestartLevel()
    {
        Debug.Log("[RunnerGameManager] Merestart level...");
        Time.timeScale = 1f; // Pastikan waktu normal sebelum memuat scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Memuat level berikutnya berdasarkan Build Index
    /// </summary>
    public void LoadNextLevel()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        Time.timeScale = 1f; // Pastikan waktu normal sebelum memuat scene
        
        // Periksa apakah scene berikutnya ada di Build Settings
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log($"[RunnerGameManager] Memuat level berikutnya (Index: {nextSceneIndex})...");
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.Log("[RunnerGameManager] Tidak ada level berikutnya! Kembali ke Main Menu (Index 0)...");
            SceneManager.LoadScene(0); // Biasanya Main Menu berada di index 0
        }
    }

    /// <summary>
    /// Kembali ke Main Menu
    /// </summary>
    public void GoToMainMenu()
    {
        Debug.Log("[RunnerGameManager] Kembali ke Main Menu...");
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// Keluar dari game
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("[RunnerGameManager] Keluar dari game...");
        Time.timeScale = 1f;
        Application.Quit();
    }
}
