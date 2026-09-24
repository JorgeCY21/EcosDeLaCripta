#if UNITY_EDITOR
using System;
using System.Reflection;
using EcosDeLaCripta.Audio;
using EcosDeLaCripta.Core;
using EcosDeLaCripta.Items;
using EcosDeLaCripta.Player;
using EcosDeLaCripta.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EcosDeLaCripta.EditorTools
{
    /// <summary>Prueba breve en Play Mode para perspectiva, mira, cuerpo y partículas de power-up.</summary>
    [InitializeOnLoad]
    public static class GameplaySmokeTest
    {
        private const string RunningKey = "Ecos.GameplaySmoke.Running";
        private const string FailedKey = "Ecos.GameplaySmoke.Failed";
        private const string StageKey = "Ecos.GameplaySmoke.Stage";
        private static int frames;
        private static float frameTimeSum;
        private static int measuredFrames;

        static GameplaySmokeTest()
        {
            if (SessionState.GetBool(RunningKey, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            SessionState.SetBool(RunningKey, true);
            SessionState.SetBool(FailedKey, false);
            SessionState.SetInt(StageKey, 0);
            frames = 0;
            frameTimeSum = 0f;
            measuredFrames = 0;
            EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity", OpenSceneMode.Single);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (EditorApplication.isPlaying && stage == 0)
            {
                frames++;
                if (frames < 12) return;
                try
                {
                    Application.logMessageReceived += CaptureLog;
                    CameraFollow rig = UnityEngine.Object.FindFirstObjectByType<CameraFollow>();
                    Require(rig != null, "No se encontró CameraFollow en Play Mode");
                    rig.SetFirstPersonMode(true);
                    Require(!Camera.main.orthographic, "La cámara no cambió a perspectiva");
                    GameObject crosshair = GameObject.Find("FirstPersonCrosshair");
                    Require(crosshair != null && crosshair.activeInHierarchy, "La mira no se activó");

                    GameObject model = GameObject.Find("Player/Model");
                    if (model == null)
                    {
                        PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                        model = player != null ? player.transform.Find("Model")?.gameObject : null;
                    }
                    Require(model != null, "No se encontró el cuerpo del jugador");
                    foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                        Require(!renderer.enabled, "El cuerpo del jugador tapa la vista en primera persona");

                    PowerUp powerUp = UnityEngine.Object.FindFirstObjectByType<PowerUp>();
                    Require(powerUp != null, "No se encontró un power-up para probar partículas");
                    MethodInfo burst = typeof(PowerUp).GetMethod("SpawnPickupBurst", BindingFlags.Instance | BindingFlags.NonPublic);
                    Require(burst != null, "No se encontró SpawnPickupBurst");
                    burst.Invoke(powerUp, null);

                    DungeonAudioDirector audioDirector = UnityEngine.Object.FindFirstObjectByType<DungeonAudioDirector>();
                    Require(audioDirector != null, "No se encontró el director musical");
                    AudioSource[] musicSources = audioDirector.GetComponentsInChildren<AudioSource>();
                    Require(musicSources.Length == 2 && musicSources[0].isPlaying && musicSources[0].clip.name == "Ambiente",
                        "AMBIENTE.mp3 no está reproduciéndose");
                    Require(musicSources[1].clip != null && musicSources[1].clip.name == "BossFinal",
                        "BOSS_FINAL.mp3 no está preparado");

                    PlayerDamageOverlay damageOverlay = UnityEngine.Object.FindFirstObjectByType<PlayerDamageOverlay>();
                    Require(damageOverlay != null, "No se encontró el feedback rojo de daño");
                    Health playerHealth = UnityEngine.Object.FindFirstObjectByType<PlayerController>().GetComponent<Health>();
                    playerHealth.TakeDamage(1);
                    UnityEngine.UI.Image damageImage = damageOverlay.GetComponent<UnityEngine.UI.Image>();
                    Require(damageImage.color.a > 0.2f, "El destello rojo no reaccionó al daño");
                    Vector3 bossRoomPosition = playerHealth.transform.position;
                    bossRoomPosition.x = 66f;
                    CharacterController controller = playerHealth.GetComponent<CharacterController>();
                    controller.enabled = false;
                    playerHealth.transform.position = bossRoomPosition;
                    controller.enabled = true;

                    SessionState.SetInt(StageKey, 1);
                    frames = 0;
                    frameTimeSum = 0f;
                    measuredFrames = 0;
                }
                catch (Exception ex)
                {
                    Fail(ex.ToString());
                    Application.logMessageReceived -= CaptureLog;
                    SessionState.SetInt(StageKey, 2);
                    EditorApplication.isPlaying = false;
                }
            }
            else if (EditorApplication.isPlaying && stage == 1)
            {
                frames++;
                if (frames == 8)
                {
                    try
                    {
                        DungeonAudioDirector audioDirector = UnityEngine.Object.FindFirstObjectByType<DungeonAudioDirector>();
                        AudioSource[] musicSources = audioDirector.GetComponentsInChildren<AudioSource>();
                        Require(musicSources[1].isPlaying && musicSources[1].volume > 0f,
                            "BOSS_FINAL.mp3 no comenzó al entrar en la arena");
                    }
                    catch (Exception ex)
                    {
                        Fail(ex.ToString());
                        SessionState.SetInt(StageKey, 2);
                        EditorApplication.isPlaying = false;
                        return;
                    }
                }
                if (frames > 10)
                {
                    frameTimeSum += Time.unscaledDeltaTime;
                    measuredFrames++;
                }
                if (frames < 100) return;
                float averageMs = measuredFrames > 0 ? frameTimeSum * 1000f / measuredFrames : 999f;
                Debug.Log($"[GameplaySmokeTest] Tiempo medio de frame: {averageMs:0.00} ms ({(1000f / Mathf.Max(0.01f, averageMs)):0} FPS aprox.)");
                if (averageMs > 50f) Fail($"Rendimiento insuficiente en la prueba: {averageMs:0.00} ms por frame");
                CameraFollow rig = UnityEngine.Object.FindFirstObjectByType<CameraFollow>();
                if (rig != null) rig.SetFirstPersonMode(false);
                Application.logMessageReceived -= CaptureLog;
                SessionState.SetInt(StageKey, 2);
                EditorApplication.isPlaying = false;
            }
            else if (!EditorApplication.isPlaying && stage == 2)
            {
                bool failed = SessionState.GetBool(FailedKey, false);
                SessionState.EraseBool(RunningKey);
                SessionState.EraseBool(FailedKey);
                SessionState.EraseInt(StageKey);
                EditorApplication.update -= Tick;
                if (failed)
                {
                    Debug.LogError("[GameplaySmokeTest] FALLÓ");
                    EditorApplication.Exit(1);
                }
                else
                {
                    Debug.Log("[GameplaySmokeTest] OK: primera persona real, mira visible, cuerpo oculto y partículas sin advertencias.");
                    EditorApplication.Exit(0);
                }
            }
        }

        private static void CaptureLog(string condition, string stackTrace, LogType type)
        {
            if (condition.Contains("Setting the duration while system is still playing")) Fail(condition);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Fail(string message)
        {
            SessionState.SetBool(FailedKey, true);
            Debug.LogError("[GameplaySmokeTest] " + message);
        }
    }
}
#endif
