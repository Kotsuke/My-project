using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ============================================================
//  CAMERA EFFECTS
//  Efek kamera untuk meningkatkan game feel:
//  - Screen Shake (stun, death, landing)
//  - FOV Punch (speed boost)
//  - Vignette Pulse (stun/danger)
//  - Chromatic Aberration (stun impact)
//
//  Setup:
//  1. Pasang script ini di Main Camera
//  2. (Opsional) Assign Volume Profile untuk post-processing effects
//  3. Efek shake menghormati GameSettings.cameraShake
//
//  Semua efek menggunakan Time.unscaledDeltaTime agar
//  tetap berjalan saat slow motion.
// ============================================================
public class CameraEffects : MonoBehaviour
{
    public static CameraEffects Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════
    //  SCREEN SHAKE
    // ═══════════════════════════════════════════════════════════
    [Header("Screen Shake")]
    [Tooltip("Intensitas shake saat stun")]
    [SerializeField] private float stunShakeIntensity = 0.3f;
    [Tooltip("Durasi shake saat stun (detik)")]
    [SerializeField] private float stunShakeDuration = 0.3f;

    [Tooltip("Intensitas shake saat death")]
    [SerializeField] private float deathShakeIntensity = 0.5f;
    [Tooltip("Durasi shake saat death (detik)")]
    [SerializeField] private float deathShakeDuration = 0.5f;

    [Tooltip("Intensitas shake saat landing")]
    [SerializeField] private float landShakeIntensity = 0.08f;
    [Tooltip("Durasi shake saat landing (detik)")]
    [SerializeField] private float landShakeDuration = 0.15f;

    // ═══════════════════════════════════════════════════════════
    //  FOV EFFECT
    // ═══════════════════════════════════════════════════════════
    [Header("FOV Effect")]
    [Tooltip("FOV normal (default)")]
    [SerializeField] private float normalFOV = 60f;

    [Tooltip("FOV saat speed boost aktif (lebih lebar = terasa cepat)")]
    [SerializeField] private float boostFOV = 72f;

    [Tooltip("Kecepatan transisi FOV")]
    [SerializeField] private float fovLerpSpeed = 4f;

    // ═══════════════════════════════════════════════════════════
    //  POST-PROCESSING EFFECTS
    // ═══════════════════════════════════════════════════════════
    [Header("Post-Processing (Opsional)")]
    [Tooltip("Volume Profile untuk post-processing effects. Biarkan kosong jika belum setup.")]
    [SerializeField] private Volume postProcessVolume;

    [Tooltip("Intensitas vignette saat danger/stun pulse")]
    [SerializeField] private float vignetteMaxIntensity = 0.5f;

    [Tooltip("Warna vignette saat danger")]
    [SerializeField] private Color vignetteColor = new Color(0.8f, 0f, 0f, 1f);

    [Tooltip("Durasi vignette pulse (detik)")]
    [SerializeField] private float vignettePulseDuration = 0.4f;

    [Tooltip("Intensitas chromatic aberration saat impact")]
    [SerializeField] private float chromaticMaxIntensity = 0.8f;

    [Tooltip("Durasi chromatic aberration (detik)")]
    [SerializeField] private float chromaticDuration = 0.3f;

    // ── Internal ──
    private Camera _cam;
    private Vector3 _originalLocalPos;
    private float _targetFOV;
    private Coroutine _shakeCoroutine;
    private Coroutine _vignetteCoroutine;
    private Coroutine _chromaticCoroutine;

    // Post-processing components (cached)
    private Vignette _vignette;
    private ChromaticAberration _chromatic;
    private ColorAdjustments _colorAdjust;

    // Boost state tracking
    private bool _isBoosted = false;
    private RunnerMovement _playerMovement;

    // ═══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;

        _originalLocalPos = transform.localPosition;
        _targetFOV = normalFOV;

