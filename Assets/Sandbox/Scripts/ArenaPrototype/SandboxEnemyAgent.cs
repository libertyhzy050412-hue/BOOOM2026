using UnityEngine;
using System;

namespace Sandbox.DreamBattle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(SandboxActorVitals))]
    public class SandboxEnemyAgent : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string configPath = SandboxConfigPaths.EnemyBasic;
        [SerializeField] [HideInInspector] private float moveSpeed = 3f;
        [SerializeField] [HideInInspector] private float contactDamage = 1f;
        [SerializeField] [HideInInspector] private float paintWidth = 0.8f;
        [SerializeField] [HideInInspector] private float paintInterval = 0.25f;
        [SerializeField] [HideInInspector] [Range(0.05f, 1f)] private float paintOpacity = 0.78f;
        [SerializeField] [HideInInspector] [Range(0.05f, 0.95f)] private float paintHardness = 0.82f;
        [SerializeField] [HideInInspector] [Range(0.1f, 0.9f)] private float paintSpacingRatio = 0.42f;

        [Header("References")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private SandboxFloorGrid floorGrid;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color onDreamTint = new Color(0.7f, 0.55f, 0.9f);
        [SerializeField] private Color onNightmareTint = Color.white;

        [Header("Hit Feedback")]
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float hitFlashDuration = 0.12f;
        [SerializeField] private float hitFlashCooldown = 0.15f;

        private Rigidbody2D cachedRigidbody;
        private SandboxActorVitals vitals;
        private Vector2 lastPaintPos;
        private float lastPaintTime;
        private bool isDead;
        private float nextHitFlashAllowedTime;
        private float hitFlashEndTime;
        private bool isFlashing;

        // Events for spawner integration
        public event Action<SandboxEnemyAgent> OnDespawnRequested;

        public bool IsDead => isDead;

        // ── Lifecycle ────────────────────────────────

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody2D>();
            vitals = GetComponent<SandboxActorVitals>();
        }

        private void OnEnable()
        {
            LoadConfig();
            vitals.ResetVitals();
            vitals.SetDamageEnabled(true);
            isDead = false;
            lastPaintPos = transform.position;
            lastPaintTime = 0f;

            vitals.Died += OnDied;
        }

        private void OnDisable()
        {
            vitals.Died -= OnDied;
            isFlashing = false;

            if (spriteRenderer != null)
                spriteRenderer.color = onNightmareTint;
        }

        private void OnValidate()
        {
            LoadConfig();
        }

        private void Update()
        {
            if (isDead) return;

            ApplyFloorEffects();
            PaintTrail();
        }

        private void FixedUpdate()
        {
            if (isDead || playerTarget == null) return;

            Vector2 toPlayer = (Vector2)playerTarget.position - (Vector2)transform.position;
            Vector2 direction = toPlayer.normalized;
            cachedRigidbody.linearVelocity = direction * moveSpeed;
        }

        // ── Damage ───────────────────────────────────

        public void TakeDamage(float amount)
        {
            if (isDead) return;
            vitals.ApplyDamage(amount);

            if (Time.time >= nextHitFlashAllowedTime)
            {
                nextHitFlashAllowedTime = Time.time + hitFlashCooldown;
                hitFlashEndTime = Time.time + hitFlashDuration;
                isFlashing = true;
            }
        }

        private void OnDied()
        {
            isDead = true;
            cachedRigidbody.linearVelocity = Vector2.zero;
            OnDespawnRequested?.Invoke(this);
        }

        // ── Painting ─────────────────────────────────

        private void PaintTrail()
        {
            if (floorGrid == null) return;
            if (Time.time - lastPaintTime < paintInterval) return;

            Vector2 currentPos = transform.position;
            if (Vector2.Distance(currentPos, lastPaintPos) > 0.05f)
            {
                PaintNightmareTrailSegment(lastPaintPos, currentPos);
                lastPaintPos = currentPos;
            }

            lastPaintTime = Time.time;
        }

        private void PaintNightmareTrailSegment(Vector2 from, Vector2 to)
        {
            float radius = Mathf.Max(0.05f, paintWidth * 0.5f);
            float spacing = Mathf.Max(radius * paintSpacingRatio, 0.03f);

            floorGrid.PaintNightmareStroke(from, to, radius, paintOpacity, paintHardness, spacing);
            floorGrid.PaintNightmareStamp(
                to,
                radius * 0.92f,
                Mathf.Clamp01(paintOpacity * 0.7f),
                Mathf.Max(0.05f, paintHardness - 0.08f));
        }

        // ── Floor Effects ────────────────────────────

        private void ApplyFloorEffects()
        {
            if (floorGrid == null) return;

            bool onDream = floorGrid.IsDreamAt(transform.position);

            // Apply DOT from dream tiles
            if (onDream)
            {
                float dotDamage = floorGrid.DreamDamagePerSecond * Time.deltaTime;
                vitals.ApplyDamage(dotDamage);
            }

            // Slow on dream tiles
            float speedMultiplier = onDream ? 0.5f : 1f;
            cachedRigidbody.linearVelocity *= speedMultiplier;

            // Visual: hit flash takes priority, then tint
            if (spriteRenderer == null) return;

            if (isFlashing)
            {
                float elapsed = Time.time - (hitFlashEndTime - hitFlashDuration);
                float t = elapsed / hitFlashDuration;

                if (t >= 1f)
                {
                    isFlashing = false;
                    spriteRenderer.color = onDream ? onDreamTint : onNightmareTint;
                }
                else
                {
                    spriteRenderer.color = Color.Lerp(hitFlashColor, onDream ? onDreamTint : onNightmareTint, t);
                }
            }
            else
            {
                Color targetTint = onDream ? onDreamTint : onNightmareTint;
                spriteRenderer.color = Color.Lerp(spriteRenderer.color, targetTint, 8f * Time.deltaTime);
            }
        }

        // ── Config ───────────────────────────────────

        private void LoadConfig()
        {
            if (SandboxDreamBattleConfigLoader.TryLoad(configPath, out EnemyConfig cfg))
            {
                moveSpeed = cfg.moveSpeed;
                contactDamage = cfg.contactDamage;
                paintWidth = cfg.paintWidth;
                paintInterval = cfg.paintInterval;
                paintOpacity = cfg.paintOpacity;
                paintHardness = cfg.paintHardness;
                paintSpacingRatio = cfg.paintSpacingRatio;
            }

            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            contactDamage = Mathf.Max(0f, contactDamage);
            paintWidth = Mathf.Max(0.1f, paintWidth);
            paintInterval = Mathf.Max(0.05f, paintInterval);
            paintOpacity = Mathf.Clamp01(paintOpacity);
            paintHardness = Mathf.Clamp(paintHardness, 0.05f, 0.95f);
            paintSpacingRatio = Mathf.Clamp(paintSpacingRatio, 0.1f, 0.9f);
        }

        public void ApplySettingsFromConfig()
        {
            LoadConfig();

            if (SandboxDreamBattleConfigLoader.TryLoad(configPath, out EnemyConfig cfg))
            {
                vitals.ApplySettings(cfg.maxHealth, true, true);
            }
        }

        public void SetTarget(Transform target)
        {
            playerTarget = target;
        }

        public void SetFloorGrid(SandboxFloorGrid grid)
        {
            floorGrid = grid;
        }

        // ── Config type ──────────────────────────────

        [Serializable]
        private class EnemyConfig
        {
            public float maxHealth = 10f;
            public float moveSpeed = 3f;
            public float contactDamage = 1f;
            public float paintWidth = 0.8f;
            public float paintInterval = 0.25f;
            public float paintOpacity = 0.78f;
            public float paintHardness = 0.82f;
            public float paintSpacingRatio = 0.42f;
        }
    }
}
