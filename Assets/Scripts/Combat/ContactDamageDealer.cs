using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ContactDamageDealer : MonoBehaviour
{
    [SerializeField] private DamageTeam sourceTeam = DamageTeam.Neutral;
    [SerializeField, Min(0f)] private float damageAmount = 1f;
    [SerializeField, Min(0.01f)] private float hitCooldown = 0.2f;
    [SerializeField] private bool hitOnTrigger = true;
    [SerializeField] private bool hitOnTriggerStay = true;
    [SerializeField] private bool hitOnCollision = true;
    [SerializeField] private bool hitOnCollisionStay = true;
    [SerializeField] private bool autoResolveSourceTeam = true;
    [SerializeField] private bool requireWeaponOrSummonForPlayerDamage = true;

    private readonly Dictionary<int, float> nextHitTimes = new Dictionary<int, float>();
    private Component ownerDamageableComponent;
    private WeaponBase ownerWeapon;
    private SummonBase ownerSummon;

    public float DamageAmount => damageAmount;

    private void Awake()
    {
        ResolveOwnerDamageable();
        ResolveSourceTeam();
        ResolveAttackSourceHierarchy();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hitOnTrigger)
        {
            TryDealDamage(other);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (hitOnTriggerStay)
        {
            TryDealDamage(other);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hitOnCollision)
        {
            TryDealDamage(collision.collider);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (hitOnCollisionStay)
        {
            TryDealDamage(collision.collider);
        }
    }

    private void TryDealDamage(Component other)
    {
        if (other == null || damageAmount <= 0f)
        {
            return;
        }

        if (!TryResolveDamageable(other, out IDamageable damageable, out Component damageableComponent))
        {
            return;
        }

        ResolveOwnerDamageable();
        if (ownerDamageableComponent != null && damageableComponent == ownerDamageableComponent)
        {
            return;
        }

        if (!CanDealDamage())
        {
            return;
        }

        int targetId = damageableComponent.GetInstanceID();
        float currentTime = Time.time;
        if (nextHitTimes.TryGetValue(targetId, out float nextAllowedHitTime) && currentTime < nextAllowedHitTime)
        {
            return;
        }

        if (!damageable.ApplyDamage(damageAmount, sourceTeam, this))
        {
            return;
        }

        nextHitTimes[targetId] = currentTime + hitCooldown;
    }

    private void ResolveOwnerDamageable()
    {
        if (ownerDamageableComponent != null)
        {
            return;
        }

        TryResolveDamageable(transform, out _, out ownerDamageableComponent);
    }

    private void ResolveSourceTeam()
    {
        if (!autoResolveSourceTeam || sourceTeam != DamageTeam.Neutral)
        {
            return;
        }

        if (GetComponentInParent<Player>() != null)
        {
            sourceTeam = DamageTeam.Player;
            return;
        }

        if (GetComponentInParent<EnemyBase>() != null)
        {
            sourceTeam = DamageTeam.Enemy;
        }
    }

    private bool CanDealDamage()
    {
        if (sourceTeam != DamageTeam.Player || !requireWeaponOrSummonForPlayerDamage)
        {
            return true;
        }

        ResolveAttackSourceHierarchy();
        return ownerWeapon != null || ownerSummon != null;
    }

    private void ResolveAttackSourceHierarchy()
    {
        if (ownerWeapon == null)
        {
            ownerWeapon = GetComponentInParent<WeaponBase>();
        }

        if (ownerSummon == null)
        {
            ownerSummon = GetComponentInParent<SummonBase>();
        }
    }

    private static bool TryResolveDamageable(Component source, out IDamageable damageable, out Component damageableComponent)
    {
        MonoBehaviour[] behaviours = source.GetComponentsInParent<MonoBehaviour>(true);
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];
            if (behaviour is IDamageable resolved)
            {
                damageable = resolved;
                damageableComponent = behaviour;
                return true;
            }
        }

        damageable = null;
        damageableComponent = null;
        return false;
    }

    public void SetDamageAmount(float value)
    {
        damageAmount = Mathf.Max(0f, value);
    }

    private void OnValidate()
    {
        damageAmount = Mathf.Max(0f, damageAmount);
        hitCooldown = Mathf.Max(0.01f, hitCooldown);
    }
}