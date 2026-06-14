using UnityEngine;

// ============================================================
//  WALL RUN HANDLER
//  Tanggung jawab: deteksi dinding, logika wall run dengan timer,
//  dan wall jump dengan arc natural (parabola).
//
//  Perbaikan dari kode lama:
//  1. Ada timer maksimal wall run agar tidak nempel selamanya
//  2. Wall jump menggunakan arc parabola lewat velocity injection
//  3. Cooldown agar tidak langsung nempel ke dinding yang sama
//  4. Minimum speed check agar wall run hanya aktif saat cepat
// ============================================================
public class WallRunHandler : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Layer yang dianggap sebagai dinding")]
    public LayerMask wallMask;

    [Tooltip("Jarak raycast ke kiri dan kanan")]
    public float wallCheckDistance = 0.8f;

    [Tooltip("Minimum kecepatan horizontal agar wall run aktif")]
    public float minWallRunSpeed = 5f;

    [Header("Wall Run Physics")]
    [Tooltip("Gravitasi saat wall run (negatif = merosot perlahan)")]
    public float wallRunGravity = -2f;

    [Tooltip("Boost kecepatan horizontal saat mulai wall run")]
    public float wallRunSpeedBoost = 1.2f;

    [Tooltip("Durasi maksimal wall run (detik)")]
    public float maxWallRunDuration = 2.5f;

    [Header("Wall Jump Arc")]
    [Tooltip("Kekuatan loncat ke atas saat wall jump")]
    public float wallJumpUpForce   = 9f;

    [Tooltip("Kekuatan loncat menjauhi dinding")]
    public float wallJumpSideForce = 7f;

    [Tooltip("Kekuatan loncat ke depan saat wall jump")]
    public float wallJumpForwardForce = 3f;

    [Header("Cooldown")]
    [Tooltip("Cooldown setelah lepas dari dinding (detik)")]
    public float wallCooldown = 0.5f;

    // ── State publik (dibaca PlayerMovement) ──
    public bool  IsWallRunning  { get; private set; }
    public int   WallSide       { get; private set; }   // 0=none, 1=left, 2=right
    public bool  CanStartWallRun => _wallCooldownTimer <= 0f;

    // Velocity hasil wall jump — dibaca oleh PlayerMovement untuk inject ke velocity
    public Vector3 WallJumpVelocity { get; private set; }
    public bool    HasWallJumpVelocity { get; private set; }

    // ── Internal ──
    private float     _wallRunTimer;
    private float     _wallCooldownTimer;
    private RaycastHit _wallHit;
    private PlayerStateMachine _sm;

    // Diberi tahu dari PlayerMovement
    [HideInInspector] public float CurrentHorizontalSpeed;

    private void Awake()
    {
        _sm = GetComponent<PlayerStateMachine>();
    }

    private void OnEnable()
    {
        PlayerEvents.OnStateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        PlayerEvents.OnStateChanged -= OnStateChanged;
    }

    // ── Dipanggil tiap frame oleh PlayerMovement ──
    public void Tick(float deltaTime, bool isGrounded, float forwardInput)
    {
        // Countdown cooldown
        if (_wallCooldownTimer > 0f)
            _wallCooldownTimer -= deltaTime;

        // Tidak bisa wall run saat di tanah
        if (isGrounded)
        {
            TryStop();
            return;
        }

        // Cek dinding dari tinggi dada (1 meter ke atas) agar lebih akurat dan tidak terhalang lantai
        Vector3 checkOrigin = transform.position + Vector3.up * 1.0f;
        bool wallLeft  = Physics.Raycast(checkOrigin, -transform.right,
                             out RaycastHit leftHit,  wallCheckDistance, wallMask);
        bool wallRight = Physics.Raycast(checkOrigin,  transform.right,
                             out RaycastHit rightHit, wallCheckDistance, wallMask);

        bool hasWall = wallLeft || wallRight;

        // Syarat mulai: menekan maju, ada dinding, speed cukup, cooldown habis
        if (!hasWall || forwardInput < 0.1f || CurrentHorizontalSpeed < minWallRunSpeed || !CanStartWallRun)
        {
            TryStop();
            return;
        }

        // Pilih sisi
        int desiredSide = wallLeft ? 1 : 2;
        _wallHit        = wallLeft ? leftHit : rightHit;

        if (!IsWallRunning)
        {
            StartWallRun(desiredSide);
        }
        else
        {
            // Tick timer
            _wallRunTimer += deltaTime;
            if (_wallRunTimer >= maxWallRunDuration)
            {
                TryStop();
            }
        }
    }

    // ── Eksekusi wall jump ──
    public void ExecuteWallJump()
    {
        if (!IsWallRunning) return;

        Vector3 wallNormal = _wallHit.normal;

        // Arc alami: memantul dari dinding + naik + sedikit ke depan
        WallJumpVelocity = wallNormal      * wallJumpSideForce
                         + Vector3.up      * wallJumpUpForce
                         + transform.forward * wallJumpForwardForce;

        HasWallJumpVelocity = true;

        // Ganti state ke WallJump TERLEBIH DAHULU agar transisinya legal di StateMachine
        _sm.TrySetState(PlayerState.WallJump);

        TryStop();
        
        PlayerEvents.OnWallJumped?.Invoke();
    }

    // ── Setelah PlayerMovement membaca velocity, reset flag ──
    public void ConsumeWallJumpVelocity()
    {
        HasWallJumpVelocity = false;
        WallJumpVelocity    = Vector3.zero;
    }

    // ── Getter kecepatan merosot untuk PlayerMovement ──
    public float GetWallGravity()
    {
        // Semakin lama di dinding, semakin cepat merosot (feel natural)
        float t = Mathf.Clamp01(_wallRunTimer / maxWallRunDuration);
        return Mathf.Lerp(wallRunGravity, wallRunGravity * 2.5f, t * t);
    }

    // ── Internal helpers ──
    private void StartWallRun(int side)
    {
        IsWallRunning  = true;
        WallSide       = side;
        _wallRunTimer  = 0f;

        _sm.TrySetState(side == 1 ? PlayerState.WallRunLeft : PlayerState.WallRunRight);
        PlayerEvents.OnWallRunChanged?.Invoke(side);
    }

    private void TryStop()
    {
        if (!IsWallRunning) return;

        IsWallRunning      = false;
        WallSide           = 0;
        _wallCooldownTimer = wallCooldown;

        // Hanya paksa set ke Falling jika state saat ini masih WallRun (bukan WallJump)
        if (_sm.IsWallRunning)
            _sm.TrySetState(PlayerState.Falling);

        PlayerEvents.OnWallRunChanged?.Invoke(0);
    }

    private void OnStateChanged(PlayerState prev, PlayerState next)
    {
        // Jika masuk ke state grounded, reset cooldown
        if (next == PlayerState.Idle || next == PlayerState.Run || next == PlayerState.Sprint)
        {
            _wallCooldownTimer = 0f;
            IsWallRunning      = false;
            WallSide           = 0;
        }
    }

    // ── Debug visual ──
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Gizmos.DrawLine(origin, origin - transform.right * wallCheckDistance);
        Gizmos.DrawLine(origin, origin + transform.right * wallCheckDistance);
    }
}