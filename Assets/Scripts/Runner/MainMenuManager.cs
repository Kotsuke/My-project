using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// ============================================================
//  MAIN MENU MANAGER
//  Mengelola navigasi di Main Menu:
//  - Buka/tutup Settings Panel
//  - Play button → buka Level Select Panel
//  - Level buttons → load scene level yang dipilih
//  - Quit button → keluar aplikasi
// ============================================================
public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject levelSelectPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button settingButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    [Header("Level Select Buttons")]
    [SerializeField] private Button level1Button;
    [SerializeField] private Button level2Button;
    [SerializeField] private Button level3Button;
    [SerializeField] private Button levelBackButton;

    [Header("Level Scene Names")]
    [Tooltip("Nama scene untuk setiap level (sesuai Build Settings)")]
    [SerializeField] private string level1Scene = "level1 1";
    [SerializeField] private string level2Scene = "level1 2";
    [SerializeField] private string level3Scene = "level1 3";

    private void OnEnable()
    {
        // Main menu buttons
        if (settingButton != null)
            settingButton.onClick.AddListener(OpenSettings);
        if (playButton != null)
            playButton.onClick.AddListener(OpenLevelSelect);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitPressed);

        // Level select buttons
        if (level1Button != null)
            level1Button.onClick.AddListener(() => LoadLevel(level1Scene));
        if (level2Button != null)
            level2Button.onClick.AddListener(() => LoadLevel(level2Scene));
        if (level3Button != null)
            level3Button.onClick.AddListener(() => LoadLevel(level3Scene));
        if (levelBackButton != null)
            levelBackButton.onClick.AddListener(CloseLevelSelect);
    }

    private void OnDisable()
    {
        if (settingButton != null)
            settingButton.onClick.RemoveListener(OpenSettings);
        if (playButton != null)
            playButton.onClick.RemoveListener(OpenLevelSelect);
        if (quitButton != null)
            quitButton.onClick.RemoveListener(OnQuitPressed);

        if (level1Button != null)
            level1Button.onClick.RemoveAllListeners();
        if (level2Button != null)
            level2Button.onClick.RemoveAllListeners();
        if (level3Button != null)
            level3Button.onClick.RemoveAllListeners();
        if (levelBackButton != null)
            levelBackButton.onClick.RemoveListener(CloseLevelSelect);
    }

    // ═══════════════════════════════════════════════════════════
    //  SETTINGS PANEL
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Buka panel settings.
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            Debug.Log("[MainMenuManager] Settings panel dibuka.");
        }
    }

    /// <summary>
    /// Tutup panel settings.
    /// </summary>
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            Debug.Log("[MainMenuManager] Settings panel ditutup.");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  LEVEL SELECT PANEL
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Buka panel level select.
    /// </summary>
    public void OpenLevelSelect()
    {
        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(true);
            Debug.Log("[MainMenuManager] Level select panel dibuka.");
        }
    }

    /// <summary>
    /// Tutup panel level select.
    /// </summary>
    public void CloseLevelSelect()
    {
        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(false);
            Debug.Log("[MainMenuManager] Level select panel ditutup.");
        }
    }

    /// <summary>
    /// Load scene level yang dipilih.
    /// </summary>
    private void LoadLevel(string sceneName)
    {
        Debug.Log("[MainMenuManager] Loading level: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }

    // ═══════════════════════════════════════════════════════════
    //  QUIT
    // ═══════════════════════════════════════════════════════════

    private void OnQuitPressed()
    {
        Debug.Log("[MainMenuManager] Quit application.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
