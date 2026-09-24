using EcosDeLaCripta.Player;
using UnityEngine;

namespace EcosDeLaCripta.Items
{
    public enum PowerUpType { Health, Damage, Speed }

    public class PowerUp : MonoBehaviour
    {
        [SerializeField] private PowerUpType type = PowerUpType.Health;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float spinSpeed = 90f;
        [SerializeField] private AudioClip pickupClip;

        private Vector3 startPos;
        private Transform visual;
        private Transform orbit;
        private TextMesh label;
        private Light glow;

        public void Configure(PowerUpType powerUpType) => type = powerUpType;
        public void ConfigureAudio(AudioClip pickup) => pickupClip = pickup;

        private void Start()
        {
            startPos = transform.position;
            visual = transform.Find("Model");
            orbit = transform.Find("Orbit");
            label = GetComponentInChildren<TextMesh>();
            glow = GetComponentInChildren<Light>();
        }

        private void Update()
        {
            transform.position = startPos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
            if (visual != null) visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            if (orbit != null) orbit.Rotate(Vector3.up, spinSpeed * 0.65f * Time.deltaTime, Space.Self);
            if (glow != null) glow.intensity = 2.8f + Mathf.Sin(Time.time * 4f) * 0.65f;
            Camera cam = Camera.main;
            if (label != null && cam != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<PlayerController>();
            if (player == null) return;

            player.ApplyPowerUp(type);
            if (pickupClip != null) AudioSource.PlayClipAtPoint(pickupClip, transform.position, 0.7f);
            SpawnPickupBurst();
            Destroy(gameObject);
        }

        private void SpawnPickupBurst()
        {
            GameObject fx = new GameObject("PowerUpBurst");
            fx.transform.position = transform.position + Vector3.up * 0.45f;
            ParticleSystem ps = fx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = label != null ? label.color : Color.white;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            ps.Play();
            Destroy(fx, 1.1f);
        }
    }
}
