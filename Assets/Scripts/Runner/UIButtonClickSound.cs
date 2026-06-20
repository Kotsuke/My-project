using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  UI BUTTON CLICK SOUND
//  Tambahkan komponen ini ke GameObject Button manapun.
//  Otomatis memainkan suara klik via AudioManager.Instance
//  saat tombol diklik — TANPA perlu drag-drop di Inspector.
//
//  Keuntungan:
//  - Tidak rusak saat scene restart (karena pakai Instance)
//  - Tinggal Add Component, selesai.
// ============================================================
[RequireComponent(typeof(Button))]
public class UIButtonClickSound : MonoBehaviour
{
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
    }
}
