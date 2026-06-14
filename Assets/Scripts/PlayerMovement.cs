using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
//  PLAYER MOVEMENT
//  Tanggung jawab: input, physics, ground check, jump, slide.
//  TIDAK tahu apapun tentang animasi — semua lewat event/state.
//
//  Arsitektur:
//  - Membaca input dari PlayerControls (Input System)
//  - Mendelegasikan wall run ke WallRunHandler
//  - Mendelegasikan fall tracking ke FallTracker
//  - Semua perubahan state lewat PlayerStateMachine
// ============================================================
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(WallRunHandler))]
[RequireComponent(typeof(FallTracker))]
public class PlayerMovement : MonoBehaviour
{
    // ── Komponen ──
    private CharacterController _cc;
    private PlayerStateMachine  _sm;
    private WallRunHandler      _wallRun;
    private FallTracker         _fallTracker;
    private PlayerControls      _input;

    [Header("Speed")]
    public float walkSpeed   = 6f;
    public float runSpeed    = 8f;
    public float sprintSpeed = 13f;

    [Header("Jump")]
    public float jumpHeight  = 2.5f;
    public float gravity     = -22f;

    [Header("Double Jump")]
    public int maxJumps = 2;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float     groundDistance = 0.3f;
    public LayerMask groundMask;

    [Header("Camera")]
    public Transform cameraTransform;
    public float     rotationSpeed = 14f;

    [Header("Slide")]
    [Tooltip("Durasi slide dalam detik")]
    public float slideDuration = 0.6f;
    [Tooltip("Kecepatan saat slide")]
    public float slideSpeed    = 16f;

    // ── State gerak ──
    private Vector3 _velocity;
    private Vector2 _moveInput;
    private bool    _isGrounded;
    private bool    _wasGrounded;
    private int     _jumpsRemaining;
    private bool    _forceRollOnLand;    // setelah double jump
    private float   _slideTimer;
    private Vector3 _slideDirection;

    // Property publik untuk komponen lain (WallRunHandler, AnimController)
    public float HorizontalSpeed   => new Vector3(_velocity.x, 0, _velocity.z).magnitude;
    public float VerticalVelocity  => _velocity.y;
    public bool  IsGrounded        => _isGrounded;
    public bool  IsSprinting       => _input != null && _input.Player.Dash.IsPressed()
                                   && _moveInput.magnitude > 0.1f;
    public bool  IsMoving          => _moveInput.magnitude > 0.1f;
    public float NormalizedSpeed
    {
        get
        {
            if (!IsMoving) return 0f;
            return IsSprinting ? 2f : 1f;
        }
    }

