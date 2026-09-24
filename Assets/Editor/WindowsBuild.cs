#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EcosDeLaCripta.EditorTools
{
    public static class WindowsBuild
    {
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Level1.unity"
        };

        [MenuItem("Ecos de la Cripta/Crear ejecutable Windows")]
        public static void Build()
        {
            foreach (string scene in Scenes)
            {
                if (!File.Exists(scene))
                    throw new FileNotFoundException("Falta una escena necesaria para el juego", scene);
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputDirectory = Path.Combine(projectRoot, "Entregables", "EcosDeLaCripta_Windows");
            Directory.CreateDirectory(outputDirectory);
            string executable = Path.Combine(outputDirectory, "EcosDeLaCripta.exe");

            PlayerSettings.productName = "Ecos de la Cripta";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;

            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[WindowsBuild] Estado: {report.summary.result}. " +
                      $"Errores: {report.summary.totalErrors}. Destino: {executable}");
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(executable))
                throw new InvalidOperationException("La compilación de Windows no se completó correctamente.");
        }
    }
}
#endif
