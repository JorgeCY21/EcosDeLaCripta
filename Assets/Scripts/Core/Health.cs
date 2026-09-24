using System;
using UnityEngine;

namespace EcosDeLaCripta.Core
{
    /// <summary>Vida reusable para jugador y enemigos.</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 5;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private float destroyDelay = 1.1f;

        public int MaxHealth => maxHealth;
        public int Current { get; private set; }
        public bool IsDead { get; private set; }
        private float invulnerableTimer;

        public event Action<int, int> OnChanged; // (current, max)
        public event Action<int> OnDamaged;
        public event Action OnDied;

        private void Awake()
        {
            Current = maxHealth;
        }

        private void Update()
        {
            if (invulnerableTimer > 0f) invulnerableTimer -= Time.deltaTime;
        }

        public void SetInvulnerable(float duration)
        {
            invulnerableTimer = Mathf.Max(invulnerableTimer, duration);
        }

        public void SetMax(int value, bool refill = true)
        {
            maxHealth = Mathf.Max(1, value);
            if (refill) Current = maxHealth;
            OnChanged?.Invoke(Current, maxHealth);
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0 || invulnerableTimer > 0f) return;
            Current = Mathf.Max(0, Current - amount);
            OnChanged?.Invoke(Current, maxHealth);
            OnDamaged?.Invoke(amount);
            if (Current == 0) Die();
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            OnChanged?.Invoke(Current, maxHealth);
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            OnDied?.Invoke();
            if (destroyOnDeath)
            {
                Destroy(gameObject, destroyDelay);
            }
        }
    }
}
