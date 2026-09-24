using EcosDeLaCripta.Core;
using EcosDeLaCripta.Player;
using UnityEngine;

namespace EcosDeLaCripta.Combat
{
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 14f;
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private AudioClip hitClip;

        public void ConfigureAudio(AudioClip hit) => hitClip = hit;

        private int damage;
        private GameObject owner;
        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
        }

        public void Launch(Vector3 direction, int amount, GameObject shooter)
        {
            damage = amount;
            owner = shooter;
            rb.linearVelocity = direction.normalized * speed;
            Destroy(gameObject, lifetime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject == owner) return;
            if (other.isTrigger) return;

            var health = other.GetComponent<Health>();
            if (health != null && other.gameObject != owner)
            {
                health.TakeDamage(damage);
                if (hitClip != null) AudioSource.PlayClipAtPoint(hitClip, transform.position, 0.6f);
                SpawnImpact(other.ClosestPoint(transform.position));
                Camera cam = Camera.main;
                CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
                if (follow != null) follow.Shake(0.14f, 0.08f);
                Destroy(gameObject);
                return;
            }

            if (other.CompareTag("Wall"))
            {
                SpawnImpact(transform.position);
                Destroy(gameObject);
            }
        }

        private static void SpawnImpact(Vector3 position)
        {
            GameObject fx = new GameObject("ImpactFX");
            fx.transform.position = position + Vector3.up * 0.25f;
            ParticleSystem ps = fx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.18f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.8f, 1f), Color.white);
            main.maxParticles = 24;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            var renderer = fx.GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null) renderer.material = new Material(shader);
            ps.Play();
            Object.Destroy(fx, 0.7f);
        }
    }
}
