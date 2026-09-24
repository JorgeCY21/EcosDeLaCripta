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

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        public void Launch(Vector3 direction, int amount)
        {
            damage = amount;
            body.linearVelocity = direction.normalized * speed;
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
