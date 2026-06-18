using UnityEngine;

// ============================================================
//  RUNNER STATE MANAGER
//  Mengelola transisi state untuk endless runner.
//  Lebih sederhana dari PlayerStateMachine karena hanya
//  ada 6 state tanpa wall run / double jump.
// ============================================================
public class RunnerStateManager : MonoBehaviour
{
    public static RunnerStateManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool showStateLog = false;

    public RunnerState CurrentState  { get; private set; } = RunnerState.Idle;
    public RunnerState PreviousState { get; private set; } = RunnerState.Idle;

    // ── Quick status helpers ──
    public bool IsGrounded   => CurrentState == RunnerState.Running || CurrentState == RunnerState.Idle;
    public bool IsAirborne   => CurrentState == RunnerState.Jumping
                             || CurrentState == RunnerState.Falling;
    public bool IsActionLocked => CurrentState == RunnerState.Sliding
                               || CurrentState == RunnerState.Dying
                               || CurrentState == RunnerState.Stunned;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Coba ganti state. Return true jika transisi diizinkan.
    /// </summary>
    public bool TrySetState(RunnerState newState)
    {
        if (newState == CurrentState) return false;

        if (!IsTransitionAllowed(CurrentState, newState))
        {
            if (showStateLog)
                Debug.LogWarning($"[RunnerSM] BLOCKED: {CurrentState} → {newState}");
            return false;
        }

        PreviousState = CurrentState;
        CurrentState  = newState;

        if (showStateLog)
            Debug.Log($"[RunnerSM] {PreviousState} → {CurrentState}");

        RunnerEvents.OnStateChanged?.Invoke(PreviousState, CurrentState);
        return true;
    }

    /// <summary>
    /// Paksa ganti state (untuk death/stun yang tidak bisa dicegah).
    /// </summary>
    public void ForceSetState(RunnerState newState)
    {
        PreviousState = CurrentState;
        CurrentState  = newState;

        if (showStateLog)
            Debug.Log($"[RunnerSM] FORCE {PreviousState} → {CurrentState}");

        RunnerEvents.OnStateChanged?.Invoke(PreviousState, CurrentState);
    }

    // ── Transition Rules ──
    private bool IsTransitionAllowed(RunnerState from, RunnerState to)
    {
        // Dying = terminal state, tidak bisa keluar
        if (from == RunnerState.Dying) return false;

        switch (to)
        {
            case RunnerState.Running:
                // Boleh kembali ke Running dari apapun kecuali Dying
                return true;

            case RunnerState.Jumping:
                // Hanya bisa lompat saat lari di tanah
                return from == RunnerState.Running;

            case RunnerState.Falling:
                // Falling setelah puncak lompatan
                return from == RunnerState.Jumping
                    || from == RunnerState.Running;

            case RunnerState.Sliding:
                // Hanya bisa slide saat lari di tanah
                return from == RunnerState.Running;

            case RunnerState.Stunned:
                // Bisa stunned dari manapun kecuali Dying
                return from != RunnerState.Dying;

            case RunnerState.Dying:
                // Bisa mati dari mana saja
                return true;

            default:
                return true;
        }
    }
}
