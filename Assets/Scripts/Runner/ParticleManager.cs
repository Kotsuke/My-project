using UnityEngine;

// ============================================================
//  PARTICLE MANAGER
//  Mengelola semua efek partikel game, spawn otomatis
//  berdasarkan RunnerEvents.
//
//  Setup:
//  1. Buat Empty GameObject "ParticleManager"
//  2. Add component ParticleManager
//  3. Assign player transform dan feet transform (groundCheck)
//  4. Buat particle prefab (atau gunakan default Unity particles)
//  5. Drag prefab ke slot di Inspector
//
//  Prefab yang kosong = event itu dilewati (no error).
//
//  Setiap particle prefab harus:
//  - Punya komponen ParticleSystem
//  - Set "Stop Action" ke "Disable" di ParticleSystem
//    (agar ObjectPool bisa recycle)
//  - Atau biarkan, ParticleManager akan auto-return
//    setelah durasi particle selesai
// ============================================================
public class ParticleManager : MonoBehaviour
{
    public static ParticleManager Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════
    //  REFERENCES
    // ═══════════════════════════════════════════════════════════
    [Header("Player References")]
    [Tooltip("Transform player (body center)")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Transform kaki player (groundCheck) untuk dust effects")]
    [SerializeField] private Transform playerFeet;

    // ═══════════════════════════════════════════════════════════
    //  PARTICLE PREFABS
    // ═══════════════════════════════════════════════════════════
    [Header("Movement Particles")]
    [Tooltip("Debu saat lompat (spawn di kaki)")]
    [SerializeField] private GameObject jumpDustPrefab;

    [Tooltip("Debu/impact saat mendarat (spawn di kaki)")]
    [SerializeField] private GameObject landDustPrefab;

    [Tooltip("Trail/spark saat slide (spawn di bawah player)")]
    [SerializeField] private GameObject slideTrailPrefab;

    [Header("Impact Particles")]
    [Tooltip("Flash/debris saat kena halangan (stun)")]
    [SerializeField] private GameObject stunImpactPrefab;

    [Tooltip("Efek besar saat mati")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("Pickup Particles")]
    [Tooltip("Sparkle/burst saat ambil Energy Orb")]
    [SerializeField] private GameObject orbCollectPrefab;

    [Header("Boost Particles")]
    [Tooltip("Speed lines / aura saat speed boost aktif (LOOPING particle)")]
    [SerializeField] private GameObject boostAuraPrefab;

    [Header("Lane Change Particles")]
    [Tooltip("Efek trail singkat saat pindah lane (opsional)")]
    [SerializeField] private GameObject laneChangeTrailPrefab;

    // ═══════════════════════════════════════════════════════════
    //  POOL SETTINGS
    // ═══════════════════════════════════════════════════════════
    [Header("Pool Settings")]
    [Tooltip("Jumlah instance per pool particle")]
    [SerializeField] private int poolSizePerPrefab = 3;

    // ── Internal ──
    private ObjectPool _jumpDustPool;
    private ObjectPool _landDustPool;
    private ObjectPool _slideTrailPool;
    private ObjectPool _stunImpactPool;
    private ObjectPool _deathEffectPool;
    private ObjectPool _orbCollectPool;
    private ObjectPool _laneChangePool;
    private GameObject _activeBoostAura;  // Instance boost aura yang sedang aktif
    private GameObject _activeSlideTrail; // Instance slide trail yang sedang aktif

    // ═══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-find player jika tidak di-assign
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                // Coba cari groundCheck sebagai feet
                RunnerMovement movement = player.GetComponent<RunnerMovement>();
                if (movement != null && movement.groundCheck != null)
                {
                    playerFeet = movement.groundCheck;
                }
                else
                {
                    playerFeet = playerTransform; // Fallback
                }
            }
        }

        InitializePools();
    }

    private void OnEnable()
    {
        RunnerEvents.OnJumped              += OnJumped;
        RunnerEvents.OnLanded              += OnLanded;
        RunnerEvents.OnSlideStarted        += OnSlideStarted;
        RunnerEvents.OnSlideEnded          += OnSlideEnded;
        RunnerEvents.OnStunned             += OnStunned;
        RunnerEvents.OnDied                += OnDied;
        RunnerEvents.OnEnergyOrbCollected  += OnEnergyOrbCollected;
        RunnerEvents.OnLaneChanged         += OnLaneChanged;
        RunnerEvents.OnStateChanged        += OnStateChanged;
    }

    private void OnDisable()
    {
        RunnerEvents.OnJumped              -= OnJumped;
        RunnerEvents.OnLanded              -= OnLanded;
        RunnerEvents.OnSlideStarted        -= OnSlideStarted;
        RunnerEvents.OnSlideEnded          -= OnSlideEnded;
        RunnerEvents.OnStunned             -= OnStunned;
        RunnerEvents.OnDied                -= OnDied;
        RunnerEvents.OnEnergyOrbCollected  -= OnEnergyOrbCollected;
        RunnerEvents.OnLaneChanged         -= OnLaneChanged;
        RunnerEvents.OnStateChanged        -= OnStateChanged;
    }

    private void Update()
    {
        // Update posisi boost aura agar mengikuti player
        if (_activeBoostAura != null && _activeBoostAura.activeInHierarchy && playerTransform != null)
        {
            _activeBoostAura.transform.position = playerTransform.position;
            _activeBoostAura.transform.rotation = playerTransform.rotation;
        }

        // Update posisi slide trail
        if (_activeSlideTrail != null && _activeSlideTrail.activeInHierarchy && playerFeet != null)
        {
            _activeSlideTrail.transform.position = playerFeet.position;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  POOL INITIALIZATION
    // ═══════════════════════════════════════════════════════════

    private void InitializePools()
    {
        _jumpDustPool    = CreatePoolForPrefab(jumpDustPrefab, "JumpDust");
        _landDustPool    = CreatePoolForPrefab(landDustPrefab, "LandDust");
        _slideTrailPool  = CreatePoolForPrefab(slideTrailPrefab, "SlideTrail");
        _stunImpactPool  = CreatePoolForPrefab(stunImpactPrefab, "StunImpact");
        _deathEffectPool = CreatePoolForPrefab(deathEffectPrefab, "DeathEffect");
        _orbCollectPool  = CreatePoolForPrefab(orbCollectPrefab, "OrbCollect");
        _laneChangePool  = CreatePoolForPrefab(laneChangeTrailPrefab, "LaneChange");
    }

    private ObjectPool CreatePoolForPrefab(GameObject prefab, string poolName)
    {
        if (prefab == null) return null;

        GameObject poolGO = new GameObject($"ParticlePool_{poolName}");
        poolGO.transform.SetParent(transform);
        ObjectPool pool = poolGO.AddComponent<ObjectPool>();
        pool.Initialize(prefab, poolSizePerPrefab);
        return pool;
    }

    // ═══════════════════════════════════════════════════════════
    //  SPAWN HELPERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Spawn particle dari pool, auto-return setelah durasi selesai.
    /// </summary>
    private void SpawnParticle(ObjectPool pool, Vector3 position, Quaternion rotation, float autoReturnDelay = 0f)
    {
        if (pool == null) return;

        GameObject obj = pool.Get(position, rotation);
        if (obj == null) return;

        // Play particle system
        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear();
            ps.Play();

            // Auto-return setelah particle selesai
            float duration = autoReturnDelay > 0f ? autoReturnDelay : ps.main.duration + ps.main.startLifetime.constantMax;
            pool.ReturnDelayed(obj, duration);
        }
        else
        {
            // Jika bukan ParticleSystem, return setelah 2 detik default
            pool.ReturnDelayed(obj, 2f);
        }
    }

    /// <summary>
    /// Spawn particle di posisi kaki player.
    /// </summary>
    private void SpawnAtFeet(ObjectPool pool, float autoReturn = 0f)
    {
        if (playerFeet == null) return;
        SpawnParticle(pool, playerFeet.position, Quaternion.identity, autoReturn);
    }

    /// <summary>
    /// Spawn particle di posisi body player.
    /// </summary>
    private void SpawnAtBody(ObjectPool pool, float autoReturn = 0f)
    {
        if (playerTransform == null) return;
        // Spawn sedikit di atas center agar lebih terlihat
        Vector3 bodyPos = playerTransform.position + Vector3.up * 1f;
        SpawnParticle(pool, bodyPos, playerTransform.rotation, autoReturn);
    }

    // ═══════════════════════════════════════════════════════════
    //  EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════

    private void OnJumped()
    {
        SpawnAtFeet(_jumpDustPool);
    }

    private void OnLanded()
    {
        SpawnAtFeet(_landDustPool);
    }

    private void OnSlideStarted()
    {
        // Slide trail = looping, tetap hidup sampai slide selesai
        if (_slideTrailPool != null && playerFeet != null)
        {
            _activeSlideTrail = _slideTrailPool.Get(playerFeet.position, Quaternion.identity);
            if (_activeSlideTrail != null)
            {
                ParticleSystem ps = _activeSlideTrail.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = true;
                    ps.Clear();
                    ps.Play();
                }
            }
        }
    }

    private void OnSlideEnded()
    {
        // Stop slide trail
        if (_activeSlideTrail != null)
        {
            ParticleSystem ps = _activeSlideTrail.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            // Return setelah particle yang tersisa selesai
            if (_slideTrailPool != null)
            {
                _slideTrailPool.ReturnDelayed(_activeSlideTrail, 1f);
            }
            _activeSlideTrail = null;
        }
    }

    private void OnStunned()
    {
        SpawnAtBody(_stunImpactPool);

        // Stop boost aura jika ada
        StopBoostAura();
    }

    private void OnDied()
    {
        SpawnAtBody(_deathEffectPool, 3f);

        // Cleanup semua looping effects
        StopBoostAura();
        OnSlideEnded();
    }

    private void OnEnergyOrbCollected()
    {
        SpawnAtBody(_orbCollectPool);
        StartBoostAura();
    }

    private void OnLaneChanged(int lane)
    {
        SpawnAtBody(_laneChangePool);
    }

    private void OnStateChanged(RunnerState prev, RunnerState next)
    {
        // Cek apakah boost aura perlu dimatikan
        // (Ini fallback — idealnya cek IsBoosted dari RunnerMovement)
        if (next == RunnerState.Dying || next == RunnerState.Stunned)
        {
            StopBoostAura();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  BOOST AURA (LOOPING EFFECT)
    // ═══════════════════════════════════════════════════════════

    private void StartBoostAura()
    {
        if (boostAuraPrefab == null || playerTransform == null) return;

        // Jika sudah ada boost aura, restart particle-nya
        if (_activeBoostAura != null && _activeBoostAura.activeInHierarchy)
        {
            ParticleSystem ps = _activeBoostAura.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
            return;
        }

        // Spawn boost aura baru (tidak pakai pool, cukup 1 instance)
        _activeBoostAura = Instantiate(boostAuraPrefab, playerTransform.position, playerTransform.rotation);
        _activeBoostAura.transform.SetParent(null); // Jangan child ke player agar rotation independent

        ParticleSystem auraPS = _activeBoostAura.GetComponent<ParticleSystem>();
        if (auraPS != null)
        {
            var main = auraPS.main;
            main.loop = true;
            auraPS.Play();
        }
    }

    private void StopBoostAura()
    {
        if (_activeBoostAura == null) return;

        ParticleSystem ps = _activeBoostAura.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // Destroy setelah particle yang tersisa selesai
        Destroy(_activeBoostAura, 2f);
        _activeBoostAura = null;
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC API (untuk spawn manual dari script lain)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Spawn particle efek di posisi tertentu (untuk keperluan custom).
    /// </summary>
    public void SpawnEffect(GameObject prefab, Vector3 position, float duration = 2f)
    {
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, position, Quaternion.identity);
        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play();
        }
        Destroy(obj, duration);
    }
}
