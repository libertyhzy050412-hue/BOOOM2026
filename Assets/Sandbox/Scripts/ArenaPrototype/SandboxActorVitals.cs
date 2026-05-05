using UnityEngine;
using System;

namespace Sandbox.DreamBattle
{
    [DisallowMultipleComponent]
    public class SandboxActorVitals : MonoBehaviour
    {
        [SerializeField] [HideInInspector] private float maxHealth = 10f;
        [SerializeField] [HideInInspector] private bool canTakeDamage;

        private float currentHealth;
        private bool isDead;

        public event Action<float, float> Damaged;

        public event Action Died;

        public event Action ResetOccurred;

        public float MaxHealth => maxHealth;

        public float CurrentHealth => currentHealth;

        public bool CanTakeDamage => canTakeDamage;

        public bool IsDead => isDead;

        private void Awake()
        {
            ResetVitals();
        }

        public void ApplySettings(float configuredMaxHealth, bool damageEnabled, bool resetCurrentHealth)
        {
            maxHealth = Mathf.Max(1f, configuredMaxHealth);
            canTakeDamage = damageEnabled;

            if (resetCurrentHealth || currentHealth <= 0f || currentHealth > maxHealth)
            {
                currentHealth = maxHealth;
                isDead = false;
                ResetOccurred?.Invoke();
            }
        }

        public void ResetVitals()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = maxHealth;
            isDead = false;
            ResetOccurred?.Invoke();
        }

        public bool ApplyDamage(float damage)
        {
            if (!canTakeDamage || isDead || damage <= 0f)
            {
                return false;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            Damaged?.Invoke(damage, currentHealth);
            isDead = currentHealth <= 0f;
            if (isDead)
            {
                Died?.Invoke();
            }

            return true;
        }

        public void SetDamageEnabled(bool enabled)
        {
            canTakeDamage = enabled;
        }
    }
}