    // ── Lifecycle ──
    private void Awake()
    {
        _cc          = GetComponent<CharacterController>();
        _sm          = GetComponent<PlayerStateMachine>();
        _wallRun     = GetComponent<WallRunHandler>();
        _fallTracker = GetComponent<FallTracker>();
        _input       = new PlayerControls();
    }

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        _jumpsRemaining = maxJumps;
    }

    private void OnEnable()
    {
        _input.Player.Enable();
        _input.Player.Jump.performed  += OnJumpInput;
        _input.Player.Slide.performed += OnSlideInput;
    }

    private void OnDisable()
    {
        _input.Player.Disable();
        _input.Player.Jump.performed  -= OnJumpInput;
        _input.Player.Slide.performed -= OnSlideInput;
    }

    private void Update()
    {
        // Jangan proses input saat mati
        if (_sm.CurrentState == PlayerState.Dying) return;

        UpdateGroundCheck();
        ReadInput();
        HandleLandingDetection();
        HandleSlideTimer();

        // Update info untuk komponen lain
        _wallRun.CurrentHorizontalSpeed = HorizontalSpeed;
        _fallTracker.IsSprinting        = IsSprinting;
        _fallTracker.IsMoving           = IsMoving;

        // Update wall run system
        _wallRun.Tick(Time.deltaTime, _isGrounded, _moveInput.y);

        // Inject wall jump velocity jika ada
        if (_wallRun.HasWallJumpVelocity)
        {
            _velocity = _wallRun.WallJumpVelocity;
            _wallRun.ConsumeWallJumpVelocity();
        }

        // Kalkulasi gerakan
        Vector3 horizontalMove = CalculateHorizontalMovement();
        ApplyGravity();
        ApplyMovement(horizontalMove);
        UpdateRotation(horizontalMove);
        BroadcastMovementState();
    }

    // ── Ground Check ──
    private void UpdateGroundCheck()
    {
        Vector3 checkPos = groundCheck != null ? groundCheck.position : transform.position;
        _isGrounded = Physics.CheckSphere(checkPos, groundDistance, groundMask);

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;  // snapping ke tanah
    }

    // ── Baca input ──
    private void ReadInput()
    {
        _moveInput = _input.Player.Move.ReadValue<Vector2>();
    }

    // ── Deteksi landing dan trigger event yang tepat ──
    private void HandleLandingDetection()
    {
        // Menggunakan _velocity.y <= 0f agar tidak terdeteksi mendarat saat melompat naik (ground check flickering)
        bool justLanded = _isGrounded && !_wasGrounded && _velocity.y <= 0f;
        bool justLeftGround = !_isGrounded && _wasGrounded;

        if (justLeftGround)
        {
            _fallTracker.BeginTracking(transform.position.y);
            // Jika lepas landas tanpa melompat (misal jalan keluar dari tebing), kurangi sisa lompatan
            if (_sm.CurrentState != PlayerState.Jump 
             && _sm.CurrentState != PlayerState.DoubleJump 
             && _sm.CurrentState != PlayerState.WallJump)
            {
                _jumpsRemaining = Mathf.Max(_jumpsRemaining, maxJumps - 1);
            }
        }

        if (justLanded)
        {
            _jumpsRemaining = maxJumps;

            if (_forceRollOnLand)
            {
                // Setelah double jump, selalu roll
                _forceRollOnLand = false;
                HandleLandingState(LandingType.Roll);
            }
            else
            {
                LandingType landing = _fallTracker.EvaluateLanding(transform.position.y);
                HandleLandingState(landing);
            }

            PlayerEvents.OnLanded?.Invoke(_fallTracker.LastFallDistance);
        }
        else if (_isGrounded)
        {
            _fallTracker.TrackPeak(transform.position.y);
        }
        else
        {
            _fallTracker.TrackPeak(transform.position.y);
        }

        _wasGrounded = _isGrounded;
    }

    private void HandleLandingState(LandingType landing)
    {
        switch (landing)
        {
            case LandingType.Roll:
                _sm.TrySetState(PlayerState.LandingRoll);
                break;
            case LandingType.Hard:
                _sm.TrySetState(PlayerState.HardLanding);
                break;
            case LandingType.LandToRun:
                _sm.TrySetState(PlayerState.LandToRun);
                break;
            default:
                // Normal landing — langsung ke Idle atau Run
                _sm.TrySetState(IsMoving ? PlayerState.Run : PlayerState.Idle);
                break;
        }
    }

    // ── Slide timer: state slide selesai setelah durasi habis ──
    private void HandleSlideTimer()
    {
        if (_sm.CurrentState != PlayerState.Slide) return;

        _slideTimer -= Time.deltaTime;
        if (_slideTimer <= 0f)
        {
            _sm.TrySetState(IsMoving ? PlayerState.Run : PlayerState.Idle);
        }
    }

    // ── Hitung arah gerak horizontal ──
    private Vector3 CalculateHorizontalMovement()
    {
        // ── Landing Roll: diam total dari script (animasi yang menggerakkan via Root Motion) ──
        if (_sm.CurrentState == PlayerState.LandingRoll)
            return Vector3.zero;

        // ── LandToRun: diam total dari script (animasi yang menggerakkan via Root Motion) ──
        if (_sm.CurrentState == PlayerState.LandToRun)
            return Vector3.zero;

        // ── HardLanding: diam total ──
        if (_sm.CurrentState == PlayerState.HardLanding)
            return Vector3.zero;

        // ── Slide: pakai slideTimer yang di-set saat mulai slide ──
        if (_sm.CurrentState == PlayerState.Slide)
        {
            float t = Mathf.Clamp01(1f - (_slideTimer / slideDuration));
            float speed = Mathf.Lerp(slideSpeed, runSpeed * 0.5f, t);
            return _slideDirection * speed;
        }

        // ── Wall Running: maju searah dinding ──
        if (_sm.IsWallRunning)
            return transform.forward * sprintSpeed * _wallRun.wallRunSpeedBoost;

        if (_moveInput.magnitude < 0.1f) return Vector3.zero;

        // ── Gerak normal relatif terhadap kamera ──
        Vector3 move;
        if (cameraTransform != null)
        {
            Vector3 camFwd   = Vector3.Scale(cameraTransform.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 camRight = cameraTransform.right;
            move = camRight * _moveInput.x + camFwd * _moveInput.y;
        }
        else
        {
            move = transform.right * _moveInput.x + transform.forward * _moveInput.y;
        }

        float moveSpeed = IsSprinting ? sprintSpeed : runSpeed;
        return move.normalized * moveSpeed;
    }

    // ── Baca normalized time animator layer 0 (0.0-1.0) ──
    private Animator _cachedAnimator;
    private float GetAnimatorNormalizedTime(string expectedStateName)
    {
        if (_cachedAnimator == null)
            _cachedAnimator = GetComponent<Animator>();
        if (_cachedAnimator == null) return 0.5f;

        // Jika animator sedang dalam transisi, kembalikan 0f agar tidak terburu-buru keluar dari state
        if (_cachedAnimator.IsInTransition(0))
            return 0f;

        AnimatorStateInfo stateInfo = _cachedAnimator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(expectedStateName))
            return 0f; // Animator belum sampai ke state yang dituju

        return Mathf.Clamp01(stateInfo.normalizedTime % 1f);
    }

    // ── Terapkan gravitasi ──
    private void ApplyGravity()
    {
        if (_sm.IsWallRunning)
        {
            _velocity.y = _wallRun.GetWallGravity();
        }
        else
        {
            _velocity.y += gravity * Time.deltaTime;
        }
    }

    // ── Jalankan CharacterController.Move ──
    private void ApplyMovement(Vector3 horizontal)
    {
        _cc.Move(horizontal * Time.deltaTime);
        _cc.Move(_velocity  * Time.deltaTime);
    }

    // ── Rotasi karakter menghadap arah gerak ──
    private void UpdateRotation(Vector3 moveDir)
    {
        if (_sm.IsActionLocked || _sm.IsWallRunning) return;
        if (moveDir.sqrMagnitude < 0.01f) return;

        Quaternion target = Quaternion.LookRotation(moveDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
    }

    // ── Siarkan state gerakan ke state machine ──
    private void BroadcastMovementState()
    {
        // Terminal / in-flight states: tidak di-override dari sini
        if (_sm.CurrentState == PlayerState.Dying
         || _sm.CurrentState == PlayerState.Stunned
         || _sm.IsWallRunning) return;

        // Jika tidak menempel di tanah (aerial), tapi berada dalam state grounded, slide, atau landing, transition ke Falling
        if (!_isGrounded)
        {
            if (_sm.IsGrounded || _sm.IsInLandingAnim || _sm.CurrentState == PlayerState.Slide)
            {
                _sm.TrySetState(PlayerState.Falling);
                return;
            }

            // Jika dalam state airborne (Jump, DoubleJump, WallJump) dan mulai turun
            if ((_sm.CurrentState == PlayerState.Jump || _sm.CurrentState == PlayerState.DoubleJump || _sm.CurrentState == PlayerState.WallJump)
                && _velocity.y < -0.5f)
            {
                _sm.TrySetState(PlayerState.Falling);
                return;
            }
        }

        // Landing anim states: cek apakah animasi sudah selesai (normalizedTime >= 0.95)
        // Jika selesai, keluarkan ke Idle/Run. Jika belum, biarkan animasi jalan.
        if (_sm.IsInLandingAnim)
        {
            string expectedStateName = "";
            if (_sm.CurrentState == PlayerState.LandingRoll) expectedStateName = "LandingRoll";
            else if (_sm.CurrentState == PlayerState.HardLanding) expectedStateName = "HardLanding";
            else if (_sm.CurrentState == PlayerState.LandToRun) expectedStateName = "LandToRun";

            float animT = GetAnimatorNormalizedTime(expectedStateName);
            if (animT >= 0.95f)
            {
                // Animasi landing selesai: kembali ke state gerak normal
                _sm.TrySetState(IsMoving ? PlayerState.Run : PlayerState.Idle);
            }
            return; // landing masih jalan, jangan override
        }

        // Airborne states yang dikelola oleh jump/landing detection — jangan override
        if (_sm.CurrentState == PlayerState.Jump
         || _sm.CurrentState == PlayerState.DoubleJump
         || _sm.CurrentState == PlayerState.WallJump) return;

        if (_sm.CurrentState == PlayerState.Falling)
        {
            // Hanya override falling jika sudah landed (ditangani HandleLandingDetection)
            return;
        }

        // Slide dikelola oleh HandleSlideTimer — jangan override di sini
        if (_sm.CurrentState == PlayerState.Slide) return;

        // ── State grounded normal: update Idle/Run/Sprint sesuai input ──
        if (!_isGrounded)
        {
            if (_velocity.y < -0.5f)
                _sm.TrySetState(PlayerState.Falling);
            return;
        }

        if (_moveInput.magnitude < 0.1f)
            _sm.TrySetState(PlayerState.Idle);
        else if (IsSprinting)
            _sm.TrySetState(PlayerState.Sprint);
        else
            _sm.TrySetState(PlayerState.Run);
    }

    // ── Handler input jump ──
    private void OnJumpInput(InputAction.CallbackContext ctx)
    {
        // Blok hanya dari state yang benar-benar tidak boleh loncat
        // Bug lama: IsActionLocked mencakup landing state yang sudah selesai animasinya
        if (_sm.CurrentState == PlayerState.Dying
         || _sm.CurrentState == PlayerState.Stunned) return;

        // Saat animasi landing masih berjalan (belum 95%), blok jump
        if (_sm.IsInLandingAnim)
        {
            string expectedStateName = "";
            if (_sm.CurrentState == PlayerState.LandingRoll) expectedStateName = "LandingRoll";
            else if (_sm.CurrentState == PlayerState.HardLanding) expectedStateName = "HardLanding";
            else if (_sm.CurrentState == PlayerState.LandToRun) expectedStateName = "LandToRun";

            if (GetAnimatorNormalizedTime(expectedStateName) < 0.95f) return;
        }

        // Slide: blok jump (atau bisa dibuat slide-cancel, tergantung desain)
        if (_sm.CurrentState == PlayerState.Slide) return;

        if (_sm.IsWallRunning)
        {
            // Wall jump
            _wallRun.ExecuteWallJump();
            return;
        }

        if (_isGrounded)
        {
            PerformJump(false);
        }
        else if (_jumpsRemaining > 0 && _sm.CurrentState != PlayerState.DoubleJump)
        {
            PerformJump(true);
        }
    }

    private void PerformJump(bool isDouble)
    {
        _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        _jumpsRemaining--;

        if (isDouble)
        {
            _forceRollOnLand = true;
            _sm.TrySetState(PlayerState.DoubleJump);
            PlayerEvents.OnDoubleJumped?.Invoke();
        }
        else
        {
            _sm.TrySetState(PlayerState.Jump);
            PlayerEvents.OnJumped?.Invoke();
        }
    }

    // ── Handler input slide ──
    private void OnSlideInput(InputAction.CallbackContext ctx)
    {
        if (!_isGrounded) return;
        if (_sm.IsActionLocked || _sm.IsWallRunning) return;
        if (_moveInput.magnitude < 0.1f) return;

        // Simpan arah slide saat ini
        if (cameraTransform != null)
        {
            Vector3 camFwd   = Vector3.Scale(cameraTransform.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 camRight = cameraTransform.right;
            _slideDirection = (camRight * _moveInput.x + camFwd * _moveInput.y).normalized;
        }
        else
        {
            _slideDirection = transform.forward;
        }

        _slideTimer = slideDuration;
        _sm.TrySetState(PlayerState.Slide);
        PlayerEvents.OnSlideStarted?.Invoke();
    }

    // ── API publik untuk sistem lain (misalnya damage system) ──
    public void SetStunned(bool stunned)
    {
        if (stunned) _sm.TrySetState(PlayerState.Stunned);
        else if (_sm.CurrentState == PlayerState.Stunned)
            _sm.TrySetState(PlayerState.Idle);

        PlayerEvents.OnStunnedChanged?.Invoke(stunned);
    }

    public void Die()
    {
        _sm.ForceSetState(PlayerState.Dying);
        PlayerEvents.OnDied?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
    }
}