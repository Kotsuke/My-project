using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  AUDIO MANAGER
//  Mengelola semua audio game: BGM (Background Music) dan
//  SFX (Sound Effects).
//
//  Fitur:
//  - Event-driven: Subscribe ke RunnerEvents (zero coupling)
//  - Pool AudioSource untuk SFX (tidak perlu Instantiate)
//  - Volume dikontrol oleh GameSettings
//  - Siap untuk AudioMixer (tinggal uncomment)
//
//  Setup:
//  1. Buat Empty GameObject "AudioManager"
//  2. Add component AudioManager
//  3. Drag audio clips ke slot di Inspector
//  4. Clip yang kosong = event itu dilewati (no error)
//
//  Nanti ganti clip placeholder dengan audio asli.
// ============================================================
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════
    //  BGM (Background Music)
    // ═══════════════════════════════════════════════════════════
    [Header("Background Music")]
    [Tooltip("Clip BGM yang diputar saat game berjalan")]
    [SerializeField] private AudioClip bgmGameplay;

    [Tooltip("Clip BGM untuk Main Menu (opsional)")]
    [SerializeField] private AudioClip bgmMenu;

    [Tooltip("Volume BGM (0-1), dikali dengan GameSettings.musicVolume")]
    [SerializeField] private float bgmBaseVolume = 0.5f;

    [Tooltip("Fade duration saat ganti BGM (detik)")]
    [SerializeField] private float bgmFadeDuration = 1f;

    // ═══════════════════════════════════════════════════════════
    //  SFX CLIPS
    // ═══════════════════════════════════════════════════════════
    [Header("Player Action SFX")]
    [Tooltip("Suara saat lompat")]
    [SerializeField] private AudioClip sfxJump;

    [Tooltip("Suara saat mulai slide")]
    [SerializeField] private AudioClip sfxSlide;

    [Tooltip("Suara saat mendarat")]
    [SerializeField] private AudioClip sfxLand;

    [Tooltip("Suara saat pindah lane")]
    [SerializeField] private AudioClip sfxLaneChange;

    [Header("Impact SFX")]
    [Tooltip("Suara saat kena halangan (stun)")]
    [SerializeField] private AudioClip sfxStun;

    [Tooltip("Suara saat mati")]
    [SerializeField] private AudioClip sfxDeath;

    [Header("Pickup SFX")]
    [Tooltip("Suara saat ambil Energy Orb")]
    [SerializeField] private AudioClip sfxEnergyOrb;

    [Tooltip("Suara saat ambil coin (nanti)")]
    [SerializeField] private AudioClip sfxCoinCollect;

    [Header("UI SFX")]
    [Tooltip("Suara klik tombol UI")]
    [SerializeField] private AudioClip sfxButtonClick;

    [Tooltip("Suara countdown (3, 2, 1)")]
    [SerializeField] private AudioClip sfxCountdown;

    [Tooltip("Suara saat GO! muncul")]
    [SerializeField] private AudioClip sfxGo;

    [Header("Ambient SFX")]
    [Tooltip("Suara deru angin/mesin saat speed boost")]
    [SerializeField] private AudioClip sfxBoostLoop;

    [Tooltip("Suara jantung berdebar saat chaser mendekat (opsional)")]
    [SerializeField] private AudioClip sfxHeartbeat;

    [Header("Footstep SFX")]
    [Tooltip("Suara langkah kaki (looping)")]
    [SerializeField] private AudioClip sfxFootstep;

    // ═══════════════════════════════════════════════════════════
    //  SFX POOL SETTINGS
    // ═══════════════════════════════════════════════════════════
    [Header("SFX Pool")]
    [Tooltip("Jumlah AudioSource yang di-pool untuk SFX")]
    [SerializeField] private int sfxPoolSize = 8;

    [Tooltip("Volume dasar SFX (0-1), dikali dengan GameSettings.sfxVolume")]
    [SerializeField] private float sfxBaseVolume = 1f;

    // ── Internal ──
    private AudioSource _bgmSource;
    private AudioSource _boostLoopSource;
    private AudioSource _footstepSource;
    private AudioSource _uiSource; // Dedicated untuk UI — tidak pernah di-pause
    private List<AudioSource> _sfxPool = new List<AudioSource>();
    private int _sfxIndex = 0;
    private Coroutine _bgmFadeCoroutine;

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
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        SetupAudioSources();
    }

    private void OnEnable()
    {
        // Subscribe ke sceneLoaded
        SceneManager.sceneLoaded        += OnSceneLoaded;

        // Subscribe ke semua RunnerEvents
        RunnerEvents.OnJumped           += OnJumped;
        RunnerEvents.OnSlideStarted     += OnSlideStarted;
        RunnerEvents.OnLanded           += OnLanded;
        RunnerEvents.OnLaneChanged      += OnLaneChanged;
        RunnerEvents.OnStunned          += OnStunned;
        RunnerEvents.OnDied             += OnDied;
        RunnerEvents.OnEnergyOrbCollected += OnEnergyOrbCollected;
        RunnerEvents.OnStateChanged     += OnStateChanged;
    }

    private void OnDisable()
    {
        // Unsubscribe dari sceneLoaded
        SceneManager.sceneLoaded        -= OnSceneLoaded;

        RunnerEvents.OnJumped           -= OnJumped;
        RunnerEvents.OnSlideStarted     -= OnSlideStarted;
        RunnerEvents.OnLanded           -= OnLanded;
        RunnerEvents.OnLaneChanged      -= OnLaneChanged;
        RunnerEvents.OnStunned          -= OnStunned;
        RunnerEvents.OnDied             -= OnDied;
        RunnerEvents.OnEnergyOrbCollected -= OnEnergyOrbCollected;
        RunnerEvents.OnStateChanged     -= OnStateChanged;
    }

    private void Start()
    {
        // Tentukan BGM awal saat game mulai pertama kali
        PlayBGMForActiveScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Jangan putar jika bukan instance utama (singleton duplicate)
        if (Instance != this) return;

        PlayBGMForActiveScene();
    }

    private void PlayBGMForActiveScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "MainMenu")
        {
            PlayBGM(bgmMenu);
        }
        else
        {
            PlayBGM(bgmGameplay);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  SETUP
    // ═══════════════════════════════════════════════════════════

    private void SetupAudioSources()
    {
        // BGM Source — dedicated, loop
        GameObject bgmGO = new GameObject("BGM_Source");
        bgmGO.transform.SetParent(transform);
        _bgmSource = bgmGO.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;
        _bgmSource.priority = 0; // Highest priority

        // Boost Loop Source — dedicated, loop
        GameObject boostGO = new GameObject("BoostLoop_Source");
        boostGO.transform.SetParent(transform);
        _boostLoopSource = boostGO.AddComponent<AudioSource>();
        _boostLoopSource.loop = true;
        _boostLoopSource.playOnAwake = false;
        _boostLoopSource.volume = 0f;

        // Footstep Source — dedicated, loop
        GameObject footstepGO = new GameObject("Footstep_Source");
        footstepGO.transform.SetParent(transform);
        _footstepSource = footstepGO.AddComponent<AudioSource>();
        _footstepSource.loop = true;
        _footstepSource.playOnAwake = false;
        _footstepSource.volume = 0f;

        // SFX Pool — beberapa AudioSource yang digunakan bergantian
        for (int i = 0; i < sfxPoolSize; i++)
        {
            GameObject sfxGO = new GameObject($"SFX_Source_{i}");
            sfxGO.transform.SetParent(transform);
            AudioSource src = sfxGO.AddComponent<AudioSource>();
            src.loop = false;
            src.playOnAwake = false;
            _sfxPool.Add(src);
        }

        // UI Source — dedicated untuk suara klik tombol, TIDAK pernah di-pause
        GameObject uiGO = new GameObject("UI_Source");
        uiGO.transform.SetParent(transform);
        _uiSource = uiGO.AddComponent<AudioSource>();
        _uiSource.loop = false;
        _uiSource.playOnAwake = false;
    }

    // ═══════════════════════════════════════════════════════════
    //  BGM CONTROL
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Putar BGM dengan fade in. Jika sudah ada BGM, crossfade.
    /// </summary>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        if (_bgmFadeCoroutine != null)
            StopCoroutine(_bgmFadeCoroutine);

        _bgmFadeCoroutine = StartCoroutine(CrossfadeBGM(clip));
    }

    /// <summary>
    /// Hentikan BGM dengan fade out.
    /// </summary>
    public void StopBGM()
    {
        if (_bgmFadeCoroutine != null)
            StopCoroutine(_bgmFadeCoroutine);

        _bgmFadeCoroutine = StartCoroutine(FadeOutBGM());
    }

    private IEnumerator CrossfadeBGM(AudioClip newClip)
    {
        float targetVolume = GetMusicVolume();

        // Fade out current BGM
        if (_bgmSource.isPlaying)
        {
            float startVol = _bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < bgmFadeDuration * 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / (bgmFadeDuration * 0.5f));
                yield return null;
            }
        }

        // Switch clip
        _bgmSource.clip = newClip;
        _bgmSource.volume = 0f;
        _bgmSource.Play();

        // Fade in new BGM
        float fadeIn = 0f;
        while (fadeIn < bgmFadeDuration * 0.5f)
        {
            fadeIn += Time.unscaledDeltaTime;
            _bgmSource.volume = Mathf.Lerp(0f, targetVolume, fadeIn / (bgmFadeDuration * 0.5f));
            yield return null;
        }

        _bgmSource.volume = targetVolume;
        _bgmFadeCoroutine = null;
    }

    private IEnumerator FadeOutBGM()
    {
        float startVol = _bgmSource.volume;
        float elapsed = 0f;

        while (elapsed < bgmFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / bgmFadeDuration);
            yield return null;
        }

        _bgmSource.Stop();
        _bgmFadeCoroutine = null;
    }

    // ═══════════════════════════════════════════════════════════
    //  SFX CONTROL
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Mainkan SFX one-shot. Menggunakan pool AudioSource.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetNextSFXSource();
        source.clip = clip;
        source.volume = GetSFXVolume() * volumeMultiplier;
        source.pitch = 1f;
        source.Play();
    }

    /// <summary>
    /// Mainkan SFX dengan random pitch (variasi natural).
    /// </summary>
    public void PlaySFXRandomPitch(AudioClip clip, float minPitch = 0.9f, float maxPitch = 1.1f, float volumeMultiplier = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetNextSFXSource();
        source.clip = clip;
        source.volume = GetSFXVolume() * volumeMultiplier;
        source.pitch = Random.Range(minPitch, maxPitch);
        source.Play();
    }

    /// <summary>
    /// Mainkan suara klik tombol UI. Panggil dari Button OnClick.
    /// Menggunakan AudioSource khusus UI agar tetap berbunyi saat pause.
    /// </summary>
    public void PlayButtonClick()
    {
        if (sfxButtonClick == null || _uiSource == null) return;
        _uiSource.PlayOneShot(sfxButtonClick, GetSFXVolume() * 0.7f);
    }

    // ── Boost Loop ──

    private void StartBoostLoop()
    {
        if (sfxBoostLoop == null || _boostLoopSource.isPlaying) return;

        _boostLoopSource.clip = sfxBoostLoop;
        _boostLoopSource.volume = GetSFXVolume() * 0.4f;
        _boostLoopSource.Play();
    }

    private void StopBoostLoop()
    {
        if (!_boostLoopSource.isPlaying) return;
        StartCoroutine(FadeOutSource(_boostLoopSource, 0.3f));
    }

    private IEnumerator FadeOutSource(AudioSource source, float duration)
    {
        float startVol = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            yield return null;
        }

        source.Stop();
        source.volume = 0f;
    }

    // ═══════════════════════════════════════════════════════════
    //  EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════

    private void OnJumped()
    {
        PlaySFXRandomPitch(sfxJump, 0.95f, 1.05f);
    }

    private void OnSlideStarted()
    {
        PlaySFX(sfxSlide);
    }

    private void OnLanded()
    {
        PlaySFX(sfxLand, 0.6f);
    }

    private void OnLaneChanged(int lane)
    {
        PlaySFXRandomPitch(sfxLaneChange, 0.9f, 1.1f, 0.5f);
    }

    private void OnStunned()
    {
        PlaySFX(sfxStun);

        // Trigger haptic feedback via SettingsManager
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.TriggerHaptic();
        }
    }

    private void OnDied()
    {
        PlaySFX(sfxDeath);

        // Fade out BGM saat mati
        StopBGM();
        StopBoostLoop();

        // Trigger haptic feedback
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.TriggerHaptic();
        }
    }

    private void OnEnergyOrbCollected()
    {
        PlaySFXRandomPitch(sfxEnergyOrb, 0.95f, 1.1f);
        StartBoostLoop();
    }

    private void StartFootstepLoop()
    {
        if (sfxFootstep == null || _footstepSource.isPlaying) return;
        _footstepSource.clip = sfxFootstep;
        _footstepSource.volume = GetSFXVolume() * 0.5f;
        _footstepSource.Play();
    }

    private void StopFootstepLoop()
    {
        if (!_footstepSource.isPlaying) return;
        _footstepSource.Stop();
    }

    private void OnStateChanged(RunnerState prev, RunnerState next)
    {
        if (next == RunnerState.Running)
        {
            StartFootstepLoop();
        }
        else
        {
            StopFootstepLoop();
        }

        // Stop boost loop saat state berubah dari boosted ke normal
        // (Ini dihandle lebih baik via timer di Update, tapi ini fallback)
        if (next == RunnerState.Stunned || next == RunnerState.Dying)
        {
            StopBoostLoop();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  VOLUME HELPERS
    // ═══════════════════════════════════════════════════════════

    private float GetMusicVolume()
    {
        float settings = GameSettings.Instance.musicVolume * GameSettings.Instance.masterVolume;
        return bgmBaseVolume * settings;
    }

    private float GetSFXVolume()
    {
        float settings = GameSettings.Instance.sfxVolume * GameSettings.Instance.masterVolume;
        return sfxBaseVolume * settings;
    }

    /// <summary>
    /// Dipanggil oleh SettingsManager saat volume berubah.
    /// Update volume BGM dan boost loop secara realtime.
    /// </summary>
    public void RefreshVolumes()
    {
        if (_bgmSource != null && _bgmSource.isPlaying)
        {
            _bgmSource.volume = GetMusicVolume();
        }

        if (_boostLoopSource != null && _boostLoopSource.isPlaying)
        {
            _boostLoopSource.volume = GetSFXVolume() * 0.4f;
        }

        if (_footstepSource != null && _footstepSource.isPlaying)
        {
            _footstepSource.volume = GetSFXVolume() * 0.5f;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PAUSE / RESUME SFX
    //  BGM tetap jalan, hanya SFX (footstep, boost, pool) yang di-pause.
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Pause semua SFX (footstep, boost loop, SFX pool).
    /// BGM tetap terdengar.
    /// Dipanggil dari RunnerGameManager.PauseGame().
    /// </summary>
    public void PauseSFX()
    {
        if (_footstepSource != null && _footstepSource.isPlaying)
            _footstepSource.Pause();

        if (_boostLoopSource != null && _boostLoopSource.isPlaying)
            _boostLoopSource.Pause();

        // Pause semua SFX one-shot yang sedang bermain
        foreach (AudioSource src in _sfxPool)
        {
            if (src != null && src.isPlaying)
                src.Pause();
        }
    }

    /// <summary>
    /// Resume semua SFX yang di-pause.
    /// Dipanggil dari RunnerGameManager.ResumeGame().
    /// </summary>
    public void ResumeSFX()
    {
        if (_footstepSource != null)
            _footstepSource.UnPause();

        if (_boostLoopSource != null)
            _boostLoopSource.UnPause();

        foreach (AudioSource src in _sfxPool)
        {
            if (src != null)
                src.UnPause();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  INTERNAL HELPERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Round-robin: ambil AudioSource berikutnya dari pool.
    /// </summary>
    private AudioSource GetNextSFXSource()
    {
        AudioSource source = _sfxPool[_sfxIndex];
        _sfxIndex = (_sfxIndex + 1) % _sfxPool.Count;
        return source;
    }
}
