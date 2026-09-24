#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EcosDeLaCripta.EditorTools
{
    /// <summary>Ajustes generales del proyecto (resolución de build, etc.) para Ecos de la Cripta.</summary>
    public static class ProjectSetup
    {
        [MenuItem("Ecos de la Cripta/Configurar Full HD (1920x1080)")]
        public static void SetFullHD()
        {
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Resolución de build configurada a 1920x1080 ventana.");
        }
    }
}
#endif
