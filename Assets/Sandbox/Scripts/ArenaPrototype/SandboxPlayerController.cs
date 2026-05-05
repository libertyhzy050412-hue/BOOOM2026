using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sandbox.DreamBattle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class SandboxPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private string configPath = SandboxConfigPaths.PlayerMovement;
        [SerializeField] private string brushConfigPath = SandboxConfigPaths.PlayerBrush;
        [SerializeField] [HideInInspector] private float moveSpeed = 6f;
        [SerializeField] [HideInInspector] private float acceleration = 30f;
        [SerializeField] [HideInInspector] private float deceleration = 36f;

        [Header("Pour Paint (Summon)")]
        [SerializeField] [HideInInspector] private float brushMinRadius = 0.4f;
        [SerializeField] [HideInInspector] private float brushMaxRadius = 1.2f;
        [SerializeField] [HideInInspector] private float pourMaxRadius = 2.2f;
        [SerializeField] [HideInInspector] [Range(0.05f, 0.95f)] private float brushHardness = 0.58f;
        [SerializeField] [HideInInspector] [Range(0.1f, 0.9f)] private float brushSpacingRatio = 0.3f;
        [SerializeField] [HideInInspector] private float brushStrokeFlowPerSecond = 14f;
        [SerializeField] [HideInInspector] private float brushStationaryFlowPerSecond = 18f;
        [SerializeField] [HideInInspector] private float brushSpreadPerSecond = 1.1f;
        [SerializeField] [HideInInspector] private float brushThinSpeedStart = 2f;
        [SerializeField] [HideInInspector] private float brushThinSpeedEnd = 16f;
        [SerializeField] [HideInInspector] [Range(0.15f, 1f)] private float fastMoveRadiusFactor = 0.38f;
        [SerializeField] [HideInInspector] private float cursorSpeedSmoothing = 14f;
        [SerializeField] [HideInInspector] [Range(0.05f, 1f)] private float fastStrokeMinOpacity = 0.28f;
        [SerializeField] [HideInInspector] [Range(0.05f, 1f)] private float fastStrokeCoreOpacity = 0.62f;
        [SerializeField] [HideInInspector] [Range(0.2f, 0.95f)] private float fastStrokeCoreRadiusFactor = 0.55f;
        [SerializeField] [HideInInspector] [Range(0.05f, 0.5f)] private float fastStrokeOpacityBoost = 0.18f;

        [Header("Footstep Paint")]
        [SerializeField] private float footstepWidth = 1.2f;
        [SerializeField] private float footstepInterval = 0.08f;

        [Header("References")]
        [SerializeField] private SandboxFloorGrid floorGrid;
        [SerializeField] private Transform aimIndicator;

        [Header("Visual & Feedback")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float hitFlashDuration = 0.12f;
        [SerializeField] private float hitInvincibilityWindow = 0.5f;

        private Rigidbody2D cachedRigidbody;
        private SandboxActorVitals vitals;
        private Vector2 moveInput;
        private Vector2 aimDirection;
        private Vector2 mouseWorldPos;
        private bool fireHeld;
        private float pourRadius;
        private Vector2 lastPourPos;
        private bool hasLastPourPos;
        private float lastPourStampRadius;
        private float lastPourThin01;
        private float smoothedCursorSpeed;
        private float stationaryPourTime;
        private Vector2 lastFootstepPos;
        private float lastFootstepTime;

        // Hit flash
        private float nextCanBeHitTime;
        private float hitFlashEndTime;
        private bool isFlashing;
        private Color baseColor = Color.white;

        public Vector2 AimDirection => aimDirection;
        public Vector2 MouseWorldPos => mouseWorldPos;
        public float PourRadius => pourRadius;
        public float PourMaxRadius => pourMaxRadius;
        public float BrushPreviewRadius => fireHeld ? pourRadius : brushMaxRadius;

        // ── Lifecycle ────────────────────────────────

        private void Reset()
        {
            LoadConfig();
            SetupRigidbody();
        }

        private void Awake()
        {
            LoadConfig();
            SetupRigidbody();
            vitals = GetComponent<SandboxActorVitals>();
            lastFootstepPos = transform.position;
            lastPourStampRadius = brushMaxRadius;
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
        }

        private void OnEnable()
        {
            if (vitals != null) vitals.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (vitals != null) vitals.Damaged -= OnDamaged;
            isFlashing = false;
            fireHeld = false;
            pourRadius = 0f;
            hasLastPourPos = false;
            stationaryPourTime = 0f;
        }

        private void OnValidate() { LoadConfig(); }

        private void Update()
        {
            moveInput = ReadMoveInput();
            ReadAimInput();
            HandlePourPaint();
            PaintFootstep();
            UpdateHitFlash();
            UpdateAimIndicator();
        }

        private void FixedUpdate()
        {
            Vector2 desired = moveInput * moveSpeed;
            float maxDelta = (moveInput.sqrMagnitude > 0.0001f ? acceleration : deceleration) * Time.fixedDeltaTime;
            cachedRigidbody.linearVelocity = Vector2.MoveTowards(
                cachedRigidbody.linearVelocity, desired, maxDelta);
        }

        private void SetupRigidbody()
        {
            cachedRigidbody = GetComponent<Rigidbody2D>();
            cachedRigidbody.gravityScale = 0f;
            cachedRigidbody.freezeRotation = true;
            cachedRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            cachedRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // ── Input ────────────────────────────────────

        private static Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 move = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
            }
            if (Gamepad.current != null) move += Gamepad.current.leftStick.ReadValue();
            return Vector2.ClampMagnitude(move, 1f);
#else
            return Vector2.zero;
#endif
        }

        private void ReadAimInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null || Camera.main == null) return;
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 wp = Camera.main.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z));
            mouseWorldPos = wp;
            aimDirection = (mouseWorldPos - (Vector2)transform.position).normalized;
