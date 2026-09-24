using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EcosDeLaCripta.Core
{
    public enum GameState { Playing, Won, Lost }

    /// <summary>
    /// Orquesta el estado del Nivel 1: puntaje, condición de victoria (jefe de la sala final)
    /// y de derrota (vida del jugador a 0).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private int totalEnemiesInLevel = 4;

        public const string MainMenuScene = "MainMenu";

        public int Score { get; private set; }
        public int EnemiesDefeated => enemiesDefeated;
        public int TotalEnemies => totalEnemiesInLevel;
        public GameState State { get; private set; } = GameState.Playing;

        public event Action<int> OnScoreChanged;
        public event Action<int, int> OnProgressChanged;
        public event Action<GameState> OnStateChanged;

        private int enemiesDefeated;
        private bool bossDefeated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            StartLevel();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (State != GameState.Playing && keyboard != null && keyboard.rKey.wasPressedThisFrame &&
                (State == GameState.Won || State == GameState.Lost))
            {
                RestartLevel();
            }
        }

        public void SetTotalEnemies(int count) => totalEnemiesInLevel = count;

        public void StartLevel()
        {
            Score = 0;
            enemiesDefeated = 0;
            bossDefeated = false;
            SetState(GameState.Playing);
            OnScoreChanged?.Invoke(Score);
            OnProgressChanged?.Invoke(enemiesDefeated, totalEnemiesInLevel);
        }

        public void OnEnemyDefeated(int scoreReward, bool isBoss)
        {
            if (State != GameState.Playing) return;

            Score += scoreReward;
            enemiesDefeated++;
            if (isBoss) bossDefeated = true;
            OnScoreChanged?.Invoke(Score);
            OnProgressChanged?.Invoke(enemiesDefeated, totalEnemiesInLevel);

            if (bossDefeated)
            {
                SetState(GameState.Won);
            }
        }

        public void OnPlayerDied()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Lost);
        }

        public void RestartLevel()
        {
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        public void ReturnToMenu()
        {
            SceneManager.LoadScene(MainMenuScene);
        }

        private void SetState(GameState next)
        {
            State = next;
            OnStateChanged?.Invoke(State);
        }
    }
}
