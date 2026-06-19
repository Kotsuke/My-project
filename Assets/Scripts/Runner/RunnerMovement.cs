using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
//  RUNNER MOVEMENT
//  Script utama untuk endless runner (Pepsiman-style).
//
//  Mekanik:
//  1. AUTO-RUN: Karakter selalu berlari ke depan (sumbu Z)
//  2. LANE SYSTEM: 3 lane (kiri, tengah, kanan) — geser kiri/kanan
//  3. JUMP: Lompat untuk menghindari halangan di bawah
//  4. SLIDE: Slide untuk menghindari halangan di atas
//
//  Kontrol: Kiri, Kanan, Jump, Slide (TANPA maju/mundur)
//
//  Menggunakan CharacterController untuk physics.
//  Semua animasi diambil dari folder "motion animation".
// ============================================================
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(RunnerStateManager))]
public class RunnerMovement : MonoBehaviour
{
    // ── Komponen ──
    private CharacterController _cc;
    private RunnerStateManager  _sm;
    private PlayerControls      _input;

    // ═══════════════════════════════════════════════════════════
    //  SPEED SETTINGS
    // ═══════════════════════════════════════════════════════════
    [Header("Auto-Run Speed")]
    [Tooltip("Kecepatan lari normal (konstan)")]
    public float runSpeed = 10f;

    [Tooltip("Kecepatan saat speed boost aktif")]
    public float boostSpeed = 15f;

    [Tooltip("Akselerasi dari runSpeed ke boostSpeed")]
    public float speedAcceleration = 5f;

    // ═══════════════════════════════════════════════════════════
    //  LANE SYSTEM
    // ═══════════════════════════════════════════════════════════
    [Header("Lane System")]
    [Tooltip("Jarak antar lane (meter)")]
    public float laneDistance = 3f;

    [Tooltip("Kecepatan pindah lane (semakin tinggi = semakin responsive)")]
    public float laneSwitchSpeed = 12f;

    // -1 = kiri, 0 = tengah, 1 = kanan
    private int _currentLane = 0;

    // ═══════════════════════════════════════════════════════════
    //  JUMP
    // ═══════════════════════════════════════════════════════════
    [Header("Jump")]
    [Tooltip("Tinggi lompatan")]
    public float jumpHeight = 2.5f;

    [Tooltip("Gravitasi")]
    public float gravity = -25f;

    // ═══════════════════════════════════════════════════════════
    //  SLIDE
    // ═══════════════════════════════════════════════════════════
    [Header("Slide")]
    [Tooltip("Durasi slide dalam detik")]
    public float slideDuration = 0.8f;

    [Tooltip("Kecepatan saat slide (biasanya lebih cepat dari run)")]
    public float slideSpeed = 14f;

    [Tooltip("Tinggi collider saat slide")]
    public float slideColliderHeight = 0.5f;

    [Tooltip("Center collider saat slide")]
    public float slideColliderCenter = 0.25f;

    // ═══════════════════════════════════════════════════════════
    //  GROUND CHECK
    // ═══════════════════════════════════════════════════════════
    [Header("Ground Check")]
    public Transform groundCheck;
    public float     groundDistance = 0.3f;
    public LayerMask groundMask;

    // ═══════════════════════════════════════════════════════════
    //  STUN
    // ═══════════════════════════════════════════════════════════
    [Header("Stun")]
    [Tooltip("Durasi stun saat kena halangan")]
    public float stunDuration = 1.0f;

    [Tooltip("Speed saat stun (lambat jalan)")]
    public float stunSpeed = 3f;

    // ═══════════════════════════════════════════════════════════
    //  FALL LIMIT
    // ═══════════════════════════════════════════════════════════
    [Header("Fall Limit")]
    [Tooltip("Batas koordinat Y minimum. Jika player berada di bawah ini, player mati jatuh.")]
    public float fallThreshold = -5f;

    // ═══════════════════════════════════════════════════════════
    //  ENERGY ORB (speed boost)
    // ═══════════════════════════════════════════════════════════
    [Header("Energy Orb / Speed Boost")]
    [Tooltip("Durasi speed boost per energy orb (detik)")]
    public float boostDuration = 3f;

