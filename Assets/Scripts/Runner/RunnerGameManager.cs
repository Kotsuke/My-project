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

    [Header("Settings")]
    [Tooltip("Jeda waktu sebelum memunculkan UI Game Over (agar animasi mati selesai)")]
    [SerializeField] private float gameOverDelay = 1.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Pastikan panel UI mati saat game mulai
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
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

    // ── Fungsi Public untuk Tombol UI ──

    /// <summary>
    /// Memuat ulang scene/level saat ini (Respawn)
    /// </summary>
    public void RestartLevel()
    {
        Debug.Log("[RunnerGameManager] Merestart level...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Memuat level berikutnya berdasarkan Build Index
    /// </summary>
    public void LoadNextLevel()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        
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
}
