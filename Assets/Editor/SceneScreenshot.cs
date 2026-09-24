#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EcosDeLaCripta.EditorTools
{
    /// <summary>Captura una imagen de la cámara principal de una escena para verificación visual (requiere correr SIN -nographics).</summary>
    public static class SceneScreenshot
    {
        public static void CaptureLevel1() => Capture("Assets/Scenes/Level1.unity", "Assets/Art/screenshot_level1.png");
        public static void CaptureMainMenu() => Capture("Assets/Scenes/MainMenu.unity", "Assets/Art/screenshot_mainmenu.png");

        private static void Capture(string scenePath, string outPath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[SceneScreenshot] No se encontró Main Camera en " + scenePath);
                return;
            }

            int width = 1920, height = 1080;
            RenderTexture rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = prevActive;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(outPath);
            Debug.Log("[SceneScreenshot] Captura guardada en " + outPath);
        }
    }
}
#endif