    [Tooltip("Tambahan durasi jika ambil orb saat boost masih aktif (stack)")]
    public float boostStackBonus = 2f;

    [Tooltip("Maksimum durasi boost yang bisa ditumpuk")]
    public float maxBoostDuration = 10f;

    // ── Internal State ──
    private Vector3 _velocity;
    private bool    _isGrounded;
    private bool    _wasGrounded;
    private float   _slideTimer;
    private float   _stunTimer;
    private float   _boostTimer;
    private float   _currentSpeed;
    private float   _startX;

    [Header("Start Delay / Countdown")]
    [Tooltip("Waktu persiapan sebelum mulai berlari (detik)")]
    public float startDelay = 1.5f;
    private float _startDelayTimer;

    // ── Collider original values ──
    private float _originalCCHeight;
    private float _originalCCCenterY;

    // ── Properties untuk Animation Controller ──
    public float CurrentSpeed     => _currentSpeed;
    public float VerticalVelocity => _velocity.y;
    public bool  IsGrounded       => _isGrounded;
    public bool  IsBoosted        => _boostTimer > 0f;
    public int   CurrentLane      => _currentLane;

    // ═══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════
    private void Awake()
    {
        _cc    = GetComponent<CharacterController>();
        _sm    = GetComponent<RunnerStateManager>();
        _input = new PlayerControls();

        // Simpan ukuran collider asli untuk toggle slide
        _originalCCHeight  = _cc.height;
        _originalCCCenterY = _cc.center.y;

        // Auto-configure Rigidbody agar tidak bentrok dengan CharacterController
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Jika groundMask kosong/Nothing, otomatis set ke layer "Ground"
        if (groundMask.value == 0)
        {
            groundMask = LayerMask.GetMask("Ground");
        }
    }

    private void Start()
    {
        // FPS dan VSync sekarang dikelola oleh SettingsManager
        // (Lihat GameSettings.cs dan SettingsManager.cs)

        _currentSpeed = 0f; // Mulai dari diam
        _currentLane  = 0;
        _startX       = transform.position.x; // Simpan posisi X awal sebagai acuan lane
        _startDelayTimer = startDelay;
        _sm.ForceSetState(RunnerState.Idle); // Set state awal ke Idle
    }

    private void OnEnable()
    {
        _input.Player.Enable();
        _input.Player.Jump.performed  += OnJumpInput;
        _input.Player.Slide.performed += OnSlideInput;
        _input.Player.Move.performed  += OnMoveInput;
    }

    private void OnDisable()
    {
        _input.Player.Disable();
        _input.Player.Jump.performed  -= OnJumpInput;
        _input.Player.Slide.performed -= OnSlideInput;
        _input.Player.Move.performed  -= OnMoveInput;
    }

    // ═══════════════════════════════════════════════════════════
    //  UPDATE LOOP
    // ═══════════════════════════════════════════════════════════
    private void Update()
    {
        // Jangan proses apapun saat mati
        if (_sm.CurrentState == RunnerState.Dying) return;

        // Cek jika jatuh ke luar map (di bawah batas Y)
        if (transform.position.y < fallThreshold)
        {
            TriggerDeath();
            return;
        }

        // Proses hitung mundur di awal game (Idle state)
        if (_sm.CurrentState == RunnerState.Idle)
        {
            _startDelayTimer -= Time.deltaTime;
            if (_startDelayTimer <= 0f)
            {
                _sm.TrySetState(RunnerState.Running);
            }
        }

        UpdateGroundCheck();
        HandleLanding();
        HandleSlideTimer();
        HandleStunTimer();
        HandleBoostTimer();

        // Hitung gerakan
        Vector3 moveDir = CalculateMovement();
        ApplyGravity();
        ApplyMovement(moveDir);

        BroadcastState();
    }

    // ═══════════════════════════════════════════════════════════
    //  GROUND CHECK
    // ═══════════════════════════════════════════════════════════
    private void UpdateGroundCheck()
    {
        Vector3 checkPos = groundCheck != null ? groundCheck.position : transform.position;
        _isGrounded = Physics.CheckSphere(checkPos, groundDistance, groundMask);

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f; // Snap ke tanah
    }

