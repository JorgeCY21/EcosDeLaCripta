#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EcosDeLaCripta.EditorTools
{
    [InitializeOnLoad]
    public static class AutoRebuildLevel
    {
        private const string FlagPath = "Temp/rebuild_level1.flag";

        static AutoRebuildLevel()
        {
            if (!File.Exists(FlagPath)) return;
            File.Delete(FlagPath);
            EditorApplication.delayCall += Rebuild;
        }

        private static void Rebuild()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += Rebuild;
                return;
            }
            LevelBuilder.BuildLevel1();
            SceneScreenshot.CaptureLevel1();
            Debug.Log("[AutoRebuildLevel] Nivel renovado y captura actualizada.");
        }
    }
}
#endif
