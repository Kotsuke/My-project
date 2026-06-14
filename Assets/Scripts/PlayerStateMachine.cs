using UnityEngine;

// ============================================================
//  ENUM: Semua state yang mungkin dimiliki player
//  Tambah state baru di sini, TIDAK perlu ubah file lain
// ============================================================
public enum PlayerState
{
    Idle,
    Run,
    Sprint,
    Jump,
    DoubleJump,
    Falling,
    WallRunLeft,
    WallRunRight,
    WallJump,
    Slide,
    LandingRoll,
    HardLanding,
    LandToRun,
    Stunned,
    Dying
}

// ============================================================
//  EVENT CHANNEL: Komunikasi antar sistem tanpa coupling keras
//  Sistem A trigger event → Sistem B dengerin, tidak ada
//  referensi langsung antar komponen.
// ============================================================
public static class PlayerEvents
{
    // Movement events
    public static System.Action<PlayerState, PlayerState> OnStateChanged;  // (prevState, newState)
    public static System.Action<float>                    OnLanded;         // param: fall distance
    public static System.Action                           OnJumped;
    public static System.Action                           OnDoubleJumped;
    public static System.Action<int>                      OnWallRunChanged; // 0=off, 1=left, 2=right
    public static System.Action                           OnWallJumped;
    public static System.Action                           OnSlideStarted;
    public static System.Action                           OnDied;
    public static System.Action<bool>                     OnStunnedChanged;
}

// ============================================================
//  STATE MACHINE: Satu-satunya tempat state diubah.
//  Komponen lain HANYA baca CurrentState atau subscribe event.
// ============================================================
public class PlayerStateMachine : MonoBehaviour
{
    public static PlayerStateMachine Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool showStateLog = false;

    public PlayerState CurrentState  { get; private set; } = PlayerState.Idle;
    public PlayerState PreviousState { get; private set; } = PlayerState.Idle;

    // ── Helpers status baca-cepat (tanpa overhead string compare) ──
    public bool IsGrounded      => CurrentState == PlayerState.Idle
                                || CurrentState == PlayerState.Run
                                || CurrentState == PlayerState.Sprint;

    public bool IsAirborne      => CurrentState == PlayerState.Jump
                                || CurrentState == PlayerState.DoubleJump
                                || CurrentState == PlayerState.Falling
                                || CurrentState == PlayerState.WallJump;

    public bool IsWallRunning   => CurrentState == PlayerState.WallRunLeft
                                || CurrentState == PlayerState.WallRunRight;

    public bool IsInLandingAnim => CurrentState == PlayerState.LandingRoll
                                || CurrentState == PlayerState.HardLanding
                                || CurrentState == PlayerState.LandToRun;

    public bool IsActionLocked  => IsInLandingAnim
                                || CurrentState == PlayerState.Slide
                                || CurrentState == PlayerState.Dying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ── Satu-satunya entry point untuk ganti state ──
    public bool TrySetState(PlayerState newState)
    {
        if (newState == CurrentState) return false;

        // Validasi transisi ilegal
        if (!IsTransitionAllowed(CurrentState, newState))
        {
            if (showStateLog)
                Debug.LogWarning($"[StateMachine] Transition BLOCKED: {CurrentState} → {newState}");
            return false;
        }

        PreviousState = CurrentState;
        CurrentState  = newState;

        if (showStateLog)
            Debug.Log($"[StateMachine] {PreviousState} → {CurrentState}");

        PlayerEvents.OnStateChanged?.Invoke(PreviousState, CurrentState);
        return true;
    }

    // ── Force set (untuk kematian/stun yang tidak bisa dicegah) ──
    public void ForceSetState(PlayerState newState)
    {
        PreviousState = CurrentState;
        CurrentState  = newState;

        if (showStateLog)
            Debug.Log($"[StateMachine] FORCE {PreviousState} → {CurrentState}");

        PlayerEvents.OnStateChanged?.Invoke(PreviousState, CurrentState);
    }

    // ── Tabel transisi yang diizinkan ──
    private bool IsTransitionAllowed(PlayerState from, PlayerState to)
    {
        // Dying adalah terminal state — tidak bisa keluar
        if (from == PlayerState.Dying) return false;

        // Landing anim state: HANYA boleh keluar ke Idle/Run/Sprint/Dying/Stunned/Falling
        // Bug lama: IsActionLocked memblok semua exit termasuk transisi normal selesai animasi
        if (IsInLandingAnim)
        {
            return to == PlayerState.Idle
                || to == PlayerState.Run
                || to == PlayerState.Sprint
                || to == PlayerState.Dying
                || to == PlayerState.Stunned
                || to == PlayerState.Falling;
        }

        // Slide: boleh keluar ke Idle/Run/Sprint/Dying/Stunned/Falling
        if (from == PlayerState.Slide)
        {
            return to == PlayerState.Idle
                || to == PlayerState.Run
                || to == PlayerState.Sprint
                || to == PlayerState.Jump   // slide-cancel jump (opsional)
                || to == PlayerState.Dying
                || to == PlayerState.Stunned
                || to == PlayerState.Falling;
        }

        // Aturan per state tujuan
        switch (to)
        {
            case PlayerState.Jump:
                // Tidak boleh loncat saat dalam animasi landing
                return from != PlayerState.Dying
                    && !IsInLandingAnim;

            case PlayerState.DoubleJump:
                return from == PlayerState.Jump
                    || from == PlayerState.Falling
                    || from == PlayerState.WallJump;

            case PlayerState.WallRunLeft:
            case PlayerState.WallRunRight:
                return from == PlayerState.Run
                    || from == PlayerState.Sprint
                    || from == PlayerState.Jump
                    || from == PlayerState.DoubleJump
                    || from == PlayerState.Falling
                    || from == PlayerState.WallJump;

            case PlayerState.WallJump:
                return from == PlayerState.WallRunLeft
                    || from == PlayerState.WallRunRight;

            case PlayerState.Slide:
                return from == PlayerState.Run
                    || from == PlayerState.Sprint;

            case PlayerState.Stunned:
                return from != PlayerState.Dying;

            default:
                return true;
        }
    }
}