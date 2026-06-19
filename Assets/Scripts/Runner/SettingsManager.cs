using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ============================================================
//  SETTINGS MANAGER
//  Singleton MonoBehaviour yang menerapkan GameSettings
//  ke sistem Unity (Graphics, Audio, dll).
//
//  Letakkan di scene pertama (atau pakai DontDestroyOnLoad).
//
//  Untuk AudioMixer nanti:
//  1. Buat AudioMixer asset
//  2. Expose parameter: "MasterVolume", "MusicVolume", "SFXVolume"
//  3. Assign audioMixer field di Inspector
//  4. Uncomment kode AudioMixer di ApplyAudio()
// ============================================================
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("References (Opsional — isi saat sudah ada)")]
    // [Tooltip("Drag AudioMixer ke sini setelah membuat AudioMixer asset")]
    // public UnityEngine.Audio.AudioMixer audioMixer;

    [Tooltip("Volume Profile untuk Post-Processing (biasanya DefaultVolumeProfile)")]
    [SerializeField] private VolumeProfile volumeProfile;

    [Header("FPS Counter")]
    [Tooltip("GameObject Text/TMP di canvas yang menampilkan FPS")]
    [SerializeField] private GameObject fpsCounterUI;

    // ── FPS Counter internal ──
    private float _fpsTimer;
    private int   _fpsFrameCount;
    private TMPro.TextMeshProUGUI _fpsText;

    // ═══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Terapkan semua setting saat game dimulai
        ApplyAllSettings();
    }

    /// <summary>
    /// Terapkan semua setting sekaligus (dipanggil saat load dan saat Apply di panel).
    /// </summary>
    public void ApplyAllSettings()
    {
        ApplyAudio();
        ApplyGraphics();
        ApplyGameplay();
    }

    // ═══════════════════════════════════════════════════════════
    //  AUDIO
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Terapkan setting audio ke sistem Unity.
    /// Saat ini menggunakan AudioListener.volume.
    /// Nanti saat AudioMixer sudah ada, ganti ke audioMixer.SetFloat().
    /// </summary>
    public void ApplyAudio()
    {
        GameSettings s = GameSettings.Instance;

        // Untuk saat ini: atur global volume via AudioListener
        // Master volume mengontrol semua suara
        AudioListener.volume = s.masterVolume;

        // Sinkronkan volume ke AudioManager (BGM dan SFX loop)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.RefreshVolumes();
        }

        // ────────────────────────────────────────────────────
        // NANTI: Setelah AudioMixer dipasang, uncomment ini:
        // ────────────────────────────────────────────────────
        // if (audioMixer != null)
        // {
        //     // AudioMixer menggunakan skala dB (-80 hingga 0)
        //     // Konversi dari linear (0–1) ke dB
        //     audioMixer.SetFloat("MasterVolume", LinearToDecibel(s.masterVolume));
        //     audioMixer.SetFloat("MusicVolume",  LinearToDecibel(s.musicVolume));
        //     audioMixer.SetFloat("SFXVolume",    LinearToDecibel(s.sfxVolume));
        // }

        Debug.Log($"[SettingsManager] Audio diterapkan — Master: {s.masterVolume:F2}");
    }

    // ═══════════════════════════════════════════════════════════
    //  GRAPHICS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Terapkan setting grafis ke sistem Unity.
    /// </summary>
    public void ApplyGraphics()
    {
        GameSettings s = GameSettings.Instance;

        // Quality Level
        QualitySettings.SetQualityLevel(s.qualityLevel, true);

        // Target FPS
        Application.targetFrameRate = s.GetTargetFps();

        // VSync — matikan di mobile agar targetFrameRate bekerja
        QualitySettings.vSyncCount = 0;

        // Shadow
        if (s.shadowEnabled)
        {
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
        }
        else
        {
            QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
        }

        // Post-Processing
        ApplyPostProcessing(s.postProcessEnabled);

        Debug.Log($"[SettingsManager] Graphics diterapkan — Quality: {s.GetQualityName()}, FPS: {s.GetTargetFps()}, Shadow: {s.shadowEnabled}, PP: {s.postProcessEnabled}");
    }

    /// <summary>
    /// Toggle post-processing on/off.
    /// Mencari semua Volume di scene dan mengaktifkan/menonaktifkannya.
    /// </summary>
    private void ApplyPostProcessing(bool enabled)
    {
        // Cara 1: Cari semua Volume component di scene
        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        foreach (Volume vol in volumes)
        {
            vol.enabled = enabled;
        }

        // Cara 2: Via URP Renderer Data (jika perlu kontrol lebih halus)
        // UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        // if (urpAsset != null) { ... }
    }

    // ═══════════════════════════════════════════════════════════
    //  GAMEPLAY
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Terapkan setting gameplay.
    /// </summary>
    public void ApplyGameplay()
    {
        GameSettings s = GameSettings.Instance;

        // FPS Counter
        if (fpsCounterUI != null)
        {
            fpsCounterUI.SetActive(s.showFpsCounter);
            if (s.showFpsCounter && _fpsText == null)
            {
                _fpsText = fpsCounterUI.GetComponent<TMPro.TextMeshProUGUI>();
            }
        }

        Debug.Log($"[SettingsManager] Gameplay diterapkan — Haptic: {s.hapticEnabled}, CamShake: {s.cameraShake}, FPS Counter: {s.showFpsCounter}");
    }

    // ═══════════════════════════════════════════════════════════
    //  FPS COUNTER UPDATE
    // ═══════════════════════════════════════════════════════════
    private void Update()
    {
        if (!GameSettings.Instance.showFpsCounter || _fpsText == null) return;

        _fpsFrameCount++;
        _fpsTimer += Time.unscaledDeltaTime;

        if (_fpsTimer >= 0.5f)
        {
            float fps = _fpsFrameCount / _fpsTimer;
            _fpsText.text = $"FPS: {fps:F0}";
            _fpsTimer = 0f;
            _fpsFrameCount = 0;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Trigger haptic feedback (getaran) jika setting mengizinkan.
    /// Panggil dari script lain: SettingsManager.Instance.TriggerHaptic()
    /// </summary>
    public void TriggerHaptic()
    {
        if (!GameSettings.Instance.hapticEnabled) return;

#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    /// <summary>
    /// Cek apakah camera shake diizinkan oleh setting.
    /// Panggil dari CameraShake script: if (SettingsManager.Instance.IsCameraShakeEnabled()) ...
    /// </summary>
    public bool IsCameraShakeEnabled()
    {
        return GameSettings.Instance.cameraShake;
    }

    // ═══════════════════════════════════════════════════════════
    //  UTILITY
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Konversi volume linear (0–1) ke decibel (-80 – 0).
    /// Digunakan untuk AudioMixer nanti.
    /// </summary>
    public static float LinearToDecibel(float linear)
    {
        if (linear <= 0f) return -80f;
        return Mathf.Log10(linear) * 20f;
    }

    /// <summary>
    /// Konversi decibel (-80 – 0) ke volume linear (0–1).
    /// </summary>
    public static float DecibelToLinear(float dB)
    {
        return Mathf.Pow(10f, dB / 20f);
    }
}