        CachePostProcessing();
    }

    private void OnEnable()
    {
        RunnerEvents.OnStunned             += OnStunned;
        RunnerEvents.OnDied                += OnDied;
        RunnerEvents.OnLanded              += OnLanded;
        RunnerEvents.OnEnergyOrbCollected  += OnEnergyOrbCollected;
        RunnerEvents.OnStateChanged        += OnStateChanged;
    }

    private void OnDisable()
    {
        RunnerEvents.OnStunned             -= OnStunned;
        RunnerEvents.OnDied                -= OnDied;
        RunnerEvents.OnLanded              -= OnLanded;
        RunnerEvents.OnEnergyOrbCollected  -= OnEnergyOrbCollected;
        RunnerEvents.OnStateChanged        -= OnStateChanged;
    }

    private void Start()
    {
        // Cari player movement untuk cek boost state
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerMovement = player.GetComponent<RunnerMovement>();
        }

        if (_cam != null)
        {
            normalFOV = _cam.fieldOfView;
            _targetFOV = normalFOV;
        }
    }

    private void LateUpdate()
    {
        // ── FOV Lerp ──
        UpdateFOV();
    }

    // ═══════════════════════════════════════════════════════════
    //  SCREEN SHAKE
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Trigger screen shake. Menghormati GameSettings.cameraShake.
    /// </summary>
    public void Shake(float intensity, float duration)
    {
        // Cek setting
        if (!GameSettings.Instance.cameraShake) return;
        if (SettingsManager.Instance != null && !SettingsManager.Instance.IsCameraShakeEnabled()) return;

        if (_shakeCoroutine != null)
            StopCoroutine(_shakeCoroutine);

        _shakeCoroutine = StartCoroutine(ShakeRoutine(intensity, duration));
    }

    private IEnumerator ShakeRoutine(float intensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / duration;

            // Decay: intensitas berkurang secara exponential
            float currentIntensity = intensity * (1f - progress);

            // Random offset
            float offsetX = Random.Range(-1f, 1f) * currentIntensity;
            float offsetY = Random.Range(-1f, 1f) * currentIntensity;

            transform.localPosition = _originalLocalPos + new Vector3(offsetX, offsetY, 0f);

            yield return null;
        }

        // Reset posisi
        transform.localPosition = _originalLocalPos;
        _shakeCoroutine = null;
    }

    // ═══════════════════════════════════════════════════════════
    //  FOV EFFECT
    // ═══════════════════════════════════════════════════════════

    private void UpdateFOV()
    {
        if (_cam == null) return;

        // Cek apakah player sedang boost
        bool currentlyBoosted = _playerMovement != null && _playerMovement.IsBoosted;

        if (currentlyBoosted != _isBoosted)
        {
            _isBoosted = currentlyBoosted;
            _targetFOV = _isBoosted ? boostFOV : normalFOV;
        }

        // Smooth lerp FOV
        if (Mathf.Abs(_cam.fieldOfView - _targetFOV) > 0.01f)
        {
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _targetFOV, fovLerpSpeed * Time.unscaledDeltaTime);
        }
    }

    /// <summary>
    /// FOV punch: FOV membesar cepat lalu kembali (untuk impact moments).
    /// </summary>
    public void FOVPunch(float punchAmount = 5f, float duration = 0.2f)
    {
        if (_cam == null) return;
        StartCoroutine(FOVPunchRoutine(punchAmount, duration));
    }

    private IEnumerator FOVPunchRoutine(float punchAmount, float duration)
    {
        float originalFOV = _cam.fieldOfView;
        float punchedFOV = originalFOV + punchAmount;
        float elapsed = 0f;

        // Punch out
        while (elapsed < duration * 0.3f)
        {
            elapsed += Time.unscaledDeltaTime;
            _cam.fieldOfView = Mathf.Lerp(originalFOV, punchedFOV, elapsed / (duration * 0.3f));
            yield return null;
        }

        // Return
        elapsed = 0f;
        while (elapsed < duration * 0.7f)
        {
            elapsed += Time.unscaledDeltaTime;
            _cam.fieldOfView = Mathf.Lerp(punchedFOV, _targetFOV, elapsed / (duration * 0.7f));
            yield return null;
        }

        _cam.fieldOfView = _targetFOV;
    }

    // ═══════════════════════════════════════════════════════════
    //  POST-PROCESSING EFFECTS
    // ═══════════════════════════════════════════════════════════

    private void CachePostProcessing()
    {
        if (postProcessVolume == null)
        {
            // Coba cari Volume di scene
            postProcessVolume = FindFirstObjectByType<Volume>();
        }

        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out _vignette);
            postProcessVolume.profile.TryGet(out _chromatic);
            postProcessVolume.profile.TryGet(out _colorAdjust);
        }
    }

    /// <summary>
    /// Pulse vignette merah (untuk stun/danger).
    /// </summary>
    public void PulseVignette()
    {
        if (_vignette == null) return;

        if (_vignetteCoroutine != null)
            StopCoroutine(_vignetteCoroutine);

        _vignetteCoroutine = StartCoroutine(VignettePulseRoutine());
    }

    private IEnumerator VignettePulseRoutine()
    {
        _vignette.active = true;
        _vignette.color.Override(vignetteColor);

        float elapsed = 0f;
        float halfDuration = vignettePulseDuration * 0.5f;

        // Fade in
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;
            _vignette.intensity.Override(Mathf.Lerp(0f, vignetteMaxIntensity, t));
            yield return null;
        }

        // Fade out
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;
            _vignette.intensity.Override(Mathf.Lerp(vignetteMaxIntensity, 0f, t));
            yield return null;
        }

        _vignette.intensity.Override(0f);
        _vignetteCoroutine = null;
    }

    /// <summary>
    /// Pulse chromatic aberration (untuk impact).
    /// </summary>
    public void PulseChromatic()
    {
        if (_chromatic == null) return;

        if (_chromaticCoroutine != null)
            StopCoroutine(_chromaticCoroutine);

        _chromaticCoroutine = StartCoroutine(ChromaticPulseRoutine());
    }

    private IEnumerator ChromaticPulseRoutine()
    {
        _chromatic.active = true;

        float elapsed = 0f;

        // Quick spike then decay
        while (elapsed < chromaticDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / chromaticDuration;

            // Spike up quickly, then slow decay
            float intensity;
            if (t < 0.2f)
            {
                intensity = Mathf.Lerp(0f, chromaticMaxIntensity, t / 0.2f);
            }
            else
            {
                intensity = Mathf.Lerp(chromaticMaxIntensity, 0f, (t - 0.2f) / 0.8f);
            }

            _chromatic.intensity.Override(intensity);
            yield return null;
        }

        _chromatic.intensity.Override(0f);
        _chromaticCoroutine = null;
    }

    /// <summary>
    /// Desaturate layar perlahan (untuk death).
    /// </summary>
    public void DesaturateScreen(float duration = 1f)
    {
        if (_colorAdjust == null) return;
        StartCoroutine(DesaturateRoutine(duration));
    }

    private IEnumerator DesaturateRoutine(float duration)
    {
        _colorAdjust.active = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            // Saturation: 0 = normal, -100 = fully desaturated
            _colorAdjust.saturation.Override(Mathf.Lerp(0f, -80f, t));
            yield return null;
        }

        _colorAdjust.saturation.Override(-80f);
    }

    /// <summary>
    /// Reset semua post-processing ke default.
    /// </summary>
    public void ResetPostProcessing()
    {
        if (_vignette != null) _vignette.intensity.Override(0f);
        if (_chromatic != null) _chromatic.intensity.Override(0f);
        if (_colorAdjust != null) _colorAdjust.saturation.Override(0f);
    }

    // ═══════════════════════════════════════════════════════════
    //  EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════

    private void OnStunned()
    {
        Shake(stunShakeIntensity, stunShakeDuration);
        FOVPunch(3f, 0.25f);
        PulseVignette();
        PulseChromatic();
    }

    private void OnDied()
    {
        Shake(deathShakeIntensity, deathShakeDuration);
        FOVPunch(5f, 0.3f);
        PulseVignette();
        PulseChromatic();
        DesaturateScreen(1.5f);
    }

    private void OnLanded()
    {
        Shake(landShakeIntensity, landShakeDuration);
    }

    private void OnEnergyOrbCollected()
    {
        FOVPunch(4f, 0.3f);
    }

    private void OnStateChanged(RunnerState prev, RunnerState next)
    {
        // Reset post-processing saat restart/respawn
        if (next == RunnerState.Idle && prev == RunnerState.Dying)
        {
            ResetPostProcessing();
            if (_cam != null) _cam.fieldOfView = normalFOV;
        }
    }
}
