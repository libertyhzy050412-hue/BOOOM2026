using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class InnerWorldEnemyEffectController : MonoBehaviour
{
    private static readonly Vector2[] SurroundingSampleDirections =
    {
        Vector2.right,
        Vector2.left,
        Vector2.up,
        Vector2.down,
        new Vector2(1f, 1f).normalized,
        new Vector2(-1f, 1f).normalized,
        new Vector2(1f, -1f).normalized,
        new Vector2(-1f, -1f).normalized
    };

    private sealed class EnemyRuntimeState
    {
        public float baseMoveSpeed;
        public bool speedOverridden;
        public float nextDamageTime;
        public bool damageScheduleInitialized;
    }

    [Header("运行设置")]
    [SerializeField] private bool effectEnabled = true;
    [SerializeField] private bool createRevealMaskControllerIfMissing = true;
    [SerializeField, Min(0.02f)] private float evaluationInterval = 0.1f;
    [SerializeField, Range(0f, 1f)] private float revealAlphaThreshold = 0.05f;
    [SerializeField] private Vector2 sampleWorldOffset = Vector2.zero;
    [SerializeField, Min(0f)] private float surroundingSampleRadius = 0.95f;
    [SerializeField, Range(0, 8)] private int surroundingSampleCount = 8;

    [Header("伤害效果")]
    [SerializeField] private bool dealDamageOverTime = true;
    [SerializeField, Min(0f)] private float damagePerHit = 1f;
    [SerializeField, Min(0f)] private float damageInterval = 0.5f;

    [Header("减速效果")]
    [SerializeField] private bool modifyMoveSpeed = true;
    [SerializeField, Min(0f)] private float moveSpeedMultiplier = 0.6f;

    private readonly Dictionary<EnemyBase, EnemyRuntimeState> trackedEnemies = new Dictionary<EnemyBase, EnemyRuntimeState>();

    private WorldRevealMaskController revealMaskController;
    private float nextEvaluationTime;

    private void OnEnable()
    {
        nextEvaluationTime = 0f;
    }

    private void Update()
    {
        if (!effectEnabled)
        {
            if (trackedEnemies.Count > 0)
            {
                RestoreAllEnemySpeeds();
            }

            return;
        }

        float currentTime = Time.time;
        if (currentTime < nextEvaluationTime)
        {
            return;
        }

        nextEvaluationTime = currentTime + Mathf.Max(0.02f, evaluationInterval);

        EvaluateEnemies(currentTime);
    }

    private void OnDisable()
    {
        RestoreAllEnemySpeeds();
    }

    private void OnDestroy()
    {
        RestoreAllEnemySpeeds();
    }

    private void EvaluateEnemies(float currentTime)
    {
        if (!TryResolveRevealMaskController())
        {
            RestoreAllEnemySpeeds();
            return;
        }

        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0)
        {
            RestoreAllEnemySpeeds();
            return;
        }

        HashSet<EnemyBase> activeEnemies = new HashSet<EnemyBase>();
        for (int index = 0; index < enemies.Length; index++)
        {
            EnemyBase enemy = enemies[index];
            if (enemy == null)
            {
                continue;
            }

            activeEnemies.Add(enemy);
            ApplyEffectsToEnemy(enemy, currentTime);
        }

        CleanupMissingEnemies(activeEnemies);
    }

    private void ApplyEffectsToEnemy(EnemyBase enemy, float currentTime)
    {
        EnemyRuntimeState state = GetOrCreateState(enemy);
        if (!state.speedOverridden)
        {
            state.baseMoveSpeed = Mathf.Max(0f, enemy.MoveSpeed);
        }

        if (!enemy.IsAlive || !enemy.EnemyEnabled)
        {
            ResetDamageSchedule(state, currentTime);
            RemoveMoveSpeedOverride(enemy, state);
            return;
        }

        if (!IsEnemyInsideInnerWorld(enemy))
        {
            ResetDamageSchedule(state, currentTime);
            RemoveMoveSpeedOverride(enemy, state);
            if (!state.speedOverridden)
            {
                state.baseMoveSpeed = Mathf.Max(0f, enemy.MoveSpeed);
            }

            return;
        }

        if (dealDamageOverTime && damagePerHit > 0f)
        {
            TryApplyPeriodicDamage(enemy, state, currentTime);
        }

        if (!modifyMoveSpeed)
        {
            RemoveMoveSpeedOverride(enemy, state);
            return;
        }

        float targetMoveSpeed = Mathf.Max(0f, state.baseMoveSpeed * moveSpeedMultiplier);
        if (!state.speedOverridden || !Mathf.Approximately(enemy.MoveSpeed, targetMoveSpeed))
        {
            enemy.SetMoveSpeed(targetMoveSpeed);
        }

        state.speedOverridden = true;
    }

    private void TryApplyPeriodicDamage(EnemyBase enemy, EnemyRuntimeState state, float currentTime)
    {
        if (!state.damageScheduleInitialized)
        {
            state.nextDamageTime = currentTime;
            state.damageScheduleInitialized = true;
        }

        if (currentTime + 0.0001f < state.nextDamageTime)
        {
            return;
        }

        enemy.ApplyDamage(damagePerHit, DamageTeam.Neutral, this);
        state.nextDamageTime = currentTime + Mathf.Max(0f, damageInterval);
    }

    private static void ResetDamageSchedule(EnemyRuntimeState state, float currentTime)
    {
        state.damageScheduleInitialized = false;
        state.nextDamageTime = currentTime;
    }

    private bool IsEnemyInsideInnerWorld(EnemyBase enemy)
    {
        if (revealMaskController == null)
        {
            return false;
        }

        Vector2 centerPosition = (Vector2)enemy.transform.position + sampleWorldOffset;
        if (revealMaskController.IsRevealedAtWorldPosition(centerPosition, revealAlphaThreshold))
        {
            return true;
        }

        if (surroundingSampleRadius <= 0f || surroundingSampleCount <= 0)
        {
            return false;
        }

        int sampleCount = Mathf.Min(surroundingSampleCount, SurroundingSampleDirections.Length);
        for (int index = 0; index < sampleCount; index++)
        {
            Vector2 samplePosition = centerPosition + SurroundingSampleDirections[index] * surroundingSampleRadius;
            if (revealMaskController.IsRevealedAtWorldPosition(samplePosition, revealAlphaThreshold))
            {
                return true;
            }
        }

        return false;
    }

    private EnemyRuntimeState GetOrCreateState(EnemyBase enemy)
    {
        if (!trackedEnemies.TryGetValue(enemy, out EnemyRuntimeState state))
        {
            state = new EnemyRuntimeState
            {
                baseMoveSpeed = Mathf.Max(0f, enemy.MoveSpeed)
            };
            trackedEnemies.Add(enemy, state);
        }

        return state;
    }

    private void RemoveMoveSpeedOverride(EnemyBase enemy, EnemyRuntimeState state)
    {
        if (!state.speedOverridden)
        {
            state.baseMoveSpeed = Mathf.Max(0f, enemy.MoveSpeed);
            return;
        }

        enemy.SetMoveSpeed(state.baseMoveSpeed);
        state.speedOverridden = false;
        state.baseMoveSpeed = Mathf.Max(0f, enemy.MoveSpeed);
    }

    private void CleanupMissingEnemies(HashSet<EnemyBase> activeEnemies)
    {
        if (trackedEnemies.Count == 0)
        {
            return;
        }

        List<EnemyBase> staleEnemies = null;
        foreach (KeyValuePair<EnemyBase, EnemyRuntimeState> pair in trackedEnemies)
        {
            EnemyBase enemy = pair.Key;
            if (enemy != null && activeEnemies.Contains(enemy))
            {
                continue;
            }

            if (enemy != null)
            {
                RemoveMoveSpeedOverride(enemy, pair.Value);
            }

            staleEnemies ??= new List<EnemyBase>();
            staleEnemies.Add(enemy);
        }

        if (staleEnemies == null)
        {
            return;
        }

        for (int index = 0; index < staleEnemies.Count; index++)
        {
            trackedEnemies.Remove(staleEnemies[index]);
        }
    }

    private void RestoreAllEnemySpeeds()
    {
        if (trackedEnemies.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<EnemyBase, EnemyRuntimeState> pair in trackedEnemies)
        {
            if (pair.Key == null)
            {
                continue;
            }

            RemoveMoveSpeedOverride(pair.Key, pair.Value);
        }

        trackedEnemies.Clear();
    }

    private bool TryResolveRevealMaskController()
    {
        if (revealMaskController != null)
        {
            return true;
        }

        revealMaskController = WorldRevealMaskController.Instance;
        if (revealMaskController == null && createRevealMaskControllerIfMissing)
        {
            revealMaskController = WorldRevealMaskController.GetOrCreate();
        }

        return revealMaskController != null;
    }

    private void OnValidate()
    {
        evaluationInterval = Mathf.Max(0.02f, evaluationInterval);
        damagePerHit = Mathf.Max(0f, damagePerHit);
        damageInterval = Mathf.Max(0f, damageInterval);
        moveSpeedMultiplier = Mathf.Max(0f, moveSpeedMultiplier);
        surroundingSampleRadius = Mathf.Max(0f, surroundingSampleRadius);
        surroundingSampleCount = Mathf.Clamp(surroundingSampleCount, 0, SurroundingSampleDirections.Length);
    }
}