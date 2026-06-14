using UnityEngine;

// ============================================================
//  PLAYER ANIMATION CONTROLLER
//  Tanggung jawab tunggal: mengelola Animator.
//
//  Arsitektur:
//  - Hash semua parameter di Awake (ZERO string per frame)
//  - Subscribe ke PlayerEvents dan PlayerStateMachine
//  - Kirim trigger/set float/bool berdasarkan event, bukan polling
//  - PlayerMovement memberi data Speed & VerticalVelocity tiap frame
//
//  Cara extend: tambah event baru di PlayerEvents → tambah
//  subscription di sini. Tidak perlu ubah file lain.
// ============================================================
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerAnimationController : MonoBehaviour
{
    private Animator           _anim;
    private PlayerStateMachine _sm;
    private PlayerMovement     _movement;

    // ── Cached Animator Parameter Hashes (dibuat sekali, lebih cepat) ──
    private static readonly int Hash_Speed           = Animator.StringToHash("Speed");
    private static readonly int Hash_IsGrounded      = Animator.StringToHash("IsGrounded");
    private static readonly int Hash_VerticalVel     = Animator.StringToHash("VerticalVelocity");
    private static readonly int Hash_WallRunSide     = Animator.StringToHash("WallRunSide");
    private static readonly int Hash_Jump            = Animator.StringToHash("Jump");
    private static readonly int Hash_DoubleJump      = Animator.StringToHash("DoubleJump");
    private static readonly int Hash_Slide           = Animator.StringToHash("Slide");
    private static readonly int Hash_Roll            = Animator.StringToHash("Roll");
    private static readonly int Hash_HardLanding     = Animator.StringToHash("HardLanding");
    private static readonly int Hash_LandToRun       = Animator.StringToHash("LandToRun");
    private static readonly int Hash_Stunned         = Animator.StringToHash("Stunned");
    private static readonly int Hash_Die             = Animator.StringToHash("Die");

    [Header("Smoothing")]
    [Tooltip("Kecepatan smooth blend parameter Speed")]
    public float speedDampTime = 0.1f;

    private void Awake()
    {
        _anim     = GetComponent<Animator>();
        _sm       = GetComponent<PlayerStateMachine>();
        _movement = GetComponent<PlayerMovement>();
    }

    private void OnEnable()
    {
        // Subscribe ke semua event yang relevan
        PlayerEvents.OnStateChanged  += OnStateChanged;
        PlayerEvents.OnWallRunChanged += OnWallRunChanged;
        PlayerEvents.OnStunnedChanged += OnStunnedChanged;
        PlayerEvents.OnDied          += OnDied;
    }

    private void OnDisable()
    {
        PlayerEvents.OnStateChanged  -= OnStateChanged;
        PlayerEvents.OnWallRunChanged -= OnWallRunChanged;
        PlayerEvents.OnStunnedChanged -= OnStunnedChanged;
        PlayerEvents.OnDied          -= OnDied;
    }

    // ── Update: hanya parameter yang butuh nilai kontinu tiap frame ──
    private void LateUpdate()
    {
        if (_anim == null || _movement == null) return;

        // Speed: 0 = Idle, 1 = Run, 2 = Sprint — smooth blend
        float targetSpeed = _movement.NormalizedSpeed;
        _anim.SetFloat(Hash_Speed, targetSpeed, speedDampTime, Time.deltaTime);

        // IsGrounded: true juga saat wall running (agar tidak trigger falling)
        bool effectivelyGrounded = _movement.IsGrounded || _sm.IsWallRunning;
        _anim.SetBool(Hash_IsGrounded, effectivelyGrounded);

        // VerticalVelocity: untuk blend falling/jump arc
        _anim.SetFloat(Hash_VerticalVel, _movement.VerticalVelocity);
    }

    // ── Event handler: state berubah → set trigger yang sesuai ──
    private void OnStateChanged(PlayerState prev, PlayerState next)
    {
        if (_anim == null) return;

        switch (next)
        {
            case PlayerState.Jump:
            case PlayerState.WallJump:
                _anim.SetTrigger(Hash_Jump);
                break;

            case PlayerState.DoubleJump:
                _anim.SetTrigger(Hash_DoubleJump);
                break;

            case PlayerState.Slide:
                _anim.SetTrigger(Hash_Slide);
                break;

            case PlayerState.LandingRoll:
                _anim.SetTrigger(Hash_Roll);
                break;

            case PlayerState.HardLanding:
                _anim.SetTrigger(Hash_HardLanding);
                break;

            case PlayerState.LandToRun:
                _anim.SetTrigger(Hash_LandToRun);
                break;
        }
    }

    // ── Wall run: set int parameter WallRunSide ──
    private void OnWallRunChanged(int side)
    {
        if (_anim == null) return;
        _anim.SetInteger(Hash_WallRunSide, side);
    }

    // ── Stunned: set bool parameter ──
    private void OnStunnedChanged(bool isStunned)
    {
        if (_anim == null) return;
        _anim.SetBool(Hash_Stunned, isStunned);
    }

    // ── Die: set trigger ──
    private void OnDied()
    {
        if (_anim == null) return;
        _anim.SetTrigger(Hash_Die);
    }

    // ── API untuk sistem lain yang perlu override animasi langsung ──
    public void ResetAllTriggers()
    {
        _anim.ResetTrigger(Hash_Jump);
        _anim.ResetTrigger(Hash_DoubleJump);
        _anim.ResetTrigger(Hash_Slide);
        _anim.ResetTrigger(Hash_Roll);
        _anim.ResetTrigger(Hash_HardLanding);
        _anim.ResetTrigger(Hash_LandToRun);
        _anim.ResetTrigger(Hash_Die);
    }
}