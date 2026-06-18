using UnityEngine;

// ============================================================
//  FINISH LINE
//  Mendeteksi ketika player menyentuh garis finish,
//  menghentikan player, dan memicu panel kemenangan.
// ============================================================
[RequireComponent(typeof(Collider))]
public class FinishLine : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tag untuk mendeteksi objek player")]
    [SerializeField] private string playerTag = "Player";

    private bool _hasReachedFinish = false;

    private void Start()
    {
        // Pastikan collider di-set sebagai trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Hindari pemicuan ganda jika player menyentuh berulang kali
        if (_hasReachedFinish) return;

        if (other.CompareTag(playerTag))
        {
            _hasReachedFinish = true;
            Debug.Log("[FinishLine] Player melewati garis finish!");

            // 1. Matikan kontrol movement player agar player berhenti berlari maju
            RunnerMovement movement = other.GetComponent<RunnerMovement>();
            if (movement != null)
            {
                movement.enabled = false;
                Debug.Log("[FinishLine] Komponen RunnerMovement player dimatikan.");
            }

            // 2. Set state player ke Idle agar kecepatan melambat ke nol secara halus
            RunnerStateManager stateManager = other.GetComponent<RunnerStateManager>();
            if (stateManager != null)
            {
                stateManager.ForceSetState(RunnerState.Idle);
            }

            // 3. Panggil event kemenangan di Game Manager
            if (RunnerGameManager.Instance != null)
            {
                RunnerGameManager.Instance.CompleteLevel();
            }
            else
            {
                Debug.LogError("[FinishLine] RunnerGameManager tidak ditemukan di scene!");
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Gambar visualisasi garis finish di scene view agar mudah diposisikan
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0.9f, 0.9f, 0.1f, 0.3f); // Kuning transparan

            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
}
