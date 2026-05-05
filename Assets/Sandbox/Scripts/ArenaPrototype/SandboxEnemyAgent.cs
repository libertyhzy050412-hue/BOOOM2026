using UnityEngine;
using System;
using System.Collections;

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
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private float hitFlashDuration = 0.12f;
        [SerializeField] private float hitFlashCooldown = 0.15f;

        [Header("Death Feedback")]
        [SerializeField] private Color deathFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private float deathFeedbackDuration = 0.18f;
        [SerializeField] private float deathScaleMultiplier = 1.18f;
        [SerializeField] private float deathHitStopDuration = 0.035f;
        [SerializeField] [Range(0.01f, 1f)] private float deathHitStopTimeScale = 0.08f;

        private Rigidbody2D cachedRigidbody;
        private Collider2D[] cachedColliders;
        private SandboxActorVitals vitals;
        private Vector2 lastPaintPos;
        private float lastPaintTime;
        private bool isDead;
        private float nextHitFlashAllowedTime;
        private float hitFlashEndTime;
        private bool isFlashing;
        private Coroutine deathFeedbackRoutine;
        private Vector3 initialLocalScale;
        private bool deathHitStopApplied;

        private static int activeDeathHitStopCount;
        private static float cachedTimeScale = 1f;

        // Events for spawner integration
        public event Action<SandboxEnemyAgent> OnDespawnRequested;

        public bool IsDead => isDead;

        // ── Lifecycle ────────────────────────────────

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody2D>();
            cachedColliders = GetComponents<Collider2D>();
            vitals = GetComponent<SandboxActorVitals>();
            initialLocalScale = transform.localScale;
        }

        private void OnEnable()
        {
            LoadConfig();
            vitals.ResetVitals();
            vitals.SetDamageEnabled(true);
            isDead = false;
            lastPaintPos = transform.position;
            lastPaintTime = 0f;
            nextHitFlashAllowedTime = 0f;
            isFlashing = false;
            ResetVisualState();
            SetCollidersEnabled(true);

            vitals.Died += OnDied;
        }

        private void OnDisable()
        {
            vitals.Died -= OnDied;
            isFlashing = false;

            if (deathFeedbackRoutine != null)
            {
                StopCoroutine(deathFeedbackRoutine);
                deathFeedbackRoutine = null;
            }

            RestoreDeathHitStopIfNeeded();

            SetCollidersEnabled(true);
            ResetVisualState();
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
            bool tookDamage = vitals.ApplyDamage(amount);
            if (!tookDamage || vitals.IsDead) return;

            if (Time.time >= nextHitFlashAllowedTime)
            {
                nextHitFlashAllowedTime = Time.time + hitFlashCooldown;
                hitFlashEndTime = Time.time + hitFlashDuration;
                isFlashing = true;
            }
        }

        private void OnDied()
        {
            if (isDead) return;

            isDead = true;
            vitals.SetDamageEnabled(false);
            cachedRigidbody.linearVelocity = Vector2.zero;
            isFlashing = false;
            SetCollidersEnabled(false);

            if (deathFeedbackRoutine != null)
                StopCoroutine(deathFeedbackRoutine);

            deathFeedbackRoutine = StartCoroutine(PlayDeathFeedback());
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
                if (vitals.IsDead) return;
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
            deathFeedbackDuration = Mathf.Max(0.05f, deathFeedbackDuration);
            deathScaleMultiplier = Mathf.Max(1f, deathScaleMultiplier);
            deathHitStopDuration = Mathf.Max(0f, deathHitStopDuration);
            deathHitStopTimeScale = Mathf.Clamp(deathHitStopTimeScale, 0.01f, 1f);
        }

        private IEnumerator PlayDeathFeedback()
        {
            if (spriteRenderer == null)
            {
                deathFeedbackRoutine = null;
                OnDespawnRequested?.Invoke(this);
                yield break;
            }

            yield return PlayDeathHitStop();

            Color startColor = Color.Lerp(spriteRenderer.color, deathFlashColor, 0.9f);
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
            Vector3 endScale = initialLocalScale * deathScaleMultiplier;
            float elapsed = 0f;

            while (elapsed < deathFeedbackDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / deathFeedbackDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                spriteRenderer.color = Color.Lerp(startColor, endColor, eased);
                transform.localScale = Vector3.Lerp(initialLocalScale, endScale, eased);
                yield return null;
            }

            spriteRenderer.color = endColor;
            transform.localScale = endScale;
            deathFeedbackRoutine = null;
            OnDespawnRequested?.Invoke(this);
        }

        private IEnumerator PlayDeathHitStop()
        {
            if (deathHitStopDuration <= 0f || deathHitStopTimeScale >= 0.999f)
                yield break;

            if (activeDeathHitStopCount == 0)
            {
                cachedTimeScale = Time.timeScale;
                Time.timeScale = cachedTimeScale * deathHitStopTimeScale;
            }

            activeDeathHitStopCount++;
            deathHitStopApplied = true;

            yield return new WaitForSecondsRealtime(deathHitStopDuration);

            RestoreDeathHitStopIfNeeded();
        }

        private void RestoreDeathHitStopIfNeeded()
        {
            if (!deathHitStopApplied)
                return;

            deathHitStopApplied = false;
            activeDeathHitStopCount = Mathf.Max(0, activeDeathHitStopCount - 1);

            if (activeDeathHitStopCount == 0)
            {
                Time.timeScale = cachedTimeScale;
            }
        }

        private void ResetVisualState()
        {
            transform.localScale = initialLocalScale;

            if (spriteRenderer == null) return;

            Color resetColor = onNightmareTint;
            resetColor.a = 1f;
            spriteRenderer.color = resetColor;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (cachedColliders == null) return;

            for (int i = 0; i < cachedColliders.Length; i++)
            {
                cachedColliders[i].enabled = enabled;
            }
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
