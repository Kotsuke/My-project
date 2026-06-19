using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  OBJECT POOL
//  Komponen pool yang bisa dipasang pada GameObject kosong.
//  Satu ObjectPool mengelola satu jenis prefab.
//
//  Cara pakai:
//    var obj = pool.Get(position, rotation);
//    pool.Return(obj);
//
//  Otomatis expand jika pool kosong (configurable).
//  Cocok untuk: Particle, SFX AudioSource, Coin, Obstacle.
// ============================================================
public class ObjectPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [Tooltip("Prefab yang akan di-pool")]
    [SerializeField] private GameObject prefab;

    [Tooltip("Jumlah objek awal saat pool dibuat")]
    [SerializeField] private int initialSize = 10;

    [Tooltip("Apakah pool boleh membuat objek baru jika habis")]
    [SerializeField] private bool autoExpand = true;

    // ── Internal ──
    private Queue<GameObject> _available = new Queue<GameObject>();
    private List<GameObject>  _allObjects = new List<GameObject>();
    private Transform _poolParent;

    // ── Properties ──
    public GameObject Prefab => prefab;
    public int CountAvailable => _available.Count;
    public int CountTotal => _allObjects.Count;

    // ═══════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// Inisialisasi pool. Dipanggil otomatis di Awake,
    /// tapi bisa dipanggil manual jika prefab di-set dari kode.
    /// </summary>
    public void Initialize()
    {
        if (prefab == null) return;
        if (_poolParent != null) return; // Sudah di-init

        // Buat parent kosong untuk organisasi hierarki
        _poolParent = new GameObject($"Pool_{prefab.name}").transform;
        _poolParent.SetParent(transform);

        // Pre-warm: buat objek awal
        for (int i = 0; i < initialSize; i++)
        {
            CreateNewObject();
        }
    }

    /// <summary>
    /// Inisialisasi pool dari kode (tanpa Inspector).
    /// </summary>
    public void Initialize(GameObject poolPrefab, int size, bool canExpand = true)
    {
        prefab = poolPrefab;
        initialSize = size;
        autoExpand = canExpand;
        Initialize();
    }

    // ═══════════════════════════════════════════════════════════
    //  GET / RETURN
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Ambil objek dari pool. Aktifkan dan posisikan.
    /// Jika pool kosong dan autoExpand = true, buat objek baru.
    /// </summary>
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject obj = null;

        // Cari objek yang tersedia
        while (_available.Count > 0)
        {
            obj = _available.Dequeue();
            if (obj != null) break; // Objek bisa null jika sudah dihancurkan di luar pool
            obj = null;
        }

        // Jika tidak ada yang tersedia
        if (obj == null)
        {
            if (autoExpand)
            {
                obj = CreateNewObject();
            }
            else
            {
                Debug.LogWarning($"[ObjectPool] Pool '{prefab.name}' kosong dan autoExpand = false!");
                return null;
            }
        }

        // Aktifkan dan posisikan
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        return obj;
    }

    /// <summary>
    /// Ambil objek dari pool di posisi default.
    /// </summary>
    public GameObject Get()
    {
        return Get(Vector3.zero, Quaternion.identity);
    }

    /// <summary>
    /// Kembalikan objek ke pool. Nonaktifkan dan masukkan kembali ke antrian.
    /// </summary>
    public void Return(GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);
        obj.transform.SetParent(_poolParent);
        _available.Enqueue(obj);
    }

    /// <summary>
    /// Kembalikan objek ke pool setelah delay (dalam detik).
    /// Cocok untuk particle effect yang perlu bermain sampai selesai.
    /// </summary>
    public void ReturnDelayed(GameObject obj, float delay)
    {
        if (obj == null) return;
        StartCoroutine(ReturnDelayedRoutine(obj, delay));
    }

    private System.Collections.IEnumerator ReturnDelayedRoutine(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        Return(obj);
    }

    /// <summary>
    /// Kembalikan semua objek aktif ke pool.
    /// </summary>
    public void ReturnAll()
    {
        foreach (GameObject obj in _allObjects)
        {
            if (obj != null && obj.activeInHierarchy)
            {
                Return(obj);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  INTERNAL
    // ═══════════════════════════════════════════════════════════

    private GameObject CreateNewObject()
    {
        GameObject obj = Instantiate(prefab, _poolParent);
        obj.SetActive(false);
        _available.Enqueue(obj);
        _allObjects.Add(obj);
        return obj;
    }
}
