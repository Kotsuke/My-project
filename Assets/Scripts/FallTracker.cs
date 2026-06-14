using UnityEngine;

// ============================================================
//  FALL TRACKER
//  Tanggung jawab tunggal: melacak ketinggian jatuh secara
//  akurat dan menentukan jenis landing yang sesuai.
//
//  Masalah di kode lama:
//  - highestYPoint di-reset setiap frame saat isGrounded,
//    sehingga lompatan kecil dari tepi tebing bisa salah hitung.
//  - Tidak ada hysteresis: langsung switch antara "grounded"
//    dan "airborne" tanpa buffer.
// ============================================================
public class FallTracker : MonoBehaviour
{
    [Header("Landing Thresholds")]
    [Tooltip("Jarak jatuh minimum untuk memicu HardLanding")]
    public float hardLandingThreshold = 4f;

    [Tooltip("Jarak jatuh minimum untuk memicu LandToRun")]
    public float landToRunThreshold   = 2f;

    [Tooltip("Jarak jatuh minimum untuk memicu Roll")]
    public float rollThreshold        = 6f;

    // Nilai ini disetel dari PlayerMovement berdasarkan kecepatan saat ini
    [HideInInspector] public bool IsSprinting = false;
    [HideInInspector] public bool IsMoving    = false;

    // ── State internal ──
    private float _peakY;           // titik Y tertinggi sejak meninggalkan tanah
    private bool  _trackingFall;    // apakah sedang di udara dan melacak

    // Baca dari luar jika perlu debugging
    public float LastFallDistance { get; private set; }

    private PlayerStateMachine _sm;

    private void Awake()
    {
        _sm = GetComponent<PlayerStateMachine>();
        if (_sm == null)
            Debug.LogError("[FallTracker] PlayerStateMachine tidak ditemukan!");
    }

    private void OnEnable()
    {
        PlayerEvents.OnStateChanged += HandleStateChanged;
        PlayerEvents.OnLanded       += HandleLanded;
    }

    private void OnDisable()
    {
        PlayerEvents.OnStateChanged -= HandleStateChanged;
        PlayerEvents.OnLanded       -= HandleLanded;
    }

    // ── Dipanggil tiap frame oleh PlayerMovement saat di udara ──
    public void TrackPeak(float currentY)
    {
        if (!_trackingFall) return;

        if (currentY > _peakY)
            _peakY = currentY;
    }

    // ── Dipanggil saat player baru saja mendarat ──
    public LandingType EvaluateLanding(float landedY)
    {
        if (!_trackingFall) return LandingType.Normal;

        LastFallDistance = _peakY - landedY;
        _trackingFall    = false;

        return ClassifyLanding(LastFallDistance);
    }

    // ── Mulai tracking saat player meninggalkan tanah ──
    public void BeginTracking(float currentY)
    {
        _peakY        = currentY;
        _trackingFall = true;
    }

    // ── Klasifikasi berdasarkan threshold dan status movement ──
    private LandingType ClassifyLanding(float dist)
    {
        // Jika tidak cukup tinggi, selalu landing biasa
        if (dist < landToRunThreshold)
            return LandingType.Normal;

        // Jika ada input gerak, masuk ke LandToRun (menekan input movement)
        if (IsMoving)
            return LandingType.LandToRun;

        // Jika tidak ada input gerak (tanpa input lain)
        // Kalau sangat tinggi, lakukan roll
        if (dist >= rollThreshold)
            return LandingType.Roll;

        // Kalau sedang (antara hardLandingThreshold dan rollThreshold), lakukan Hard Landing
        if (dist >= hardLandingThreshold)
            return LandingType.Hard;

        return LandingType.Normal;
    }

    private void HandleStateChanged(PlayerState prev, PlayerState next)
    {
        // Mulai tracking saat meninggalkan tanah atau dinding
        bool wasAttached = prev == PlayerState.Idle
                        || prev == PlayerState.Run
                        || prev == PlayerState.Sprint
                        || prev == PlayerState.WallRunLeft
                        || prev == PlayerState.WallRunRight;

        bool nowAirborne = next == PlayerState.Jump
                        || next == PlayerState.Falling
                        || next == PlayerState.DoubleJump
                        || next == PlayerState.WallJump;

        if (wasAttached && nowAirborne)
        {
            BeginTracking(transform.position.y);
        }
        else if (next == PlayerState.WallRunLeft || next == PlayerState.WallRunRight)
        {
            // Reset / stop tracking saat menempel ke dinding
            _trackingFall = false;
        }
    }

    private void HandleLanded(float fallDist)
    {
        // Terima data dari PlayerMovement untuk sinkronisasi log
        LastFallDistance = fallDist;
    }
}

// ── Enum tipe landing ──
public enum LandingType
{
    Normal,     // jatuh kecil, langsung ke Idle/Run
    LandToRun,  // jatuh sedang sambil bergerak
    Hard,       // jatuh keras, berdiri di tempat
    Roll        // jatuh sangat keras atau sambil sprint
}