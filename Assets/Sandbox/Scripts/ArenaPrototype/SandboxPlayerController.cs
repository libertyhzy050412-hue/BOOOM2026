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
        [SerializeField] [HideInInspector] private float moveSpeed = 6f;
        [SerializeField] [HideInInspector] private float acceleration = 30f;
        [SerializeField] [HideInInspector] private float deceleration = 36f;

        [Header("Pour Paint (Summon)")]
        [SerializeField] private float paintAreaPerSecond = 8f;
        [SerializeField] private float pourMaxRadius = 2.5f;

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

            if (fireHeld)
            {
                Vector2 pos = mouseWorldPos;

                // Connect frames with a line to bridge fast cursor movement
                if (hasLastPourPos && Vector2.Distance(pos, lastPourPos) > 0.01f)
                {
                    float lineWidth = Mathf.Max(pourRadius * 1.6f, 0.6f);
                    floorGrid.SetDreamLine(lastPourPos, pos, lineWidth);
                }

                // Expand pour radius continuously — no reset on cursor move
                float currentArea = Mathf.PI * pourRadius * pourRadius;
                float newArea = currentArea + paintAreaPerSecond * Time.deltaTime;
                pourRadius = Mathf.Sqrt(newArea / Mathf.PI);
                pourRadius = Mathf.Min(pourRadius, pourMaxRadius);

                floorGrid.SetDreamCircle(pos, pourRadius);

                lastPourPos = pos;
                hasLastPourPos = true;
            }
            else
            {
                pourRadius = 0f;
                hasLastPourPos = false;
            }
#endif
        }

        // ── Footstep Paint ───────────────────────────

        private void PaintFootstep()
        {
            if (floorGrid == null) return;
            if (Time.time - lastFootstepTime < footstepInterval) return;

            Vector2 pos = transform.position;
            if (moveInput.sqrMagnitude > 0.001f && Vector2.Distance(pos, lastFootstepPos) > 0.02f)
            {
                floorGrid.SetDreamLine(lastFootstepPos, pos, footstepWidth);
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
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
        }

        [System.Serializable]
        private class PlayerMovementConfig
        {
            public float moveSpeed = 6f;
            public float acceleration = 30f;
            public float deceleration = 36f;
        }
    }
}
