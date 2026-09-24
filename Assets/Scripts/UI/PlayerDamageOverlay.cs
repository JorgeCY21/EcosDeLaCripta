using System.Collections;
using EcosDeLaCripta.Core;
using UnityEngine;
using UnityEngine.UI;

namespace EcosDeLaCripta.UI
{
    /// <summary>Destello rojo inmediato y sonido localizado cuando el jugador recibe daño.</summary>
    [RequireComponent(typeof(Image))]
    public class PlayerDamageOverlay : MonoBehaviour
    {
        [SerializeField] private Health playerHealth;
        [SerializeField] private AudioClip hurtClip;
        [SerializeField] private float peakAlpha = 0.46f;
        [SerializeField] private float fadeDuration = 0.28f;
        private Image overlay;
        private Coroutine flashRoutine;

        public void Configure(Health health, AudioClip clip)
        {
            playerHealth = health;
            hurtClip = clip;
        }

        private void Awake()
        {
            overlay = GetComponent<Image>();
            SetAlpha(0f);
        }

        private void Start()
        {
            if (playerHealth != null) playerHealth.OnDamaged += HandleDamage;
        }

        private void HandleDamage(int amount)
        {
            if (hurtClip != null) AudioSource.PlayClipAtPoint(hurtClip, playerHealth.transform.position, 0.85f);
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            SetAlpha(peakAlpha);
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(peakAlpha, 0f, elapsed / fadeDuration));
                yield return null;
            }
            SetAlpha(0f);
            flashRoutine = null;
        }

        private void SetAlpha(float alpha)
        {
            if (overlay == null) overlay = GetComponent<Image>();
            overlay.color = new Color(0.72f, 0.015f, 0.01f, alpha);
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.OnDamaged -= HandleDamage;
        }
    }
}