    // ═══════════════════════════════════════════════════════════
    //  LANDING DETECTION
    // ═══════════════════════════════════════════════════════════
    private void HandleLanding()
    {
        bool justLanded = _isGrounded && !_wasGrounded && _velocity.y <= 0f;

        if (justLanded)
        {
            // Kembali ke Running setelah landing
            _sm.TrySetState(RunnerState.Running);
            RunnerEvents.OnLanded?.Invoke();
        }

        _wasGrounded = _isGrounded;
    }

    // ═══════════════════════════════════════════════════════════
    //  SLIDE TIMER
    // ═══════════════════════════════════════════════════════════
    private void HandleSlideTimer()
    {
        if (_sm.CurrentState != RunnerState.Sliding) return;

        _slideTimer -= Time.deltaTime;
        if (_slideTimer <= 0f)
        {
            EndSlide();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  STUN TIMER
    // ═══════════════════════════════════════════════════════════
    private void HandleStunTimer()
    {
        if (_sm.CurrentState != RunnerState.Stunned) return;

        _stunTimer -= Time.deltaTime;
        if (_stunTimer <= 0f)
        {
            _sm.TrySetState(RunnerState.Running);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  BOOST TIMER
    // ═══════════════════════════════════════════════════════════
    private void HandleBoostTimer()
    {
        if (_boostTimer > 0f)
        {
            _boostTimer -= Time.deltaTime;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  CALCULATE MOVEMENT
    // ═══════════════════════════════════════════════════════════
    private Vector3 CalculateMovement()
    {
        // Tentukan kecepatan berdasarkan state
        float targetSpeed;
        switch (_sm.CurrentState)
        {
            case RunnerState.Idle:
                targetSpeed = 0f;
                break;
            case RunnerState.Sliding:
                targetSpeed = slideSpeed;
                break;
            case RunnerState.Stunned:
                targetSpeed = stunSpeed;
                break;
            default:
                targetSpeed = IsBoosted ? boostSpeed : runSpeed;
                break;
        }

        // Smooth acceleration/deceleration
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, speedAcceleration * Time.deltaTime);

        // ── Forward Movement (selalu maju, sumbu Z lokal) ──
        Vector3 forwardMove = transform.forward * _currentSpeed;

        // ── Lane Switching (geser kiri/kanan secara smooth relatif terhadap _startX) ──
        float targetX  = _startX + (_currentLane * laneDistance);
        float currentX = transform.position.x;
        float deltaX   = Mathf.MoveTowards(currentX, targetX, laneSwitchSpeed * Time.deltaTime) - currentX;

        Vector3 lateralMove = new Vector3(deltaX / Time.deltaTime, 0f, 0f);

        return forwardMove + lateralMove;
    }

    // ═══════════════════════════════════════════════════════════
    //  APPLY GRAVITY
    // ═══════════════════════════════════════════════════════════
    private void ApplyGravity()
    {
        if (!_isGrounded)
        {
            _velocity.y += gravity * Time.deltaTime;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  APPLY MOVEMENT (via CharacterController)
    // ═══════════════════════════════════════════════════════════
    private void ApplyMovement(Vector3 horizontal)
    {
        Vector3 finalMove = horizontal * Time.deltaTime;
        finalMove.y += _velocity.y * Time.deltaTime;
        _cc.Move(finalMove);
    }

    // ═══════════════════════════════════════════════════════════
    //  STATE BROADCAST
    // ═══════════════════════════════════════════════════════════
    private void BroadcastState()
    {
        // Jangan override state yang sedang dikelola timer
        if (_sm.CurrentState == RunnerState.Sliding
         || _sm.CurrentState == RunnerState.Stunned
         || _sm.CurrentState == RunnerState.Dying) return;

        // Deteksi mulai jatuh
        if (!_isGrounded && _sm.CurrentState == RunnerState.Jumping && _velocity.y < 0f)
        {
            _sm.TrySetState(RunnerState.Falling);
        }
        else if (!_isGrounded && _sm.CurrentState == RunnerState.Running)
        {
            _sm.TrySetState(RunnerState.Falling);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  INPUT HANDLERS
    // ═══════════════════════════════════════════════════════════

    // ── Move Input: HANYA kiri/kanan, abaikan maju/mundur ──
    private void OnMoveInput(InputAction.CallbackContext ctx)
    {
        if (RunnerGameManager.Instance != null && RunnerGameManager.Instance.IsPaused) return;

        // Jangan izinkan pindah lane saat mati atau saat masih bersiap (Idle)
        if (_sm.CurrentState == RunnerState.Dying || _sm.CurrentState == RunnerState.Idle) return;

        Vector2 input = ctx.ReadValue<Vector2>();

        // Threshold agar tidak terlalu sensitif
        if (input.x > 0.5f)
        {
            // Geser ke kanan
            if (_currentLane < 1)
            {
                _currentLane++;
                RunnerEvents.OnLaneChanged?.Invoke(_currentLane);
            }
        }
        else if (input.x < -0.5f)
        {
            // Geser ke kiri
            if (_currentLane > -1)
            {
                _currentLane--;
                RunnerEvents.OnLaneChanged?.Invoke(_currentLane);
            }
        }
    }

    // ── Jump ──
    private void OnJumpInput(InputAction.CallbackContext ctx)
    {
        if (RunnerGameManager.Instance != null && RunnerGameManager.Instance.IsPaused) return;

        if (_sm.CurrentState == RunnerState.Dying
         || _sm.CurrentState == RunnerState.Stunned
         || _sm.CurrentState == RunnerState.Sliding) return; // TIDAK boleh lompat saat sedang slide

        // Hanya bisa lompat saat running normal di tanah
        if (_isGrounded && _sm.CurrentState == RunnerState.Running)
        {
            PerformJump();
        }
    }

    private void PerformJump()
    {
        _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        _sm.TrySetState(RunnerState.Jumping);
        RunnerEvents.OnJumped?.Invoke();
    }

    // ── Slide ──
    private void OnSlideInput(InputAction.CallbackContext ctx)
    {
        if (RunnerGameManager.Instance != null && RunnerGameManager.Instance.IsPaused) return;

        if (!_isGrounded) return;
        if (_sm.CurrentState != RunnerState.Running) return;

        StartSlide();
    }

    private void StartSlide()
    {
        _slideTimer = slideDuration;

        // Kecilkan collider
        _cc.height = slideColliderHeight;
        // Posisikan center agar bagian bawah collider berada 0.1f di atas tanah
        // untuk menghindari clipping tembus tanah saat resizing
        float safeCenterY = 0.1f + (slideColliderHeight / 2f);
        _cc.center = new Vector3(_cc.center.x, safeCenterY, _cc.center.z);

        _sm.TrySetState(RunnerState.Sliding);
        RunnerEvents.OnSlideStarted?.Invoke();
    }

    private void EndSlide()
    {
        // Kembalikan collider ke ukuran asli
        _cc.height = _originalCCHeight;
        _cc.center = new Vector3(_cc.center.x, _originalCCCenterY, _cc.center.z);

        _sm.TrySetState(RunnerState.Running);
        RunnerEvents.OnSlideEnded?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC API (untuk sistem lain: obstacle, pickup, dll)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Panggil saat player kena halangan. Stun sebentar.
    /// </summary>
    public void TriggerStun()
    {
        if (_sm.CurrentState == RunnerState.Dying) return;

        // End slide jika sedang slide
        if (_sm.CurrentState == RunnerState.Sliding)
            EndSlide();

        // Jika sudah stunned, hanya perpanjang timer TANPA re-trigger animasi
        if (_sm.CurrentState == RunnerState.Stunned)
        {
            _stunTimer = stunDuration;
            return;
        }

        _stunTimer = stunDuration;
        _sm.ForceSetState(RunnerState.Stunned);
        RunnerEvents.OnStunned?.Invoke();
    }

    /// <summary>
    /// Panggil saat player mati (kena halangan fatal / tertangkap robot).
    /// </summary>
    public void TriggerDeath()
    {
        // End slide jika sedang slide
        if (_sm.CurrentState == RunnerState.Sliding)
            EndSlide();

        _sm.ForceSetState(RunnerState.Dying);
        RunnerEvents.OnDied?.Invoke();
    }

    /// <summary>
    /// Aktifkan speed boost dari Energy Orb.
    /// Jika boost sudah aktif, timer di-stack (ditambah, bukan di-reset).
    /// </summary>
    public void CollectEnergyOrb()
    {
        if (_boostTimer > 0f)
        {
            // Stack: tambah durasi, tidak reset
            _boostTimer = Mathf.Min(_boostTimer + boostStackBonus, maxBoostDuration);
        }
        else
        {
            // Pertama kali: set durasi penuh
            _boostTimer = boostDuration;
        }

        RunnerEvents.OnEnergyOrbCollected?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC METHODS UNTUK BUTTON UI CANVAS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Pindah lane ke kiri (dipanggil dari tombol UI Kiri)
    /// </summary>
    public void MoveLeft()
    {
        if (RunnerGameManager.Instance != null && RunnerGameManager.Instance.IsPaused) return;
        if (_sm.CurrentState == RunnerState.Dying || _sm.CurrentState == RunnerState.Idle) return;

        if (_currentLane > -1)
        {
            _currentLane--;
            RunnerEvents.OnLaneChanged?.Invoke(_currentLane);
        }
    }

    /// <summary>
    /// Pindah lane ke kanan (dipanggil dari tombol UI Kanan)
    /// </summary>
    public void MoveRight()
    {
        if (RunnerGameManager.Instance != null && RunnerGameManager.Instance.IsPaused) return;
        if (_sm.CurrentState == RunnerState.Dying || _sm.CurrentState == RunnerState.Idle) return;

        if (_currentLane < 1)
        {
            _currentLane++;
            RunnerEvents.OnLaneChanged?.Invoke(_currentLane);
        }
    }

    /// <summary>
    /// Melompat (dipanggil dari tombol UI Lompat)
    /// </summary>
    public void PressJump()
    {
        if (RunnerGameManager.Instance != null && RunnerGameManager.Instance.IsPaused) return;

        if (_sm.CurrentState == RunnerState.Dying
         || _sm.CurrentState == RunnerState.Stunned
         || _sm.CurrentState == RunnerState.Sliding) return;

        if (_isGrounded && _sm.CurrentState == RunnerState.Running)
        {
            PerformJump();
        }
    }

    /// <summary>
    /// Meluncur / Slide (dipanggil dari tombol UI Slide)
    /// </summary>
    public void PressSlide()
    {
        if (RunnerGameManager.Instance != null && RunnerGameManager.Instance.IsPaused) return;

        if (!_isGrounded) return;
        if (_sm.CurrentState != RunnerState.Running) return;

        StartSlide();
    }

    /// <summary>
    /// Collision detection untuk halangan dan pickup.
    /// Tag "EnergyOrb" = speed boost (animasi Sprint).
    /// Tag "Obstacle" = stun.
    /// Tag "DeathZone" = game over.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            TriggerStun(); // Tabrak obstacle sekarang memicu stun (membuat bot mendekat)
        }
        else if (other.CompareTag("DeathZone"))
        {
            TriggerDeath();
        }
        else if (other.CompareTag("EnergyOrb"))
        {
            CollectEnergyOrb();

            // Gunakan Object Pool jika tersedia, fallback ke Destroy
            if (PoolManager.Instance != null && PoolManager.Instance.HasPool("EnergyOrb"))
            {
                PoolManager.Instance.Despawn("EnergyOrb", other.gameObject);
            }
            else
            {
                Destroy(other.gameObject);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  DEBUG GIZMOS
    // ═══════════════════════════════════════════════════════════
    private void OnDrawGizmosSelected()
    {
        // Ground check sphere
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }

        // Lane visualization
        Gizmos.color = Color.yellow;
        Vector3 basePos = transform.position;
        for (int lane = -1; lane <= 1; lane++)
        {
            Vector3 lanePos = basePos + Vector3.right * (lane * laneDistance);
            Gizmos.DrawLine(lanePos, lanePos + transform.forward * 10f);
        }
    }
}
