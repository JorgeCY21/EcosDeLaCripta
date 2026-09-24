#if UNITY_EDITOR
using System;
using EcosDeLaCripta.Audio;
using EcosDeLaCripta.Core;
using EcosDeLaCripta.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EcosDeLaCripta.EditorTools
{
    /// <summary>Comprueba que AMBIENTE no se reinicie entre menú y nivel y ceda ante BOSS_FINAL.</summary>
    [InitializeOnLoad]
    public static class MusicPersistenceSmokeTest
    {
        private const string RunningKey = "Ecos.MusicPersistence.Running";
        private const string StageKey = "Ecos.MusicPersistence.Stage";
        private const string FailedKey = "Ecos.MusicPersistence.Failed";
        private static int frames;
        private static int directorId;
        private static float ambientTime;

        static MusicPersistenceSmokeTest()
        {
            if (SessionState.GetBool(RunningKey, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            SessionState.SetBool(RunningKey, true);
            SessionState.SetBool(FailedKey, false);
            SessionState.SetInt(StageKey, 0);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                if (SessionState.GetInt(StageKey, 0) != 3) return;
                bool failed = SessionState.GetBool(FailedKey, false);
                SessionState.EraseBool(RunningKey);
                SessionState.EraseBool(FailedKey);
                SessionState.EraseInt(StageKey);
                EditorApplication.update -= Tick;
                Debug.Log(failed
                    ? "[MusicPersistenceSmokeTest] FALLÓ"
                    : "[MusicPersistenceSmokeTest] OK: AMBIENTE continuó del menú al nivel y BOSS_FINAL tomó el control en la arena.");
                EditorApplication.Exit(failed ? 1 : 0);
                return;
            }

            frames++;
            try
            {
                int stage = SessionState.GetInt(StageKey, 0);
                if (stage == 0 && frames >= 24)
                {
                    DungeonAudioDirector director = UnityEngine.Object.FindFirstObjectByType<DungeonAudioDirector>();
                    Require(director != null, "No hay director musical en el menú");
                    AudioSource ambient = FindSource(director, "Musica_Ambiente");
                    Require(ambient != null && ambient.isPlaying && ambient.clip.name == "Ambiente", "AMBIENTE no inició en el menú");
                    directorId = director.GetInstanceID();
                    ambientTime = ambient.time;
                    SessionState.SetInt(StageKey, 1);
                    frames = 0;
                    SceneManager.LoadScene("Level1");
                }
                else if (stage == 1 && frames >= 24)
                {
                    DungeonAudioDirector director = DungeonAudioDirector.Instance;
                    Require(director != null && director.GetInstanceID() == directorId, "La música se reinició al cargar el nivel");
                    Require(UnityEngine.Object.FindObjectsByType<DungeonAudioDirector>(FindObjectsSortMode.None).Length == 1,
                        "Hay directores musicales duplicados");
                    AudioSource ambient = FindSource(director, "Musica_Ambiente");
                    Require(ambient.isPlaying && ambient.time > ambientTime, "AMBIENTE perdió su posición durante la carga");
                    PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                    Require(player != null, "No apareció el jugador tras cargar el nivel");
                    Vector3 position = player.transform.position;
                    position.x = 66f;
                    CharacterController controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    player.transform.position = position;
                    controller.enabled = true;
                    SessionState.SetInt(StageKey, 2);
                    frames = 0;
                }
                else if (stage == 2 && frames >= 240)
                {
                    DungeonAudioDirector director = DungeonAudioDirector.Instance;
                    AudioSource boss = FindSource(director, "Musica_Boss_Final");
                    PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                    Debug.Log($"[MusicPersistenceSmokeTest] Boss check: playerX={player.transform.position.x:0.00}, " +
                              $"state={GameManager.Instance?.State}, playing={boss?.isPlaying}, volume={boss?.volume:0.000}, clip={boss?.clip?.name}");
                    Require(boss != null && boss.isPlaying && boss.volume > 0f && boss.clip.name == "BossFinal",
                        "BOSS_FINAL no reemplazó a AMBIENTE en la arena");
                    Finish(false);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[MusicPersistenceSmokeTest] " + ex);
                Finish(true);
            }
        }

        private static AudioSource FindSource(DungeonAudioDirector director, string sourceName)
        {
            foreach (AudioSource source in director.GetComponentsInChildren<AudioSource>())
                if (source.name == sourceName) return source;
            return null;
        }

        private static void Finish(bool failed)
        {
            SessionState.SetBool(FailedKey, failed);
            SessionState.SetInt(StageKey, 3);
            EditorApplication.isPlaying = false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
