using System.Collections;
using EcosDeLaCripta.Core;
using EcosDeLaCripta.Player;
using UnityEngine;
using UnityEngine.UI;

namespace EcosDeLaCripta.UI
{
    /// <summary>Conecta el GameManager/Health del jugador con el HUD y las pantallas de victoria/derrota del Nivel 1.</summary>
    public class HUDController : MonoBehaviour
    {
        [Header("HUD en partida")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Text healthText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text pickupAnnouncementText;

        [Header("Paneles de estado")]
        [SerializeField] private GameObject wonPanel;
        [SerializeField] private GameObject lostPanel;
        [SerializeField] private Button retryWonButton;
        [SerializeField] private Button retryLostButton;
        [SerializeField] private Button menuWonButton;
        [SerializeField] private Button menuLostButton;

        private Health playerHealth;
        private PlayerController playerController;
        private Coroutine pickupRoutine;

        /// <summary>Usado por el LevelBuilder de editor para enlazar todos los elementos del HUD generados.</summary>
        public void Bind(Slider hpSlider, Text hpText, Text score, Text objective, Text pickupAnnouncement,
            GameObject won, GameObject lost,
            Button wonRetryBtn, Button lostRetryBtn, Button wonMenuBtn, Button lostMenuBtn)
        {
            healthSlider = hpSlider;
            healthText = hpText;
            scoreText = score;
            objectiveText = objective;
            pickupAnnouncementText = pickupAnnouncement;
            wonPanel = won;
            lostPanel = lost;
            retryWonButton = wonRetryBtn;
            retryLostButton = lostRetryBtn;
            menuWonButton = wonMenuBtn;
            menuLostButton = lostMenuBtn;
        }

        private void Start()
        {
            if (retryWonButton != null) retryWonButton.onClick.AddListener(() => GameManager.Instance.RestartLevel());
            if (retryLostButton != null) retryLostButton.onClick.AddListener(() => GameManager.Instance.RestartLevel());
            if (menuWonButton != null) menuWonButton.onClick.AddListener(() => GameManager.Instance.ReturnToMenu());
            if (menuLostButton != null) menuLostButton.onClick.AddListener(() => GameManager.Instance.ReturnToMenu());

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScoreChanged += HandleScoreChanged;
                GameManager.Instance.OnProgressChanged += HandleProgressChanged;
                GameManager.Instance.OnStateChanged += HandleStateChanged;
                HandleStateChanged(GameManager.Instance.State);
                HandleScoreChanged(GameManager.Instance.Score);
                HandleProgressChanged(GameManager.Instance.EnemiesDefeated, GameManager.Instance.TotalEnemies);
            }

            FindPlayer();
        }

        private void FindPlayer()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            playerHealth = go.GetComponent<Health>();
            playerController = go.GetComponent<PlayerController>();
            if (playerController != null) playerController.OnPowerUpCollected += HandlePowerUpCollected;
            if (playerHealth != null)
            {
                playerHealth.OnChanged += HandleHealthChanged;
                HandleHealthChanged(playerHealth.Current, playerHealth.MaxHealth);
            }
        }

        private void HandleHealthChanged(int current, int max)
        {
            if (healthSlider != null)
            {
                healthSlider.maxValue = max;
                healthSlider.value = current;
            }
            if (healthText != null) healthText.text = $"Vida: {current}/{max}";
        }

        private void HandleScoreChanged(int score)
        {
            if (scoreText != null) scoreText.text = $"Puntaje: {score}";
        }

        private void HandlePowerUpCollected(string label)
        {
            if (pickupAnnouncementText == null) return;
            if (pickupRoutine != null) StopCoroutine(pickupRoutine);
            pickupRoutine = StartCoroutine(ShowPickupAnnouncement(label));
        }

        private IEnumerator ShowPickupAnnouncement(string label)
        {
            pickupAnnouncementText.gameObject.SetActive(true);
            pickupAnnouncementText.text = "✦  " + label.ToUpperInvariant() + "  ✦";
            Color baseColor = new Color(1f, 0.78f, 0.24f, 1f);
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / 0.2f);
                pickupAnnouncementText.transform.localScale = Vector3.Lerp(Vector3.one * 0.72f, Vector3.one, p);
                pickupAnnouncementText.color = baseColor;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1.25f);
            t = 0f;
            while (t < 0.5f)
            {
                t += Time.unscaledDeltaTime;
                baseColor.a = 1f - Mathf.Clamp01(t / 0.5f);
                pickupAnnouncementText.color = baseColor;
                yield return null;
            }
            pickupAnnouncementText.gameObject.SetActive(false);
            pickupRoutine = null;
        }

        private void HandleProgressChanged(int defeated, int total)
        {
            if (objectiveText == null) return;
            if (defeated < 2) objectiveText.text = $"I  ANTESALA DE LOS ECOS  •  {defeated}/2";
            else if (defeated < 5) objectiveText.text = $"II  GALERÍA DE CUCHILLAS  •  {defeated - 2}/3";
            else if (defeated < 8) objectiveText.text = $"III  OSARIO VERDE  •  {defeated - 5}/3";
            else if (defeated < 11) objectiveText.text = $"IV  SANTUARIO DEL BRUTO  •  {defeated - 8}/3";
            else if (defeated < 12) objectiveText.text = "V  ARENA DE LA TRAYECTORIA  •  0/1";
            else objectiveText.text = "VI  DERROTA AL GUARDIÁN DE LA CRIPTA";
        }

        private void HandleStateChanged(GameState state)
        {
            if (wonPanel != null) wonPanel.SetActive(state == GameState.Won);
            if (lostPanel != null) lostPanel.SetActive(state == GameState.Lost);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScoreChanged -= HandleScoreChanged;
                GameManager.Instance.OnProgressChanged -= HandleProgressChanged;
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
            }
            if (playerHealth != null) playerHealth.OnChanged -= HandleHealthChanged;
            if (playerController != null) playerController.OnPowerUpCollected -= HandlePowerUpCollected;
        }
    }
}
