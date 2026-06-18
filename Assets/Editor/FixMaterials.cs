using UnityEditor;
using UnityEngine;

// ============================================================
//  FIX MATERIALS EDITOR SCRIPT
//  Script Editor untuk mengubah Shader Material 2D menjadi
//  URP Lit Shader agar tidak render hitam/rusak di URP 3D.
// ============================================================
public class FixMaterials
{
    [MenuItem("Tools/Fix URP Materials")]
    static void Fix()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material");

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");

        if (litShader == null)
        {
            Debug.LogError("[FixMaterials] Shader 'Universal Render Pipeline/Lit' tidak ditemukan! Pastikan proyek menggunakan URP.");
            return;
        }

        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat != null && mat.shader != null && mat.shader.name.Contains("2D"))
            {
                mat.shader = litShader;
                EditorUtility.SetDirty(mat);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[FixMaterials] Done! Berhasil memperbaiki {count} material.");
    }
}
