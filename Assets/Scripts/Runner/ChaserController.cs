using UnityEngine;

// ============================================================
//  CHASER CONTROLLER
//  Mengelola pergerakan bot pengejar (robot/monster) di belakang player.
//
//  Mekanik ala Subway Surfers / Temple Run:
//  1. Posisi Z: Menempel di belakang Player sejauh X meter.
//  2. Posisi X: Mengikuti lane Player secara smooth (Lerp).
//  3. Dua Strike System:
//     - Jika Player terkena Stun (menabrak rintangan kecil), Bot mendekat ke Player.
//     - Jika Player terkena Stun lagi saat Bot sedang dekat, Player tertangkap (Game Over).
//     - Jika Player berhasil lari tanpa terkena stun selama beberapa detik, Bot akan mundur kembali ke posisi aman.
//  4. Stuck Detection:
//     - Jika Player menabrak dinding/rintangan fisik biasa (kecepatan maju = 0),
//       Bot akan mendeteksinya, terus berlari maju, lalu menangkap Player secara realistis.
// ============================================================
public class ChaserController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference ke transform Player")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("Reference ke script RunnerMovement Player")]
    [SerializeField] private RunnerMovement playerMovement;

    [Header("Distance Settings")]
    [Tooltip("Jarak aman bot di belakang player (meter) saat normal")]
    [SerializeField] private float normalDistance = 6f;
    [Tooltip("Jarak dekat bot di belakang player saat player tersandung/stunned")]
    [SerializeField] private float closeDistance = 2.5f;
    [Tooltip("Kecepatan bot berpindah lane mengikuti player")]
    [SerializeField] private float laneSwitchSpeed = 8f;
    [Tooltip("Kecepatan bot memperpendek/memperjauh jarak ke player")]
    [SerializeField] private float distanceChangeSpeed = 5f;

    [Header("Behavior Settings")]
    [Tooltip("Durasi bot berada di jarak dekat sebelum akhirnya mundur kembali")]
    [SerializeField] private float stayCloseDuration = 5f;

    [Header("Stuck Detection")]
    [Tooltip("Berapa lama player diam di tempat (sumbu Z) sebelum dianggap nyangkut (detik)")]
    [SerializeField] private float stuckDetectTime = 0.5f;

    // State Internal
    private float _currentDistance;
    private float _targetDistance;
    private float _stayCloseTimer;
    private bool _hasTriggeredStop;
    private Animator _animator;
    private RunnerStateManager _playerStateManager;

    // Tracker Kecepatan Nyata Player
    private float _lastPlayerZ;
    private float _stuckTimer;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        
        // Auto-find references jika tidak di-assign di Inspector
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerMovement = player.GetComponent<RunnerMovement>();
            }
        }
    }

    private void OnEnable()
    {
        // Hubungkan ke event game runner
        RunnerEvents.OnStunned += HandlePlayerStunned;
        RunnerEvents.OnDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        RunnerEvents.OnStunned -= HandlePlayerStunned;
        RunnerEvents.OnDied -= HandlePlayerDied;
    }

    private void Start()
    {
        // Inisialisasi jarak awal
        _currentDistance = normalDistance;
        _targetDistance = normalDistance;
        _hasTriggeredStop = false;
        _stuckTimer = 0f;
        
        if (playerTransform != null)
        {
            Vector3 startPos = playerTransform.position;
            startPos.z -= _currentDistance;
            transform.position = startPos;
            _lastPlayerZ = playerTransform.position.z;
        }

        // Ambil State Manager langsung dari player (lebih aman dibanding Singleton)
        if (playerMovement != null)
        {
            _playerStateManager = playerMovement.GetComponent<RunnerStateManager>();
        }

        // Set grounding agar animator memutar animasi dengan benar
        if (_animator != null)
        {
            _animator.SetBool("IsGrounded", true);
            _animator.SetFloat("VerticalVelocity", 0f);
        }
    }

    private void Update()
    {
        if (playerTransform == null || playerMovement == null || _playerStateManager == null) return;

        // Hentikan pergerakan jika player sedang mati
        if (_playerStateManager.CurrentState == RunnerState.Dying)
        {
            if (!_hasTriggeredStop)
            {
                _hasTriggeredStop = true;
                if (_animator != null)
                {
                    _animator.SetFloat("Speed", 0f);
                    _animator.SetTrigger("Die"); // Memicu transisi ke Idle
                }
            }
            return;
        }

        // Hentikan pergerakan jika player sudah mencapai finish (RunnerMovement dinonaktifkan)
        if (!playerMovement.enabled)
        {
            if (!_hasTriggeredStop)
            {
                _hasTriggeredStop = true;
                if (_animator != null)
                {
                    _animator.SetFloat("Speed", 0f);
                    _animator.SetTrigger("Die"); // Memicu transisi ke Idle
                }
            }
            return;
        }

        // Hentikan pergerakan jika game belum dimulai (countdown/Idle)
        if (_playerStateManager.CurrentState == RunnerState.Idle)
        {
            if (_animator != null)
            {
                _animator.SetFloat("Speed", 0f);
            }
            _lastPlayerZ = playerTransform.position.z; // Kunci posisi awal
            return;
        }

        // ── DETEKSI NYANGKUT ──
        // Hitung kecepatan lari maju player yang sebenarnya di dunia nyata (Z-axis)
        float actualPlayerSpeedZ = (playerTransform.position.z - _lastPlayerZ) / Time.deltaTime;
        _lastPlayerZ = playerTransform.position.z;

        // Jika player sedang berlari (harusnya maju) tapi kecepatan maju sebenarnya di bawah 1.0 m/s
        if (_playerStateManager.CurrentState == RunnerState.Running && actualPlayerSpeedZ < 1.0f)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= stuckDetectTime)
            {
                // Set target jarak ke 0 agar bot langsung mendekati player yang macet
                _targetDistance = 0f;
            }
        }
        else
        {
            // Reset timer stuck perlahan jika berjalan lancar
            _stuckTimer = Mathf.MoveTowards(_stuckTimer, 0f, Time.deltaTime);
        }

        HandleChaserLogic();
        MoveChaser();
        UpdateAnimations();
    }

    private void HandleChaserLogic()
    {
        // Timer untuk mundur kembali setelah mendekati player (jika player tidak nyangkut)
        if (_targetDistance == closeDistance)
        {
            _stayCloseTimer -= Time.deltaTime;
            if (_stayCloseTimer <= 0f)
            {
                _targetDistance = normalDistance;
            }
        }

        // Interpolasi jarak secara smooth. Jika player nyangkut, bot melesat lebih cepat.
        float currentChangeSpeed = (_targetDistance == 0f) ? distanceChangeSpeed * 1.5f : distanceChangeSpeed;
        _currentDistance = Mathf.MoveTowards(_currentDistance, _targetDistance, currentChangeSpeed * Time.deltaTime);

        // Jika bot sudah menempel dekat (jarak <= 1 meter) dengan player
        if (_currentDistance <= 1.0f)
        {
            CatchPlayer();
        }
    }

    private void MoveChaser()
    {
        // 1. Hitung posisi Z di belakang player
        float targetZ = playerTransform.position.z - _currentDistance;

        // 2. Hitung posisi X mengikuti lane player secara smooth
        float targetX = playerTransform.position.x;
        float currentX = transform.position.x;
        float nextX = Mathf.Lerp(currentX, targetX, laneSwitchSpeed * Time.deltaTime);

        // 3. Hitung posisi Y menyamakan tinggi player (agar pas lompat/turun)
        float targetY = playerTransform.position.y;

        // Terapkan posisi
        transform.position = new Vector3(nextX, targetY, targetZ);

        // Hadapkan rotasi bot ke arah depan yang sama dengan player
        transform.rotation = playerTransform.rotation;
    }

    private void HandlePlayerStunned()
    {
        // Jika bot sudah dalam posisi dekat (closeDistance) dan player stun lagi, langsung tangkap
        if (_targetDistance == closeDistance)
        {
            CatchPlayer();
        }
        else
        {
            // Jika bot masih di posisi normal, buat bot langsung mendekat
            _targetDistance = closeDistance;
            _stayCloseTimer = stayCloseDuration;
            Debug.Log("[Chaser] Player menabrak rintangan! Bot mendekat...");
        }
    }

    private void HandlePlayerDied()
    {
        // Kunci jarak saat ini saat player mati (misal karena jatuh ke jurang)
        _targetDistance = _currentDistance;
    }

    private void CatchPlayer()
    {
        if (_playerStateManager != null && _playerStateManager.CurrentState == RunnerState.Dying) return;

        Debug.Log("[Chaser] Player tertangkap oleh Bot!");
        playerMovement.TriggerDeath();
        
        // Dekatkan posisi bot langsung menempel dengan player saat mati
        _targetDistance = 0f;
    }

    private void UpdateAnimations()
    {
        if (_animator == null || _playerStateManager == null) return;

        // Speed parameter untuk animator bot:
        // 0f = Idle, 1f = Run normal, 2f = Sprint (saat mendekati player)
        float speedParam = 1f;

        if (_playerStateManager.CurrentState == RunnerState.Idle || _playerStateManager.CurrentState == RunnerState.Dying)
        {
            speedParam = 0f;
        }
        else if (_targetDistance == closeDistance || _targetDistance == 0f)
        {
            speedParam = 2f;
        }

        _animator.SetFloat("Speed", speedParam);
    }
}
