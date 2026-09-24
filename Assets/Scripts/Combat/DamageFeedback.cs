using System.Collections;
using EcosDeLaCripta.Core;
using UnityEngine;

namespace EcosDeLaCripta.Combat
{
    [RequireComponent(typeof(Health))]
    public class DamageFeedback : MonoBehaviour
    {
        [SerializeField] private Color flashColor = new Color(1f, 0.15f, 0.08f);
        [SerializeField] private float flashDuration = 0.11f;
        private Health health;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Coroutine flashRoutine;
        private Vector3 baseScale;

        private void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            baseScale = transform.localScale;
        }

        private void OnEnable() => health.OnDamaged += OnDamaged;
        private void OnDisable() => health.OnDamaged -= OnDamaged;

        private void OnDamaged(int amount)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
            StartCoroutine(FloatDamage(amount));
        }

        private IEnumerator Flash()
        {
            transform.localScale = baseScale * 1.08f;
            foreach (Renderer r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", flashColor);
                block.SetColor("_EmissionColor", flashColor * 2f);
                r.SetPropertyBlock(block);
            }
            yield return new WaitForSeconds(flashDuration);
            transform.localScale = baseScale;
            block.Clear();
            foreach (Renderer r in renderers) r.SetPropertyBlock(block);
            flashRoutine = null;
        }

        private IEnumerator FloatDamage(int amount)
        {
            GameObject label = new GameObject("DamageNumber");
            label.transform.position = transform.position + Vector3.up * 1.45f;
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = "-" + amount;
            text.fontSize = 64;
            text.characterSize = 0.055f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.78f, 0.22f);
            float t = 0f;
            while (t < 0.55f)
            {
                t += Time.deltaTime;
                label.transform.position += Vector3.up * Time.deltaTime * 0.8f;
                Camera cam = Camera.main;
                if (cam != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
                text.color = new Color(text.color.r, text.color.g, text.color.b, 1f - t / 0.55f);
                yield return null;
            }
            Destroy(label);
        }
    }
}
