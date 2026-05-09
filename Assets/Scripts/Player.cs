using UnityEngine;

[DisallowMultipleComponent]
public sealed class Player : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float maxHealth = 100f;
    [SerializeField, Min(0f)] private float currentHealth = 100f;
    [SerializeField, Min(0f)] private float attackPowerPercent = 100f;
    [SerializeField, Min(0f)] private float physicalAttack = 10f;
    [SerializeField, Min(0f)] private float magicalAttack = 10f;
    [SerializeField, Range(0f, 100f)] private float dodgePercent = 5f;

    public float MoveSpeed => moveSpeed;
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float AttackPowerPercent => attackPowerPercent;
    public float PhysicalAttack => physicalAttack;
    public float MagicalAttack => magicalAttack;
    public float DodgePercent => dodgePercent;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
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
        if (value <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(currentHealth - value, 0f);
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        maxHealth = Mathf.Max(0f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        attackPowerPercent = Mathf.Max(0f, attackPowerPercent);
        physicalAttack = Mathf.Max(0f, physicalAttack);
        magicalAttack = Mathf.Max(0f, magicalAttack);
        dodgePercent = Mathf.Clamp(dodgePercent, 0f, 100f);
    }
}