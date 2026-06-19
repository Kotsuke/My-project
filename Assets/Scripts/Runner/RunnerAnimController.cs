using UnityEngine;

// ============================================================
//  RUNNER ANIMATION CONTROLLER
//  Mengelola Animator untuk endless runner.
//
//  Animasi dari folder "motion animation":
//  - Fast Run.fbx       → Running (default, selalu aktif)
//  - Sprint.fbx         → Saat speed boost
//  - Running Jump.fbx   → Lompat
//  - Running Slide.fbx  → Slide
//  - Falling Idle.fbx   → Jatuh
//  - Falling To Landing → Landing
//  - Stunned.fbx        → Kena halangan
//  - Dying.fbx          → Game Over
//
//  Parameter Animator:
//  - Speed (float)         : 1.0 = run, 2.0 = sprint/boost
//  - IsGrounded (bool)     : di tanah atau tidak
//  - VerticalVelocity (float) : untuk blend jump/fall
//  - Jump (trigger)        : trigger lompat
//  - Slide (trigger)       : trigger slide
//  - Stunned (bool)        : stunned on/off
//  - Die (trigger)         : trigger mati
// ============================================================
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(RunnerStateManager))]
public class RunnerAnimController : MonoBehaviour
{
    private Animator            _anim;
    private RunnerStateManager  _sm;
    private RunnerMovement      _movement;

    // ── Cached Animator Parameter Hashes ──
    private static readonly int Hash_Speed       = Animator.StringToHash("Speed");
    private static readonly int Hash_IsGrounded  = Animator.StringToHash("IsGrounded");
    private static readonly int Hash_VerticalVel = Animator.StringToHash("VerticalVelocity");
    private static readonly int Hash_Jump        = Animator.StringToHash("Jump");
    private static readonly int Hash_Slide       = Animator.StringToHash("Slide");
    private static readonly int Hash_Stunned     = Animator.StringToHash("Stunned");
    private static readonly int Hash_Die         = Animator.StringToHash("Die");

    [Header("Smoothing")]
    [Tooltip("Damping time untuk blend Speed parameter")]
    public float speedDampTime = 0.1f;

    private void Awake()
    {
        _anim     = GetComponent<Animator>();
        _sm       = GetComponent<RunnerStateManager>();
        _movement = GetComponent<RunnerMovement>();
    }

    private void OnEnable()
    {
        RunnerEvents.OnStateChanged += OnStateChanged;
        RunnerEvents.OnStunned      += OnStunned;
        RunnerEvents.OnDied         += OnDied;
    }

    private void OnDisable()
    {
        RunnerEvents.OnStateChanged -= OnStateChanged;
        RunnerEvents.OnStunned      -= OnStunned;
        RunnerEvents.OnDied         -= OnDied;
    }

    // ── Update: parameter kontinu tiap frame ──
    private void LateUpdate()
    {
        if (_anim == null || _movement == null) return;

        // Jika mati, jangan update parameter pergerakan agar animasi Die bisa terputar tanpa intervensi
        if (_sm.CurrentState == RunnerState.Dying)
        {
            _anim.SetFloat(Hash_Speed, 0f);
            return;
        }

        // Speed: 0f = idle, 1.0 = normal run, 2.0 = boost/sprint
        float targetSpeed = 0f;
        if (_sm.CurrentState == RunnerState.Idle)
        {
            targetSpeed = 0f;
        }
        else
        {
            targetSpeed = _movement.IsBoosted ? 2f : 1f;
        }
        _anim.SetFloat(Hash_Speed, targetSpeed, speedDampTime, Time.deltaTime);

        // IsGrounded
        _anim.SetBool(Hash_IsGrounded, _movement.IsGrounded);

        // VerticalVelocity untuk blend jump/fall
        _anim.SetFloat(Hash_VerticalVel, _movement.VerticalVelocity);
    }

    // ── Reaksi terhadap perubahan state ──
    private void OnStateChanged(RunnerState prev, RunnerState next)
    {
        if (_anim == null) return;

        switch (next)
        {
            case RunnerState.Jumping:
                _anim.SetTrigger(Hash_Jump);
                break;

            case RunnerState.Sliding:
                _anim.SetTrigger(Hash_Slide);
                break;

            case RunnerState.Running:
                // Keluar dari stun → matikan bool Stunned
                if (prev == RunnerState.Stunned)
                    _anim.SetBool(Hash_Stunned, false);
                break;
        }
    }

    private void OnStunned()
    {
        if (_anim == null) return;
        _anim.SetBool(Hash_Stunned, true);
    }

    private void OnDied()
    {
        if (_anim == null) return;
        _anim.SetBool(Hash_Stunned, false); // Matikan bool Stunned agar transisi ke Dying tidak terhambat
        _anim.SetTrigger(Hash_Die);
    }

    /// <summary>
    /// Reset semua trigger (utility).
    /// </summary>
    public void ResetAllTriggers()
    {
        _anim.ResetTrigger(Hash_Jump);
        _anim.ResetTrigger(Hash_Slide);
        _anim.ResetTrigger(Hash_Die);
    }
}
