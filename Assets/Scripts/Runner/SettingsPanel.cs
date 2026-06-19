using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  SETTINGS PANEL
//  Script UI untuk panel Settings.
//
//  Cara setup di Unity:
//  1. Buat Canvas → Panel (Settings)
//  2. Buat 3 Tab Button: Audio, Graphics, Gameplay
//  3. Buat 3 Content Panel (satu per tab)
//  4. Isi setiap content panel dengan Slider/Toggle/Dropdown
//  5. Drag semua UI elements ke field di Inspector
//  6. Tombol Apply, Reset Default, dan Back
//
//  Tab system menggunakan GameObject.SetActive() untuk
//  menampilkan/sembunyikan content panel sesuai tab aktif.
// ============================================================
public class SettingsPanel : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    //  TAB SYSTEM
    // ═══════════════════════════════════════════════════════════
    [Header("Tab Panels")]
    [Tooltip("Panel content untuk tab Audio")]
    [SerializeField] private GameObject audioTab;

    [Tooltip("Panel content untuk tab Graphics")]
    [SerializeField] private GameObject graphicsTab;

    [Tooltip("Panel content untuk tab Gameplay")]
    [SerializeField] private GameObject gameplayTab;

    [Header("Tab Buttons")]
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button graphicsTabButton;
    [SerializeField] private Button gameplayTabButton;

    [Header("Tab Button Colors")]
    [Tooltip("Warna tab yang aktif")]
    [SerializeField] private Color activeTabColor   = new Color(0.3f, 0.7f, 1f, 1f);
    [Tooltip("Warna tab yang tidak aktif")]
    [SerializeField] private Color inactiveTabColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    // ═══════════════════════════════════════════════════════════
    //  AUDIO UI ELEMENTS
    // ═══════════════════════════════════════════════════════════
    [Header("Audio Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [SerializeField] private TextMeshProUGUI masterVolumeLabel;
    [SerializeField] private TextMeshProUGUI musicVolumeLabel;
    [SerializeField] private TextMeshProUGUI sfxVolumeLabel;

    // ═══════════════════════════════════════════════════════════
    //  GRAPHICS UI ELEMENTS
    // ═══════════════════════════════════════════════════════════
    [Header("Graphics Settings")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown fpsDropdown;
    [SerializeField] private Toggle       shadowToggle;
    [SerializeField] private Toggle       postProcessToggle;

    // ═══════════════════════════════════════════════════════════
    //  GAMEPLAY UI ELEMENTS
    // ═══════════════════════════════════════════════════════════
    [Header("Gameplay Settings")]
    [SerializeField] private Toggle hapticToggle;
    [SerializeField] private Toggle cameraShakeToggle;
    [SerializeField] private Toggle showFpsToggle;

    // ═══════════════════════════════════════════════════════════
    //  BUTTONS
    // ═══════════════════════════════════════════════════════════
    [Header("Buttons")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button backButton;

    // ═══════════════════════════════════════════════════════════
    //  ANIMATION (Opsional)
    // ═══════════════════════════════════════════════════════════
    [Header("Panel Animation")]
    [Tooltip("Animator untuk animasi buka/tutup panel (opsional)")]
    [SerializeField] private Animator panelAnimator;

    // ── Internal ──
    private GameSettings _settings;

    // ═══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    private void OnEnable()
    {
        _settings = GameSettings.Instance;
        LoadSettingsToUI();
        SwitchTab(0); // Default buka tab Audio

        // ── Register Listeners ──
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    // ═══════════════════════════════════════════════════════════
    //  REGISTER / UNREGISTER UI LISTENERS
    // ═══════════════════════════════════════════════════════════

    private void RegisterListeners()
    {
        // Sliders — update label secara realtime
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        // Tab buttons
        if (audioTabButton != null)
            audioTabButton.onClick.AddListener(() => SwitchTab(0));
        if (graphicsTabButton != null)
            graphicsTabButton.onClick.AddListener(() => SwitchTab(1));
        if (gameplayTabButton != null)
            gameplayTabButton.onClick.AddListener(() => SwitchTab(2));

        // Action buttons
        if (applyButton != null)
            applyButton.onClick.AddListener(OnApplyPressed);
        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetPressed);
        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);
    }

    private void UnregisterListeners()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);

        if (audioTabButton != null)
            audioTabButton.onClick.RemoveAllListeners();
        if (graphicsTabButton != null)
            graphicsTabButton.onClick.RemoveAllListeners();
        if (gameplayTabButton != null)
            gameplayTabButton.onClick.RemoveAllListeners();

        if (applyButton != null)
            applyButton.onClick.RemoveAllListeners();
        if (resetButton != null)
            resetButton.onClick.RemoveAllListeners();
        if (backButton != null)
            backButton.onClick.RemoveAllListeners();
    }

    // ═══════════════════════════════════════════════════════════
    //  TAB SWITCHING
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Ganti tab yang aktif: 0 = Audio, 1 = Graphics, 2 = Gameplay
    /// </summary>
    public void SwitchTab(int tabIndex)
    {
        // Hide semua tab content
        if (audioTab != null)    audioTab.SetActive(false);
        if (graphicsTab != null) graphicsTab.SetActive(false);
        if (gameplayTab != null) gameplayTab.SetActive(false);

        // Reset warna semua tab button
        SetTabButtonColor(audioTabButton,    inactiveTabColor);
        SetTabButtonColor(graphicsTabButton, inactiveTabColor);
        SetTabButtonColor(gameplayTabButton, inactiveTabColor);

        // Tampilkan tab yang dipilih
        switch (tabIndex)
        {
            case 0:
                if (audioTab != null) audioTab.SetActive(true);
                SetTabButtonColor(audioTabButton, activeTabColor);
                break;
            case 1:
                if (graphicsTab != null) graphicsTab.SetActive(true);
                SetTabButtonColor(graphicsTabButton, activeTabColor);
                break;
            case 2:
                if (gameplayTab != null) gameplayTab.SetActive(true);
                SetTabButtonColor(gameplayTabButton, activeTabColor);
                break;
        }
    }

    private void SetTabButtonColor(Button button, Color color)
    {
        if (button == null) return;
        Image img = button.GetComponent<Image>();
        if (img != null)
        {
            img.color = color;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  LOAD SETTINGS → UI
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Muat nilai setting ke semua UI elements.
    /// </summary>
    private void LoadSettingsToUI()
    {
        // Audio
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = _settings.masterVolume;
            UpdateVolumeLabel(masterVolumeLabel, _settings.masterVolume);
        }
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = _settings.musicVolume;
            UpdateVolumeLabel(musicVolumeLabel, _settings.musicVolume);
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = _settings.sfxVolume;
            UpdateVolumeLabel(sfxVolumeLabel, _settings.sfxVolume);
        }

        // Graphics
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string> { "Low", "Medium", "High" });
            qualityDropdown.value = _settings.qualityLevel;
        }
        if (fpsDropdown != null)
        {
            fpsDropdown.ClearOptions();
            fpsDropdown.AddOptions(new System.Collections.Generic.List<string> { "30 FPS", "60 FPS" });
            fpsDropdown.value = _settings.targetFpsIndex;
        }
        if (shadowToggle != null)
            shadowToggle.isOn = _settings.shadowEnabled;
        if (postProcessToggle != null)
            postProcessToggle.isOn = _settings.postProcessEnabled;

        // Gameplay
        if (hapticToggle != null)
            hapticToggle.isOn = _settings.hapticEnabled;
        if (cameraShakeToggle != null)
            cameraShakeToggle.isOn = _settings.cameraShake;
        if (showFpsToggle != null)
            showFpsToggle.isOn = _settings.showFpsCounter;
    }

    // ═══════════════════════════════════════════════════════════
    //  READ UI → SETTINGS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Baca semua nilai dari UI elements dan simpan ke GameSettings.
    /// </summary>
    private void ReadUIToSettings()
    {
        // Audio
        if (masterVolumeSlider != null)
            _settings.masterVolume = masterVolumeSlider.value;
        if (musicVolumeSlider != null)
            _settings.musicVolume = musicVolumeSlider.value;
        if (sfxVolumeSlider != null)
            _settings.sfxVolume = sfxVolumeSlider.value;

        // Graphics
        if (qualityDropdown != null)
            _settings.qualityLevel = qualityDropdown.value;
        if (fpsDropdown != null)
            _settings.targetFpsIndex = fpsDropdown.value;
        if (shadowToggle != null)
            _settings.shadowEnabled = shadowToggle.isOn;
        if (postProcessToggle != null)
            _settings.postProcessEnabled = postProcessToggle.isOn;

        // Gameplay
        if (hapticToggle != null)
            _settings.hapticEnabled = hapticToggle.isOn;
        if (cameraShakeToggle != null)
            _settings.cameraShake = cameraShakeToggle.isOn;
        if (showFpsToggle != null)
            _settings.showFpsCounter = showFpsToggle.isOn;
    }

    // ═══════════════════════════════════════════════════════════
    //  SLIDER CALLBACKS (untuk update label secara realtime)
    // ═══════════════════════════════════════════════════════════

    private void OnMasterVolumeChanged(float value)
    {
        UpdateVolumeLabel(masterVolumeLabel, value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        UpdateVolumeLabel(musicVolumeLabel, value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        UpdateVolumeLabel(sfxVolumeLabel, value);
    }

    private void UpdateVolumeLabel(TextMeshProUGUI label, float value)
    {
        if (label != null)
        {
            label.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  BUTTON CALLBACKS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Tombol Apply: simpan setting dan terapkan.
    /// </summary>
    public void OnApplyPressed()
    {
        ReadUIToSettings();
        _settings.Save();

        // Terapkan setting ke sistem Unity
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.ApplyAllSettings();
        }

        Debug.Log("[SettingsPanel] Settings diterapkan dan disimpan.");
    }

    /// <summary>
    /// Tombol Reset: kembalikan ke default.
    /// </summary>
    public void OnResetPressed()
    {
        _settings.ResetToDefault();
        LoadSettingsToUI();

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.ApplyAllSettings();
        }

        Debug.Log("[SettingsPanel] Settings direset ke default.");
    }

    /// <summary>
    /// Tombol Back: tutup panel settings.
    /// </summary>
    public void OnBackPressed()
    {
        // Simpan otomatis saat keluar
        OnApplyPressed();

        // Tutup panel settings via GameManager
        if (RunnerGameManager.Instance != null)
        {
            RunnerGameManager.Instance.CloseSettings();
        }
        else
        {
            // Fallback: sembunyikan panel ini langsung
            gameObject.SetActive(false);
        }
    }
}
