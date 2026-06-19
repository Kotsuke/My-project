using UnityEngine;

// ============================================================
//  GAME SETTINGS
//  Menyimpan semua setting game dan mengelola
//  persistensi via PlayerPrefs.
//
//  Cara pakai:
//    GameSettings.Instance.masterVolume = 0.8f;
//    GameSettings.Instance.Save();
//
//  Nanti saat AudioMixer sudah dipasang, tinggal hubungkan
//  volume values ke AudioMixer exposed parameters di
//  SettingsManager.ApplyAudio().
// ============================================================
public class GameSettings
{
    // ── Singleton ──
    private static GameSettings _instance;
    public static GameSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new GameSettings();
                _instance.Load();
            }
            return _instance;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  AUDIO SETTINGS
    // ═══════════════════════════════════════════════════════════
    public float masterVolume = 1f;   // 0f – 1f
    public float musicVolume  = 1f;   // 0f – 1f
    public float sfxVolume    = 1f;   // 0f – 1f

    // ═══════════════════════════════════════════════════════════
    //  GRAPHICS SETTINGS (Mobile)
    // ═══════════════════════════════════════════════════════════
    /// <summary>
    /// Quality level index: 0 = Low, 1 = Medium, 2 = High
    /// </summary>
    public int qualityLevel = 1;

    /// <summary>
    /// Target FPS: 0 = 30fps, 1 = 60fps
    /// </summary>
    public int targetFpsIndex = 1;

    public bool shadowEnabled      = true;
    public bool postProcessEnabled = true;

    // ═══════════════════════════════════════════════════════════
    //  GAMEPLAY SETTINGS
    // ═══════════════════════════════════════════════════════════
    public bool hapticEnabled    = true;
    public bool cameraShake      = true;
    public bool showFpsCounter   = false;

    // ═══════════════════════════════════════════════════════════
    //  PLAYERPREFS KEYS
    // ═══════════════════════════════════════════════════════════
    private const string KEY_MASTER_VOL     = "setting_master_volume";
    private const string KEY_MUSIC_VOL      = "setting_music_volume";
    private const string KEY_SFX_VOL        = "setting_sfx_volume";
    private const string KEY_QUALITY        = "setting_quality";
    private const string KEY_TARGET_FPS     = "setting_target_fps";
    private const string KEY_SHADOW         = "setting_shadow";
    private const string KEY_POSTPROCESS    = "setting_postprocess";
    private const string KEY_HAPTIC         = "setting_haptic";
    private const string KEY_CAMERA_SHAKE   = "setting_camera_shake";
    private const string KEY_SHOW_FPS       = "setting_show_fps";
    private const string KEY_INITIALIZED    = "setting_initialized";

    // ═══════════════════════════════════════════════════════════
    //  SAVE / LOAD
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Simpan semua setting ke PlayerPrefs.
    /// </summary>
    public void Save()
    {
        // Audio
        PlayerPrefs.SetFloat(KEY_MASTER_VOL, masterVolume);
        PlayerPrefs.SetFloat(KEY_MUSIC_VOL,  musicVolume);
        PlayerPrefs.SetFloat(KEY_SFX_VOL,    sfxVolume);

        // Graphics
        PlayerPrefs.SetInt(KEY_QUALITY,     qualityLevel);
        PlayerPrefs.SetInt(KEY_TARGET_FPS,  targetFpsIndex);
        PlayerPrefs.SetInt(KEY_SHADOW,      shadowEnabled      ? 1 : 0);
        PlayerPrefs.SetInt(KEY_POSTPROCESS, postProcessEnabled ? 1 : 0);

        // Gameplay
        PlayerPrefs.SetInt(KEY_HAPTIC,       hapticEnabled  ? 1 : 0);
        PlayerPrefs.SetInt(KEY_CAMERA_SHAKE, cameraShake    ? 1 : 0);
        PlayerPrefs.SetInt(KEY_SHOW_FPS,     showFpsCounter ? 1 : 0);

        PlayerPrefs.SetInt(KEY_INITIALIZED, 1);
        PlayerPrefs.Save();

        Debug.Log("[GameSettings] Settings disimpan.");
    }

    /// <summary>
    /// Muat semua setting dari PlayerPrefs.
    /// Jika belum pernah disimpan, gunakan default.
    /// </summary>
    public void Load()
    {
        // Jika belum pernah menyimpan, gunakan default values
        if (!PlayerPrefs.HasKey(KEY_INITIALIZED))
        {
            Debug.Log("[GameSettings] Belum ada setting tersimpan, menggunakan default.");
            return;
        }

        // Audio
        masterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOL, 1f);
        musicVolume  = PlayerPrefs.GetFloat(KEY_MUSIC_VOL,  1f);
        sfxVolume    = PlayerPrefs.GetFloat(KEY_SFX_VOL,    1f);

        // Graphics
        qualityLevel      = PlayerPrefs.GetInt(KEY_QUALITY,     1);
        targetFpsIndex    = PlayerPrefs.GetInt(KEY_TARGET_FPS,  1);
        shadowEnabled     = PlayerPrefs.GetInt(KEY_SHADOW,      1) == 1;
        postProcessEnabled = PlayerPrefs.GetInt(KEY_POSTPROCESS, 1) == 1;

        // Gameplay
        hapticEnabled  = PlayerPrefs.GetInt(KEY_HAPTIC,       1) == 1;
        cameraShake    = PlayerPrefs.GetInt(KEY_CAMERA_SHAKE, 1) == 1;
        showFpsCounter = PlayerPrefs.GetInt(KEY_SHOW_FPS,     0) == 1;

        Debug.Log("[GameSettings] Settings dimuat dari PlayerPrefs.");
    }

    /// <summary>
    /// Reset semua setting ke default.
    /// </summary>
    public void ResetToDefault()
    {
        masterVolume       = 1f;
        musicVolume        = 1f;
        sfxVolume          = 1f;
        qualityLevel       = 1;
        targetFpsIndex     = 1;
        shadowEnabled      = true;
        postProcessEnabled = true;
        hapticEnabled      = true;
        cameraShake        = true;
        showFpsCounter     = false;

        Save();
        Debug.Log("[GameSettings] Settings direset ke default.");
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPER
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Dapatkan nilai target FPS berdasarkan index.
    /// </summary>
    public int GetTargetFps()
    {
        switch (targetFpsIndex)
        {
            case 0: return 30;
            case 1: return 60;
            default: return 60;
        }
    }

    /// <summary>
    /// Dapatkan nama quality level untuk UI.
    /// </summary>
    public string GetQualityName()
    {
        switch (qualityLevel)
        {
            case 0: return "Low";
            case 1: return "Medium";
            case 2: return "High";
            default: return "Medium";
        }
    }
}