#endif
        }

        // ── Pour Paint ───────────────────────────────

        private void HandlePourPaint()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null || floorGrid == null) return;

            fireHeld = Mouse.current.leftButton.isPressed;
            Vector2 pos = mouseWorldPos;

            if (fireHeld)
            {
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);

                if (!hasLastPourPos)
                {
                    float startRadius = brushMaxRadius;
                    floorGrid.PaintDreamStamp(
                        pos,
                        startRadius,
                        ComputeBrushOpacity(brushStationaryFlowPerSecond, dt),
                        brushHardness);

                    pourRadius = startRadius;
                    lastPourPos = pos;
                    lastPourStampRadius = startRadius;
                    lastPourThin01 = 0f;
                    hasLastPourPos = true;
                    stationaryPourTime = 0f;
                    return;
                }

                float cursorDistance = Vector2.Distance(pos, lastPourPos);
                float rawCursorSpeed = cursorDistance / dt;
                float speedLerp = 1f - Mathf.Exp(-cursorSpeedSmoothing * dt);
                smoothedCursorSpeed = Mathf.Lerp(smoothedCursorSpeed, rawCursorSpeed, speedLerp);

                float thin01 = Mathf.InverseLerp(brushThinSpeedStart, brushThinSpeedEnd, smoothedCursorSpeed);
                float targetRadius = Mathf.Lerp(brushMaxRadius, brushMaxRadius * fastMoveRadiusFactor, thin01);
                targetRadius = Mathf.Clamp(targetRadius, brushMinRadius, brushMaxRadius);

                float stationaryThreshold = Mathf.Max(targetRadius * brushSpacingRatio, 0.02f);
                if (cursorDistance <= stationaryThreshold)
                {
                    stationaryPourTime += dt;
                    float buildupRadius = Mathf.Min(targetRadius + stationaryPourTime * brushSpreadPerSecond, pourMaxRadius);
                    floorGrid.PaintDreamStamp(
                        pos,
                        buildupRadius,
                        ComputeBrushOpacity(brushStationaryFlowPerSecond, dt),
                        brushHardness);

                    pourRadius = buildupRadius;
                    lastPourStampRadius = buildupRadius;
                }
                else
                {
                    stationaryPourTime = 0f;
                    PaintDreamStroke(lastPourPos, pos, lastPourStampRadius, targetRadius, lastPourThin01, thin01);
                    pourRadius = targetRadius;
                    lastPourStampRadius = targetRadius;
                }

                lastPourThin01 = thin01;
                lastPourPos = pos;
            }
            else
            {
                pourRadius = 0f;
                hasLastPourPos = false;
                lastPourStampRadius = brushMaxRadius;
                lastPourThin01 = 0f;
                stationaryPourTime = 0f;
            }
