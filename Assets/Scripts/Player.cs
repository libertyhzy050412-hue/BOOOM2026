using UnityEngine;

[DisallowMultipleComponent]
public sealed class Player : MonoBehaviour, IDamageable
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float maxHealth = 100f;
    [SerializeField, Min(0f)] private float currentHealth = 100f;
    [SerializeField, Min(0f)] private float damageCooldownSeconds = 0.25f;
    [SerializeField, Min(0f)] private float attackPowerPercent = 100f;
    [SerializeField, Min(0f)] private float physicalAttack = 10f;
    [SerializeField, Min(0f)] private float magicalAttack = 10f;
    [SerializeField, Range(0f, 100f)] private float dodgePercent = 5f;

    private UnitDamageFlash damageFlash;
    private float nextDamageAllowedTime;
    private bool rewardBaseStatsCaptured;
    private float rewardBaseMoveSpeed;
    private float rewardBaseMaxHealth;
    private float rewardBaseAttackPowerPercent;

    public float MoveSpeed => moveSpeed;
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float DamageCooldownSeconds => damageCooldownSeconds;
    public float AttackPowerPercent => attackPowerPercent;
    public float PhysicalAttack => physicalAttack;
    public float MagicalAttack => magicalAttack;
    public float DodgePercent => dodgePercent;
    public DamageTeam Team => DamageTeam.Player;
    public bool IsAlive => currentHealth > 0f;

    private void Awake()
    {
        CacheRewardBaseStatsIfNeeded();
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        nextDamageAllowedTime = 0f;
        damageFlash = GetComponent<UnitDamageFlash>();
        if (damageFlash == null)
        {
            damageFlash = gameObject.AddComponent<UnitDamageFlash>();
        }
    }

    public void ApplyRuntimeRewardModifiers(float moveSpeedBonus, float maxHealthBonus, float attackPowerPercentBonus)
    {
        CacheRewardBaseStatsIfNeeded();

        float previousMaxHealth = Mathf.Max(maxHealth, 0f);
        float healthRatio = previousMaxHealth > 0f ? Mathf.Clamp01(currentHealth / previousMaxHealth) : 1f;

        moveSpeed = Mathf.Max(0f, rewardBaseMoveSpeed + moveSpeedBonus);
        maxHealth = Mathf.Max(0f, rewardBaseMaxHealth + maxHealthBonus);
        attackPowerPercent = Mathf.Max(0f, rewardBaseAttackPowerPercent + attackPowerPercentBonus);
        currentHealth = Mathf.Clamp(maxHealth * healthRatio, 0f, maxHealth);
    }

    public void SetMoveSpeed(float value)
    {
        moveSpeed = Mathf.Max(0f, value);
    }

    public void SetMaxHealth(float value)
    {
        maxHealth = Mathf.Max(0f, value);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    public void SetCurrentHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
    }

    public void Heal(float value)
    {
        if (value <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Min(currentHealth + value, maxHealth);
    }

    public void TakeDamage(float value)
    {
        ApplyDamage(value, DamageTeam.Neutral, this);
    }

    public bool ApplyDamage(float amount, DamageTeam sourceTeam, Object source = null)
    {
        if (amount <= 0f || !IsAlive)
        {
            return false;
        }

        if (sourceTeam == Team)
        {
            return false;
        }

        float currentTime = Time.time;
        if (damageCooldownSeconds > 0f && currentTime < nextDamageAllowedTime)
        {
            return false;
        }

        currentHealth = Mathf.Max(currentHealth - amount, 0f);
        nextDamageAllowedTime = currentTime + damageCooldownSeconds;
        damageFlash?.PlayFlash();
        return true;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        maxHealth = Mathf.Max(0f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        damageCooldownSeconds = Mathf.Max(0f, damageCooldownSeconds);
        attackPowerPercent = Mathf.Max(0f, attackPowerPercent);
        physicalAttack = Mathf.Max(0f, physicalAttack);
        magicalAttack = Mathf.Max(0f, magicalAttack);
        dodgePercent = Mathf.Clamp(dodgePercent, 0f, 100f);
    }

    private void CacheRewardBaseStatsIfNeeded()
    {
        if (rewardBaseStatsCaptured)
        {
            return;
        }

        rewardBaseMoveSpeed = moveSpeed;
        rewardBaseMaxHealth = maxHealth;
        rewardBaseAttackPowerPercent = attackPowerPercent;
        rewardBaseStatsCaptured = true;
    }
}