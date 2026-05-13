using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    private sealed class EnemySpawnRule
    {
        public string id = "Normal";
        public EnemyBase enemyPrefab;
        public bool enabled = true;
        [Min(0f)] public float startDelay;
        [Min(0)] public int initialBurstCount;
        [Min(0.01f)] public float spawnInterval = 5f;
        [Min(1)] public int spawnCountPerCycle = 1;
        [Min(0)] public int maxAliveCount = 3;
        [Min(0)] public int maxTotalSpawnCount;
        [Min(0f)] public float minSpawnDistanceFromPlayer = 8f;
        [Min(0f)] public float maxSpawnDistanceFromPlayer = 14f;
        [Min(0f)] public float mapEdgePadding = 0.5f;

        [System.NonSerialized] public float elapsedTime;
        [System.NonSerialized] public float spawnTimer;
        [System.NonSerialized] public bool initialBurstHandled;
        [System.NonSerialized] public int totalSpawned;
        [System.NonSerialized] public List<EnemyBase> aliveEnemies = new List<EnemyBase>();
    }

    [SerializeField] private Player targetPlayer;
    [SerializeField] private Transform mapRoot;
    [SerializeField] private string mapRootName = "MapRoot";
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private string enemyRootName = "EnemyRoot";
    [SerializeField] private bool autoSpawn = true;
    [SerializeField] private List<EnemySpawnRule> spawnRules = new List<EnemySpawnRule>();

    private float enemyHealthMultiplier = 1f;
    private float enemyDamageMultiplier = 1f;
    private float enemyMoveSpeedMultiplier = 1f;
    private float spawnRateMultiplier = 1f;

    public bool AutoSpawn => autoSpawn;

    private void Awake()
    {
        ResolveTargetPlayer();
        ResolveMapRoot();
        EnsureEnemyRoot();
        ResetSpawnState();
    }

    private void Update()
    {
        ResolveTargetPlayer();
        ResolveMapRoot();
        EnsureEnemyRoot();

        if (!autoSpawn)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        for (int index = 0; index < spawnRules.Count; index++)
        {
            UpdateRule(spawnRules[index], deltaTime);
        }
    }

    public void SetAutoSpawn(bool enabled)
    {
        autoSpawn = enabled;
    }

    public void SetWaveMultipliers(float healthMultiplier, float damageMultiplier, float moveSpeedMultiplier, float spawnMultiplier)
    {
        enemyHealthMultiplier = Mathf.Max(0.01f, healthMultiplier);
        enemyDamageMultiplier = Mathf.Max(0.01f, damageMultiplier);
        enemyMoveSpeedMultiplier = Mathf.Max(0.01f, moveSpeedMultiplier);
        spawnRateMultiplier = Mathf.Max(0.01f, spawnMultiplier);
    }

    [ContextMenu("Reset Spawn State")]
    public void ResetSpawnState()
    {
        ResetSpawnState(false);
    }

    public void ResetSpawnState(bool preserveAliveEnemies)
    {
        for (int index = 0; index < spawnRules.Count; index++)
        {
            EnemySpawnRule rule = spawnRules[index];
            if (rule == null)
            {
                continue;
            }

            rule.elapsedTime = 0f;
            rule.spawnTimer = 0f;
            rule.initialBurstHandled = false;
            rule.totalSpawned = 0;
            if (rule.aliveEnemies == null)
            {
                rule.aliveEnemies = new List<EnemyBase>();
            }
            else
            {
                CleanupRule(rule);
                if (!preserveAliveEnemies)
                {
                    rule.aliveEnemies.Clear();
                }
            }
        }
    }

    private void UpdateRule(EnemySpawnRule rule, float deltaTime)
    {
        if (rule == null || !rule.enabled || rule.enemyPrefab == null)
        {
            return;
        }

        if (rule.aliveEnemies == null)
        {
            rule.aliveEnemies = new List<EnemyBase>();
        }

        CleanupRule(rule);
        rule.elapsedTime += deltaTime;
        float effectiveStartDelay = GetEffectiveStartDelay(rule);
        if (rule.elapsedTime < effectiveStartDelay)
        {
            return;
        }

        if (!rule.initialBurstHandled)
        {
            rule.initialBurstHandled = true;
            int initialBurstCount = GetEffectiveInitialBurstCount(rule);
            if (initialBurstCount > 0)
            {
                SpawnRule(rule, initialBurstCount);
            }
        }

        rule.spawnTimer += deltaTime;
        float effectiveSpawnInterval = GetEffectiveSpawnInterval(rule);
        if (rule.spawnTimer < effectiveSpawnInterval)
        {
            return;
        }

        int cycles = Mathf.FloorToInt(rule.spawnTimer / effectiveSpawnInterval);
        rule.spawnTimer -= cycles * effectiveSpawnInterval;
        for (int cycleIndex = 0; cycleIndex < cycles; cycleIndex++)
        {
            if (!CanSpawnMore(rule))
            {
                return;
            }

            SpawnRule(rule, GetEffectiveSpawnCountPerCycle(rule));
        }
    }

    private void SpawnRule(EnemySpawnRule rule, int desiredCount)
    {
        if (desiredCount <= 0 || !TryGetMapBounds(out Bounds mapBounds))
        {
            return;
        }

        Vector3 referencePosition = targetPlayer != null ? targetPlayer.transform.position : mapBounds.center;
        for (int spawnIndex = 0; spawnIndex < desiredCount; spawnIndex++)
        {
            if (!CanSpawnMore(rule))
            {
                return;
            }

            if (!TryGetSpawnPosition(rule, referencePosition, mapBounds, out Vector3 spawnPosition))
            {
                return;
            }

            EnemyBase instance = Instantiate(rule.enemyPrefab, spawnPosition, Quaternion.identity, EnsureEnemyRoot());
            instance.SetTargetPlayer(targetPlayer);
            ApplyRuntimeMultipliers(instance);
            rule.aliveEnemies.Add(instance);
            rule.totalSpawned++;
        }
    }

    private bool CanSpawnMore(EnemySpawnRule rule)
    {
        int effectiveMaxTotalSpawnCount = GetEffectiveMaxTotalSpawnCount(rule);
        if (effectiveMaxTotalSpawnCount > 0 && rule.totalSpawned >= effectiveMaxTotalSpawnCount)
        {
            return false;
        }

        int effectiveMaxAliveCount = GetEffectiveMaxAliveCount(rule);
        return effectiveMaxAliveCount <= 0 || rule.aliveEnemies.Count < effectiveMaxAliveCount;
    }

    private void CleanupRule(EnemySpawnRule rule)
    {
        for (int index = rule.aliveEnemies.Count - 1; index >= 0; index--)
        {
            EnemyBase enemy = rule.aliveEnemies[index];
            if (enemy == null || !enemy.IsAlive || !enemy.EnemyEnabled)
            {
                rule.aliveEnemies.RemoveAt(index);
            }
        }
    }

    private void ApplyRuntimeMultipliers(EnemyBase enemy)
    {
        if (enemy == null)
        {
            return;
        }

        float currentHealthRatio = enemy.MaxHealth <= 0f ? 1f : Mathf.Clamp01(enemy.CurrentHealth / enemy.MaxHealth);
        enemy.SetMaxHealth(enemy.MaxHealth * enemyHealthMultiplier);
        enemy.SetCurrentHealth(enemy.MaxHealth * currentHealthRatio);
        enemy.SetMoveSpeed(enemy.MoveSpeed * enemyMoveSpeedMultiplier);

        ContactDamageDealer[] damageDealers = enemy.GetComponentsInChildren<ContactDamageDealer>(true);
        for (int index = 0; index < damageDealers.Length; index++)
        {
            ContactDamageDealer damageDealer = damageDealers[index];
            if (damageDealer == null)
            {
                continue;
            }

            damageDealer.SetDamageAmount(damageDealer.DamageAmount * enemyDamageMultiplier);
        }
    }

    private float GetEffectiveStartDelay(EnemySpawnRule rule)
    {
        return rule.startDelay / spawnRateMultiplier;
    }

    private int GetEffectiveInitialBurstCount(EnemySpawnRule rule)
    {
        return ScaleOptionalCount(rule.initialBurstCount);
    }

    private float GetEffectiveSpawnInterval(EnemySpawnRule rule)
    {
        return Mathf.Max(0.01f, rule.spawnInterval / spawnRateMultiplier);
    }

    private int GetEffectiveSpawnCountPerCycle(EnemySpawnRule rule)
    {
        return Mathf.Max(1, Mathf.CeilToInt(rule.spawnCountPerCycle * spawnRateMultiplier));
    }

    private int GetEffectiveMaxAliveCount(EnemySpawnRule rule)
    {
        return ScaleOptionalCount(rule.maxAliveCount);
    }

    private int GetEffectiveMaxTotalSpawnCount(EnemySpawnRule rule)
    {
        return ScaleOptionalCount(rule.maxTotalSpawnCount);
    }

    private int ScaleOptionalCount(int originalValue)
    {
        if (originalValue <= 0)
        {
            return 0;
        }

        return Mathf.Max(1, Mathf.CeilToInt(originalValue * spawnRateMultiplier));
    }

    private bool TryGetSpawnPosition(EnemySpawnRule rule, Vector3 referencePosition, Bounds mapBounds, out Vector3 spawnPosition)
    {
        float minDistance = Mathf.Max(0f, rule.minSpawnDistanceFromPlayer);
        float maxDistance = Mathf.Max(minDistance, rule.maxSpawnDistanceFromPlayer);
        float minX = mapBounds.min.x + rule.mapEdgePadding;
        float maxX = mapBounds.max.x - rule.mapEdgePadding;
        float minY = mapBounds.min.y + rule.mapEdgePadding;
        float maxY = mapBounds.max.y - rule.mapEdgePadding;

        if (minX > maxX || minY > maxY)
        {
            spawnPosition = mapBounds.center;
            spawnPosition.z = 0f;
            return true;
        }

        for (int attempt = 0; attempt < 16; attempt++)
        {
            Vector2 direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }
            else
            {
                direction.Normalize();
            }

            float distance = Random.Range(minDistance, maxDistance);
            Vector2 candidate = (Vector2)referencePosition + direction * distance;
            candidate.x = Mathf.Clamp(candidate.x, minX, maxX);
            candidate.y = Mathf.Clamp(candidate.y, minY, maxY);

            if ((candidate - (Vector2)referencePosition).sqrMagnitude < minDistance * minDistance * 0.85f)
            {
                continue;
            }

            spawnPosition = new Vector3(candidate.x, candidate.y, 0f);
            return true;
        }

        spawnPosition = new Vector3(
            Random.Range(minX, maxX),
            Random.Range(minY, maxY),
            0f);
        return true;
    }

    private bool TryGetMapBounds(out Bounds bounds)
    {
        ResolveMapRoot();
        bounds = default;

        if (mapRoot == null)
        {
            return false;
        }

        Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer rendererReference = renderers[index];
            if (rendererReference == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = rendererReference.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererReference.bounds);
            }
        }

        return hasBounds;
    }

    private void ResolveTargetPlayer()
    {
        if (targetPlayer == null)
        {
            targetPlayer = FindFirstObjectByType<Player>();
        }
    }

    private void ResolveMapRoot()
    {
        if (mapRoot != null)
        {
            return;
        }

        string rootName = string.IsNullOrWhiteSpace(mapRootName) ? "MapRoot" : mapRootName;
        GameObject rootObject = GameObject.Find(rootName);
        if (rootObject != null)
        {
            mapRoot = rootObject.transform;
        }
    }

    private Transform EnsureEnemyRoot()
    {
        if (enemyRoot != null)
        {
            return enemyRoot;
        }

        string rootName = string.IsNullOrWhiteSpace(enemyRootName) ? "EnemyRoot" : enemyRootName;
        GameObject rootObject = GameObject.Find(rootName);
        if (rootObject == null)
        {
            rootObject = new GameObject(rootName);
        }

        enemyRoot = rootObject.transform;
        return enemyRoot;
    }

    private void OnValidate()
    {
        spawnRateMultiplier = Mathf.Max(0.01f, spawnRateMultiplier);

        if (spawnRules == null)
        {
            return;
        }

        for (int index = 0; index < spawnRules.Count; index++)
        {
            EnemySpawnRule rule = spawnRules[index];
            if (rule == null)
            {
                continue;
            }

            rule.startDelay = Mathf.Max(0f, rule.startDelay);
            rule.initialBurstCount = Mathf.Max(0, rule.initialBurstCount);
            rule.spawnInterval = Mathf.Max(0.01f, rule.spawnInterval);
            rule.spawnCountPerCycle = Mathf.Max(1, rule.spawnCountPerCycle);
            rule.maxAliveCount = Mathf.Max(0, rule.maxAliveCount);
            rule.maxTotalSpawnCount = Mathf.Max(0, rule.maxTotalSpawnCount);
            rule.minSpawnDistanceFromPlayer = Mathf.Max(0f, rule.minSpawnDistanceFromPlayer);
            rule.maxSpawnDistanceFromPlayer = Mathf.Max(rule.minSpawnDistanceFromPlayer, rule.maxSpawnDistanceFromPlayer);
            rule.mapEdgePadding = Mathf.Max(0f, rule.mapEdgePadding);
        }
    }
}