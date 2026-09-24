#if UNITY_EDITOR
using EcosDeLaCripta.Audio;
using EcosDeLaCripta.Enemy;
using EcosDeLaCripta.Environment;
using EcosDeLaCripta.Items;
using EcosDeLaCripta.Player;
using EcosDeLaCripta.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EcosDeLaCripta.EditorTools
{
    public static class Level1Validator
    {
        public static void Validate()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity", OpenSceneMode.Single);
            Require(Object.FindFirstObjectByType<PlayerController>() != null, "Falta PlayerController");
            CameraFollow cameraRig = Object.FindFirstObjectByType<CameraFollow>();
            Require(cameraRig != null, "Falta CameraFollow");
            Require(Object.FindFirstObjectByType<HUDController>() != null, "Falta HUDController");
            Require(Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).Length == 14, "El nivel debe tener 14 enemigos");
            Require(Object.FindObjectsByType<EnemyHealthBar>(FindObjectsSortMode.None).Length == 14, "Todos los enemigos deben tener barra de vida");
            Require(Object.FindObjectsByType<KillGate>(FindObjectsSortMode.None).Length == 4, "El nivel debe tener 4 sellos");
            Require(Object.FindObjectsByType<SpikeTrap>(FindObjectsSortMode.None).Length == 8, "El nivel debe tener 8 trampas");
            Require(Object.FindObjectsByType<PowerUp>(FindObjectsSortMode.None).Length == 7, "El nivel debe tener 7 power-ups visibles");

            string[] solidProps = { "barrel", "table", "chair", "chest", "rocks", "stones", "wood-structure", "shield-round", "shield-rectangle", "weapon-sword", "Torch" };
            Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (string propName in solidProps)
            {
                int matches = 0;
                foreach (Transform candidate in allTransforms)
                {
                    if (candidate.name != propName) continue;
                    matches++;
                    Require(candidate.GetComponent<Collider>() != null, $"Una instancia de {propName} no tiene colisión");
                }
                Require(matches > 0, $"No se encontró ninguna instancia de {propName}");
            }

            foreach (Transform candidate in allTransforms)
            {
                if (candidate.name != "wall" && candidate.name != "column") continue;
                Collider collider = candidate.GetComponent<Collider>();
                Require(collider != null && collider.bounds.size.y >= 3f, $"{candidate.name} no tiene altura de seguridad suficiente");
            }

            foreach (KillGate gate in Object.FindObjectsByType<KillGate>(FindObjectsSortMode.None))
                Require(gate.GetComponent<Collider>().bounds.size.y >= 3.5f, "Un sello se puede saltar");

            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            SerializedObject serializedPlayer = new SerializedObject(player);
            Require(serializedPlayer.FindProperty("projectilePrefab").objectReferenceValue != null, "El proyectil del jugador no está enlazado");
            Require(AssetDatabase.GetAssetPath(serializedPlayer.FindProperty("shootClip").objectReferenceValue) == "Assets/Audio/Disparo.mp3",
                "El disparo no usa DISPARO.mp3");

            SerializedObject serializedCamera = new SerializedObject(cameraRig);
            Require(serializedCamera.FindProperty("target").objectReferenceValue != null, "La cámara no tiene jugador asignado");
            Require(serializedCamera.FindProperty("crosshair").objectReferenceValue != null, "La primera persona no tiene mira enlazada");
            Require(serializedCamera.FindProperty("perspectiveToast").objectReferenceValue != null, "Falta el aviso de cambio de perspectiva");
            DungeonAudioDirector audioDirector = Object.FindFirstObjectByType<DungeonAudioDirector>();
            Require(audioDirector != null, "Falta la música dinámica del nivel");
            SerializedObject serializedAudio = new SerializedObject(audioDirector);
            Require(AssetDatabase.GetAssetPath(serializedAudio.FindProperty("ambientClip").objectReferenceValue) == "Assets/Audio/Ambiente.mp3",
                "El nivel no usa AMBIENTE.mp3");
            Require(AssetDatabase.GetAssetPath(serializedAudio.FindProperty("bossClip").objectReferenceValue) == "Assets/Audio/BossFinal.mp3",
                "El jefe no usa BOSS_FINAL.mp3");
            PlayerDamageOverlay damageOverlay = Object.FindFirstObjectByType<PlayerDamageOverlay>();
            Require(damageOverlay != null, "Falta el destello rojo de daño del jugador");
            SerializedObject serializedDamage = new SerializedObject(damageOverlay);
            Require(AssetDatabase.GetAssetPath(serializedDamage.FindProperty("hurtClip").objectReferenceValue) == "Assets/Audio/Dano.mp3",
                "El daño recibido no usa DAÑO.mp3");

            EnemyAI boss = null;
            foreach (EnemyAI enemy in Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                if (enemy.isBoss) boss = enemy;
            Require(boss != null, "Falta el jefe final");
            SerializedObject serializedBoss = new SerializedObject(boss);
            Require(serializedBoss.FindProperty("constrainToArena").boolValue, "El jefe puede bloquear la entrada de la arena");

            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            DungeonAudioDirector menuAudio = Object.FindFirstObjectByType<DungeonAudioDirector>();
            Require(menuAudio != null, "El menú no inicia la música ambiental persistente");
            SerializedObject serializedMenuAudio = new SerializedObject(menuAudio);
            Require(AssetDatabase.GetAssetPath(serializedMenuAudio.FindProperty("ambientClip").objectReferenceValue) == "Assets/Audio/Ambiente.mp3",
                "El menú no usa AMBIENTE.mp3");

            Debug.Log("[Level1Validator] OK: audios personalizados enlazados y persistentes, colisiones completas, muros altos, entrada del jefe libre y feedback de daño.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException("[Level1Validator] " + message);
        }
    }
}
#endif
