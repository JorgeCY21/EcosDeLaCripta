using EcosDeLaCripta.Core;
using EcosDeLaCripta.Enemy;
using EcosDeLaCripta.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EcosDeLaCripta.Audio
{
    /// <summary>
    /// Reproductor musical persistente. AMBIENTE comienza en el menú y no se reinicia al cargar
    /// el nivel; únicamente hace crossfade a BOSS_FINAL dentro de la arena del Guardián.
    /// </summary>
    public class DungeonAudioDirector : MonoBehaviour
    {
        public static DungeonAudioDirector Instance { get; private set; }

        [SerializeField] private AudioClip ambientClip;
        [SerializeField] private AudioClip bossClip;
        [SerializeField] private Transform player;
        [SerializeField] private EnemyAI boss;
        [SerializeField] private float bossRoomStartX = 65.5f;
        [SerializeField] private float crossfadeDuration = 1.25f;
        [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.32f;
        [SerializeField, Range(0f, 1f)] private float bossVolume = 0.42f;

        private AudioSource ambientSource;
        private AudioSource bossSource;

        public void Configure(Transform playerTransform, EnemyAI bossEnemy, float roomStartX, AudioClip ambient, AudioClip bossMusic)
        {
            player = playerTransform;
            boss = bossEnemy;
            bossRoomStartX = roomStartX;
            ambientClip = ambient;
            bossClip = bossMusic;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            ambientSource = CreateSource("Musica_Ambiente", ambientClip, ambientVolume);
            bossSource = CreateSource("Musica_Boss_Final", bossClip, 0f);
            if (ambientSource.clip != null) ambientSource.Play();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ResolveLevelReferences();
        }

        private AudioSource CreateSource(string sourceName, AudioClip clip, float volume)
        {
            GameObject child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.priority = 16;
            source.ignoreListenerPause = true;
            return source;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveLevelReferences();
        }

        private void ResolveLevelReferences()
        {
            PlayerController playerController = FindFirstObjectByType<PlayerController>();
            player = playerController != null ? playerController.transform : null;
            boss = null;
            foreach (EnemyAI enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                if (enemy.isBoss)
                {
                    boss = enemy;
                    break;
                }
            }
        }

        private void Update()
        {
            bool levelAllowsBossMusic = GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;
            bool inBossFight = player != null && player.position.x >= bossRoomStartX && levelAllowsBossMusic;
            float fadeSpeed = 1f / Mathf.Max(0.05f, crossfadeDuration);
            ambientSource.volume = Mathf.MoveTowards(ambientSource.volume, inBossFight ? 0f : ambientVolume, fadeSpeed * ambientVolume * Time.unscaledDeltaTime);
            bossSource.volume = Mathf.MoveTowards(bossSource.volume, inBossFight ? bossVolume : 0f, fadeSpeed * bossVolume * Time.unscaledDeltaTime);

            if (inBossFight && bossSource.clip != null && !bossSource.isPlaying) bossSource.Play();
            if (!inBossFight && ambientSource.clip != null && !ambientSource.isPlaying) ambientSource.Play();
            if (!inBossFight && bossSource.isPlaying && bossSource.volume <= 0.001f) bossSource.Stop();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }
}
