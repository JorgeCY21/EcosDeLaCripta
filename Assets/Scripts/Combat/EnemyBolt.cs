using EcosDeLaCripta.Core;
using UnityEngine;

namespace EcosDeLaCripta.Combat
{
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyBolt : MonoBehaviour
    {
        [SerializeField] private float speed = 7.5f;
        [SerializeField] private float lifetime = 4f;
        private int damage;
        private Rigidbody body;
        private bool ballistic;
        private float ballisticGravity;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void FixedUpdate()
        {
            if (ballistic)
                body.linearVelocity += Vector3.down * ballisticGravity * Time.fixedDeltaTime;
        }

        public void Launch(Vector3 direction, int amount)
        {
            ballistic = false;
            damage = amount;
            body.linearVelocity = direction.normalized * speed;
            Destroy(gameObject, lifetime);
        }

        public void LaunchBallistic(Vector3 launchVelocity, int amount, float gravity)
        {
            ballistic = true;
            ballisticGravity = Mathf.Max(0.1f, gravity);
            damage = amount;
            body.linearVelocity = launchVelocity;
            Destroy(gameObject, lifetime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.isTrigger) return;
            if (other.CompareTag("Player"))
            {
                Health health = other.GetComponent<Health>();
                if (health != null) health.TakeDamage(damage);
                Destroy(gameObject);
            }
            else if (other.CompareTag("Wall"))
            {
                Destroy(gameObject);
            }
        }
    }
}
