using UnityEngine;

public interface IDamageable
{
    DamageTeam Team { get; }
    bool IsAlive { get; }
    bool ApplyDamage(float amount, DamageTeam sourceTeam, Object source = null);
}
