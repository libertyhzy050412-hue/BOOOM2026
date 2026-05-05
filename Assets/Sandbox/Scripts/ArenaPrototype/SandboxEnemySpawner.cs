using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.DreamBattle
{
    [DisallowMultipleComponent]
    public class SandboxEnemySpawner : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string configPath = SandboxConfigPaths.EnemySpawner;
        [SerializeField] [HideInInspector] private float initialDelay = 1f;
        [SerializeField] [HideInInspector] private float spawnInterval = 1.5f;
        [SerializeField] [HideInInspector] private int maxActive = 15;
        [SerializeField] [HideInInspector] private float spawnInset = 1.5f;

        [Header("Pool")]
        [SerializeField] private bool usePool = true;
        [SerializeField] [HideInInspector] private int poolWarmCount = 10;

        [Header("References")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private SandboxArenaBounds arenaBounds;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private SandboxFloorGrid floorGrid;

        private readonly Queue<SandboxEnemyAgent> pool = new Queue<SandboxEnemyAgent>();
        private readonly HashSet<SandboxEnemyAgent> activeEnemies = new HashSet<SandboxEnemyAgent>();
        private float nextSpawnTime;
        private bool poolWarmed;

        public int ActiveCount => activeEnemies.Count;

        public IReadOnlyCollection<SandboxEnemyAgent> ActiveEnemies => activeEnemies;

        // ── Lifecycle ────────────────────────────────

        private void Awake()
        {
            LoadConfig();
        }

        private void Start()
        {
            nextSpawnTime = Time.time + initialDelay;

            if (usePool)
            {
                WarmPool();
            }
        }

        private void OnValidate()
        {
            LoadConfig();
        }

        private void Update()
        {
            if (Time.time >= nextSpawnTime && activeEnemies.Count < maxActive)
            {
                SpawnEnemy();
                nextSpawnTime = Time.time + spawnInterval;
            }
        }

        // ── Spawn ────────────────────────────────────

        private void SpawnEnemy()
        {
            if (enemyPrefab == null) return;

            SandboxEnemyAgent agent = usePool ? GetFromPool() : InstantiateNew();
            if (agent == null) return;

            agent.transform.position = GetSpawnPosition();
            agent.transform.rotation = Quaternion.identity;
            agent.gameObject.SetActive(true);

            agent.SetTarget(playerTarget);
            agent.SetFloorGrid(floorGrid);
            agent.ApplySettingsFromConfig();
            agent.OnDespawnRequested += HandleDespawnRequest;

            activeEnemies.Add(agent);
        }

        private Vector2 GetSpawnPosition()
        {
            if (arenaBounds != null)
            {
                return arenaBounds.GetRandomSpawnPointNearInnerEdge(spawnInset);
            }

            // Fallback: random position on a circle
            float angle = Random.Range(0f, Mathf.PI * 2f);
            return (Vector2)transform.position + new Vector2(
                Mathf.Cos(angle), Mathf.Sin(angle)) * 12f;
        }

        // ── Despawn ──────────────────────────────────

        private void HandleDespawnRequest(SandboxEnemyAgent agent)
        {
            agent.OnDespawnRequested -= HandleDespawnRequest;
            activeEnemies.Remove(agent);

            if (usePool)
            {
                agent.gameObject.SetActive(false);
                pool.Enqueue(agent);
            }
            else
            {
                Destroy(agent.gameObject);
            }
        }

        // ── Pool ─────────────────────────────────────

        private void WarmPool()
        {
            if (poolWarmed) return;
            poolWarmed = true;

            for (int i = 0; i < poolWarmCount; i++)
            {
                SandboxEnemyAgent agent = InstantiateNew();
                agent.gameObject.SetActive(false);
                agent.gameObject.name = enemyPrefab.name + " (Pooled)";
                pool.Enqueue(agent);
            }
        }

        private SandboxEnemyAgent GetFromPool()
        {
            if (pool.Count > 0)
            {
                return pool.Dequeue();
            }

            return InstantiateNew();
        }

        private SandboxEnemyAgent InstantiateNew()
        {
            GameObject obj = Instantiate(enemyPrefab, transform);
            obj.name = enemyPrefab.name;
            return obj.GetComponent<SandboxEnemyAgent>();
        }

        // ── Context Menu ─────────────────────────────

        [ContextMenu("Despawn All Enemies")]
        public void DespawnAll()
        {
            List<SandboxEnemyAgent> toDespawn = new List<SandboxEnemyAgent>(activeEnemies);
            foreach (SandboxEnemyAgent agent in toDespawn)
            {
                HandleDespawnRequest(agent);
            }
        }

        // ── Config ───────────────────────────────────

        private void LoadConfig()
        {
            if (SandboxDreamBattleConfigLoader.TryLoad(configPath, out SpawnerConfig cfg))
            {
                initialDelay = cfg.initialDelay;
                spawnInterval = cfg.spawnInterval;
                maxActive = cfg.maxActive;
                spawnInset = cfg.spawnInset;
                usePool = cfg.usePool;
                poolWarmCount = cfg.poolWarmCount;
            }

            initialDelay = Mathf.Max(0f, initialDelay);
            spawnInterval = Mathf.Max(0.1f, spawnInterval);
            maxActive = Mathf.Max(1, maxActive);
            spawnInset = Mathf.Max(0f, spawnInset);
            poolWarmCount = Mathf.Max(0, poolWarmCount);
        }

        [System.Serializable]
        private class SpawnerConfig
        {
            public float initialDelay = 1f;
            public float spawnInterval = 1.5f;
            public int maxActive = 15;
            public float spawnInset = 1.5f;
            public bool usePool = true;
            public int poolWarmCount = 10;
        }
    }
}
