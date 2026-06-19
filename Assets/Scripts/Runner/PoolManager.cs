using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  POOL MANAGER
//  Singleton registry untuk semua Object Pool di scene.
//
//  Cara pakai dari script lain:
//    // Spawn objek dari pool
//    GameObject obj = PoolManager.Instance.Spawn("EnergyOrb", pos, rot);
//
//    // Kembalikan ke pool
//    PoolManager.Instance.Despawn("EnergyOrb", obj);
//
//    // Kembalikan ke pool setelah delay
//    PoolManager.Instance.DespawnDelayed("JumpDust", obj, 2f);
//
//  Setup di Inspector:
//  1. Buat Empty GameObject "PoolManager"
//  2. Add component PoolManager
//  3. Tambahkan entry di poolEntries (tag + prefab + size)
// ============================================================
public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    [System.Serializable]
    public class PoolEntry
    {
        [Tooltip("Nama unik untuk pool ini (contoh: 'EnergyOrb', 'JumpDust')")]
        public string tag;

        [Tooltip("Prefab yang akan di-pool")]
        public GameObject prefab;

        [Tooltip("Jumlah objek awal")]
        public int initialSize = 10;
    }

    [Header("Pool Entries")]
    [Tooltip("Daftar semua pool yang akan dibuat saat game mulai")]
    [SerializeField] private List<PoolEntry> poolEntries = new List<PoolEntry>();

    // ── Internal ──
    private Dictionary<string, ObjectPool> _pools = new Dictionary<string, ObjectPool>();

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

        InitializePools();
    }

    private void InitializePools()
    {
        foreach (PoolEntry entry in poolEntries)
        {
            if (string.IsNullOrEmpty(entry.tag) || entry.prefab == null)
            {
                Debug.LogWarning("[PoolManager] Pool entry tidak valid (tag/prefab kosong). Dilewati.");
                continue;
            }

            if (_pools.ContainsKey(entry.tag))
            {
                Debug.LogWarning($"[PoolManager] Duplikat pool tag '{entry.tag}'. Dilewati.");
                continue;
            }

            CreatePool(entry.tag, entry.prefab, entry.initialSize);
        }

        Debug.Log($"[PoolManager] {_pools.Count} pool berhasil dibuat.");
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Buat pool baru secara runtime (jika belum ada di Inspector).
    /// </summary>
    public ObjectPool CreatePool(string tag, GameObject prefab, int initialSize)
    {
        if (_pools.ContainsKey(tag))
        {
            Debug.LogWarning($"[PoolManager] Pool '{tag}' sudah ada!");
            return _pools[tag];
        }

        // Buat child GameObject untuk pool ini
        GameObject poolGO = new GameObject($"Pool_{tag}");
        poolGO.transform.SetParent(transform);

        ObjectPool pool = poolGO.AddComponent<ObjectPool>();
        pool.Initialize(prefab, initialSize);

        _pools.Add(tag, pool);
        return pool;
    }

    /// <summary>
    /// Ambil objek dari pool berdasarkan tag.
    /// </summary>
    public GameObject Spawn(string tag, Vector3 position, Quaternion rotation)
    {
        if (!_pools.ContainsKey(tag))
        {
            Debug.LogWarning($"[PoolManager] Pool '{tag}' tidak ditemukan!");
            return null;
        }

        return _pools[tag].Get(position, rotation);
    }

    /// <summary>
    /// Ambil objek dari pool berdasarkan tag (posisi default).
    /// </summary>
    public GameObject Spawn(string tag)
    {
        return Spawn(tag, Vector3.zero, Quaternion.identity);
    }

    /// <summary>
    /// Kembalikan objek ke pool.
    /// </summary>
    public void Despawn(string tag, GameObject obj)
    {
        if (!_pools.ContainsKey(tag))
        {
            Debug.LogWarning($"[PoolManager] Pool '{tag}' tidak ditemukan! Menghancurkan objek...");
            Destroy(obj);
            return;
        }

        _pools[tag].Return(obj);
    }

    /// <summary>
    /// Kembalikan objek ke pool setelah delay (detik).
    /// </summary>
    public void DespawnDelayed(string tag, GameObject obj, float delay)
    {
        if (!_pools.ContainsKey(tag))
        {
            Debug.LogWarning($"[PoolManager] Pool '{tag}' tidak ditemukan! Menghancurkan objek...");
            Destroy(obj, delay);
            return;
        }

        _pools[tag].ReturnDelayed(obj, delay);
    }

    /// <summary>
    /// Cek apakah pool dengan tag tertentu ada.
    /// </summary>
    public bool HasPool(string tag)
    {
        return _pools.ContainsKey(tag);
    }

    /// <summary>
    /// Dapatkan pool berdasarkan tag (untuk akses langsung).
    /// </summary>
    public ObjectPool GetPool(string tag)
    {
        return _pools.ContainsKey(tag) ? _pools[tag] : null;
    }
}
