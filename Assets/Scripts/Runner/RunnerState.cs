using System;

// ============================================================
//  RUNNER STATE
//  State enum untuk endless runner (Pepsiman-style).
//  Hanya state yang relevan untuk auto-run gameplay.
// ============================================================
public enum RunnerState
{
    Idle,           // Persiapan di awal (diam)
    Running,        // Default: karakter selalu lari ke depan
    Jumping,        // Lompat
    Falling,        // Jatuh setelah puncak lompatan
    Sliding,        // Slide di bawah halangan
    Stunned,        // Kena halangan, slow down sebentar
    Dying           // Game over
}

// ============================================================
//  RUNNER EVENTS
//  Event channel — komunikasi antar sistem tanpa coupling.
// ============================================================
public static class RunnerEvents
{
    public static Action<RunnerState, RunnerState> OnStateChanged;   // (prev, next)
    public static Action                           OnJumped;
    public static Action                           OnSlideStarted;
    public static Action                           OnSlideEnded;
    public static Action                           OnLanded;
    public static Action                           OnStunned;
    public static Action                           OnDied;
    public static Action<int>                      OnLaneChanged;    // -1, 0, 1
    public static Action                           OnEnergyOrbCollected;  // ambil energy orb → speed boost
}
