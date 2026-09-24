using EcosDeLaCripta.Core;
using UnityEngine;

namespace EcosDeLaCripta.Environment
{
    public class SpikeTrap : MonoBehaviour
    {
        [SerializeField] private float cycleDuration = 1.65f;
        [SerializeField] private float activeDuration = 0.48f;
        [SerializeField] private int damage = 1;
        private Transform visual;
        private Light warningLight;
        private float phase;
        private float hitCooldown;

        private void Awake()
        {
            visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
            phase = Random.Range(0f, cycleDuration);
            GameObject lightGo = new GameObject("WarningLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.25f;
            warningLight = lightGo.AddComponent<Light>();
            warningLight.type = LightType.Point;
            warningLight.color = new Color(1f, 0.08f, 0.02f);
            warningLight.range = 2.2f;
        }

        private bool IsActive => phase > cycleDuration - activeDuration;

        private void Update()
        {
            phase = (phase + Time.deltaTime) % cycleDuration;
            hitCooldown -= Time.deltaTime;
            float anticipation = Mathf.InverseLerp(cycleDuration - activeDuration - 0.35f, cycleDuration - activeDuration, phase);
            warningLight.intensity = IsActive ? 3.5f : anticipation * 1.8f;
            Vector3 p = visual.localPosition;
            p.y = Mathf.Lerp(p.y, IsActive ? 0.22f : -0.08f, Time.deltaTime * 14f);
            visual.localPosition = p;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsActive || hitCooldown > 0f || !other.CompareTag("Player")) return;
            Health hp = other.GetComponent<Health>();
            if (hp == null) return;
            hp.TakeDamage(damage);
            hitCooldown = 0.8f;
        }
    }
}
