#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EcosDeLaCripta.EditorTools
{
    /// <summary>Utilidad temporal para inspeccionar los bounds de los modelos Kenney importados.</summary>
    public static class KenneyDiagnostics
    {
        private const string ModelsFolder = "Assets/Art/Kenney/MiniDungeon/Models";

        public static void PrintBounds()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder });
            var lines = new System.Text.StringBuilder();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                GameObject instance = Object.Instantiate(prefab);
                Bounds? bounds = null;
                foreach (var r in instance.GetComponentsInChildren<Renderer>())
                {
                    if (bounds.HasValue) bounds.Value.Encapsulate(r.bounds);
                    else bounds = r.bounds;
                }
                Object.DestroyImmediate(instance);

                string name = Path.GetFileNameWithoutExtension(path);
                if (bounds.HasValue)
                {
                    lines.AppendLine($"{name}: size={bounds.Value.size:F3} center={bounds.Value.center:F3}");
                }
                else
                {
                    lines.AppendLine($"{name}: (sin MeshFilter / sin mesh)");
                }
            }

            string outPath = "Assets/Art/Kenney/bounds_report.txt";
            File.WriteAllText(outPath, lines.ToString());
            AssetDatabase.ImportAsset(outPath);
            Debug.Log("[KenneyDiagnostics] Reporte guardado en " + outPath);
        }
    }
}
#endif