#endif
        }

        private void PaintDreamStroke(Vector2 from, Vector2 to, float fromRadius, float toRadius, float fromThin01, float toThin01)
        {
            float distance = Vector2.Distance(from, to);
            if (distance <= 0.0001f)
            {
                float singleOpacity = ComputeReadableStrokeOpacity(
                    ComputeBrushOpacity(brushStrokeFlowPerSecond, Time.deltaTime),
                    toThin01);

                floorGrid.PaintDreamStamp(
                    to,
                    toRadius,
                    singleOpacity,
                    brushHardness);

                PaintDreamStrokeCore(to, toRadius, toThin01);
                return;
            }

            float averageRadius = Mathf.Max(0.01f, (fromRadius + toRadius) * 0.5f);
            float spacing = Mathf.Max(averageRadius * brushSpacingRatio, 0.03f);
            int stampCount = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));
            float stampDeltaTime = Mathf.Max(Time.deltaTime / (stampCount + 1), 0.0001f);
            float baseOpacity = ComputeBrushOpacity(brushStrokeFlowPerSecond, stampDeltaTime);

            for (int i = 0; i <= stampCount; i++)
            {
                float t = i / (float)stampCount;
                Vector2 stampPos = Vector2.Lerp(from, to, t);
                float radius = Mathf.Lerp(fromRadius, toRadius, t);
                float thin01 = Mathf.Lerp(fromThin01, toThin01, t);
                float opacity = ComputeReadableStrokeOpacity(baseOpacity, thin01);
                floorGrid.PaintDreamStamp(stampPos, radius, opacity, brushHardness);
                PaintDreamStrokeCore(stampPos, radius, thin01);
            }
        }

        private void PaintDreamStrokeCore(Vector2 stampPos, float radius, float thin01)
        {
            float coreRadius = Mathf.Max(brushMinRadius * 0.45f, radius * fastStrokeCoreRadiusFactor);
            float coreOpacity = Mathf.Clamp01(fastStrokeCoreOpacity + fastStrokeOpacityBoost * thin01 * 0.5f);
            float coreHardness = Mathf.Clamp01(Mathf.Lerp(brushHardness, 0.96f, 0.7f));

            floorGrid.PaintDreamStamp(stampPos, coreRadius, coreOpacity, coreHardness);
        }

        private float ComputeReadableStrokeOpacity(float baseOpacity, float thin01)
        {
            float boostedFloor = fastStrokeMinOpacity + fastStrokeOpacityBoost * thin01;
            return Mathf.Clamp01(Mathf.Max(baseOpacity, boostedFloor));
        }

        private static float ComputeBrushOpacity(float flowPerSecond, float deltaTime)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, flowPerSecond) * Mathf.Max(0f, deltaTime));
        }

        // ── Footstep Paint ───────────────────────────

        private void PaintFootstep()
        {
            if (floorGrid == null) return;
            if (Time.time - lastFootstepTime < footstepInterval) return;

            Vector2 pos = transform.position;
            if (moveInput.sqrMagnitude > 0.001f && Vector2.Distance(pos, lastFootstepPos) > 0.02f)
            {
                float footstepRadius = Mathf.Max(0.05f, footstepWidth * 0.5f);
                floorGrid.PaintDreamStroke(lastFootstepPos, pos, footstepRadius, 0.72f, 0.82f, footstepRadius * 0.28f);
                lastFootstepPos = pos;
                lastFootstepTime = Time.time;
            }
        }

        // ── Damage & Feedback ────────────────────────

        public void TakeDamage(float amount)
        {
            if (Time.time < nextCanBeHitTime) return;
            nextCanBeHitTime = Time.time + hitInvincibilityWindow;
            if (vitals != null) vitals.ApplyDamage(amount);
        }

        private void OnDamaged(float damage, float currentHealth)
        {
            isFlashing = true;
            hitFlashEndTime = Time.time + hitFlashDuration;
        }

        private void UpdateHitFlash()
        {
            if (spriteRenderer == null) return;
            if (isFlashing)
            {
                float elapsed = Time.time - (hitFlashEndTime - hitFlashDuration);
                float t = elapsed / hitFlashDuration;
                if (t >= 1f) { isFlashing = false; spriteRenderer.color = baseColor; }
                else spriteRenderer.color = Color.Lerp(hitFlashColor, baseColor, t);
            }
        }

        // ── Visual ───────────────────────────────────

        private void UpdateAimIndicator()
        {
            if (aimIndicator == null) return;
            aimIndicator.gameObject.SetActive(aimDirection.sqrMagnitude > 0.001f);
            if (aimDirection.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                aimIndicator.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        // ── Config ───────────────────────────────────

        private void LoadConfig()
        {
            if (SandboxDreamBattleConfigLoader.TryLoad(configPath, out PlayerMovementConfig cfg))
            {
                moveSpeed = cfg.moveSpeed;
                acceleration = cfg.acceleration;
                deceleration = cfg.deceleration;
            }

            if (SandboxDreamBattleConfigLoader.TryLoad(brushConfigPath, out PlayerBrushConfig brushCfg))
            {
                brushMinRadius = brushCfg.brushMinRadius;
                brushMaxRadius = brushCfg.brushMaxRadius;
                pourMaxRadius = brushCfg.brushStationaryMaxRadius;
                brushHardness = brushCfg.brushHardness;
                brushSpacingRatio = brushCfg.brushSpacingRatio;
                brushStrokeFlowPerSecond = brushCfg.brushStrokeFlowPerSecond;
                brushStationaryFlowPerSecond = brushCfg.brushStationaryFlowPerSecond;
                brushSpreadPerSecond = brushCfg.brushSpreadPerSecond;
                brushThinSpeedStart = brushCfg.brushThinSpeedStart;
                brushThinSpeedEnd = brushCfg.brushThinSpeedEnd;
                fastMoveRadiusFactor = brushCfg.fastMoveRadiusFactor;
                cursorSpeedSmoothing = brushCfg.cursorSpeedSmoothing;
                fastStrokeMinOpacity = brushCfg.fastStrokeMinOpacity;
                fastStrokeCoreOpacity = brushCfg.fastStrokeCoreOpacity;
                fastStrokeCoreRadiusFactor = brushCfg.fastStrokeCoreRadiusFactor;
                fastStrokeOpacityBoost = brushCfg.fastStrokeOpacityBoost;
            }

            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            brushMinRadius = Mathf.Max(0.05f, brushMinRadius);
            brushMaxRadius = Mathf.Max(brushMinRadius, brushMaxRadius);
            pourMaxRadius = Mathf.Max(brushMaxRadius, pourMaxRadius);
            brushHardness = Mathf.Clamp(brushHardness, 0.05f, 0.95f);
            brushSpacingRatio = Mathf.Clamp(brushSpacingRatio, 0.1f, 0.9f);
            brushStrokeFlowPerSecond = Mathf.Max(0.1f, brushStrokeFlowPerSecond);
            brushStationaryFlowPerSecond = Mathf.Max(0.1f, brushStationaryFlowPerSecond);
            brushSpreadPerSecond = Mathf.Max(0f, brushSpreadPerSecond);
            brushThinSpeedStart = Mathf.Max(0f, brushThinSpeedStart);
            brushThinSpeedEnd = Mathf.Max(brushThinSpeedStart + 0.01f, brushThinSpeedEnd);
            fastMoveRadiusFactor = Mathf.Clamp(fastMoveRadiusFactor, 0.15f, 1f);
            cursorSpeedSmoothing = Mathf.Max(0.1f, cursorSpeedSmoothing);
            fastStrokeMinOpacity = Mathf.Clamp01(fastStrokeMinOpacity);
            fastStrokeCoreOpacity = Mathf.Clamp01(fastStrokeCoreOpacity);
            fastStrokeCoreRadiusFactor = Mathf.Clamp(fastStrokeCoreRadiusFactor, 0.2f, 0.95f);
            fastStrokeOpacityBoost = Mathf.Clamp(fastStrokeOpacityBoost, 0.05f, 0.5f);
        }

        [System.Serializable]
        private class PlayerMovementConfig
        {
            public float moveSpeed = 6f;
            public float acceleration = 30f;
            public float deceleration = 36f;
        }

        [System.Serializable]
        private class PlayerBrushConfig
        {
            public float brushMinRadius = 0.4f;
            public float brushMaxRadius = 1.2f;
            public float brushStationaryMaxRadius = 2.2f;
            public float brushHardness = 0.58f;
            public float brushSpacingRatio = 0.3f;
            public float brushStrokeFlowPerSecond = 14f;
            public float brushStationaryFlowPerSecond = 18f;
            public float brushSpreadPerSecond = 1.1f;
            public float brushThinSpeedStart = 2f;
            public float brushThinSpeedEnd = 16f;
            public float fastMoveRadiusFactor = 0.38f;
            public float cursorSpeedSmoothing = 14f;
            public float fastStrokeMinOpacity = 0.28f;
            public float fastStrokeCoreOpacity = 0.62f;
            public float fastStrokeCoreRadiusFactor = 0.55f;
            public float fastStrokeOpacityBoost = 0.18f;
        }
    }
}
