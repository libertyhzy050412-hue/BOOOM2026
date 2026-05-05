using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sandbox.DreamBattle
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SandboxMouseRing : MonoBehaviour
    {
        [Header("Ring")]
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private SandboxPlayerController playerController;
        [SerializeField] private Color ringColor = new Color(1f, 0.9f, 0.35f, 0.85f);
        [SerializeField] [Range(0.02f, 0.3f)] private float lineWidth = 0.06f;
        [SerializeField] [Range(16, 128)] private int segments = 64;

        [Header("Inner Dot")]
        [SerializeField] private bool showDot = true;
        [SerializeField] private float dotRadius = 0.08f;

        private LineRenderer circleRenderer;
        private LineRenderer dotRenderer;

        // ── Lifecycle ────────────────────────────────

        private void Awake()
        {
            BuildRing();
        }

        private void OnEnable()
        {
            if (circleRenderer == null) BuildRing();
        }

        private void OnValidate()
        {
            segments = Mathf.Clamp(segments, 16, 128);
            lineWidth = Mathf.Clamp(lineWidth, 0.02f, 0.3f);

            if (circleRenderer != null)
            {
                RefreshRingVisuals();
            }
        }

        private void Update()
        {
            FollowMouse();
            UpdateRadius();
        }

        // ── Build ────────────────────────────────────

        private void BuildRing()
        {
            // Ensure child objects
            Transform circleChild = EnsureChild("Ring Circle");
            Transform dotChild = EnsureChild("Ring Dot");

            circleRenderer = circleChild.GetComponent<LineRenderer>();
            if (circleRenderer == null)
                circleRenderer = circleChild.gameObject.AddComponent<LineRenderer>();

            dotRenderer = dotChild.GetComponent<LineRenderer>();
            if (dotRenderer == null)
                dotRenderer = dotChild.gameObject.AddComponent<LineRenderer>();

            ConfigureRenderer(circleRenderer, segments);
            ConfigureRenderer(dotRenderer, 2);

            RefreshRingVisuals();
        }

        private Transform EnsureChild(string name)
        {
            Transform child = transform.Find(name);
            if (child == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;

            return child;
        }

        private void ConfigureRenderer(LineRenderer lr, int positionCount)
        {
            lr.useWorldSpace = false;
            lr.loop = positionCount > 2;
            lr.positionCount = positionCount;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.startColor = ringColor;
            lr.endColor = ringColor;
            lr.sharedMaterial = GetLineMaterial();
            lr.sortingOrder = 100;
        }

        private void RefreshRingVisuals()
        {
            if (circleRenderer != null)
            {
                circleRenderer.positionCount = segments;
                circleRenderer.loop = true;
                circleRenderer.startWidth = lineWidth;
                circleRenderer.endWidth = lineWidth;
                circleRenderer.startColor = ringColor;
                circleRenderer.endColor = ringColor;

                Vector3[] positions = new Vector3[segments];
                float angleStep = 360f / segments * Mathf.Deg2Rad;

                for (int i = 0; i < segments; i++)
                {
                    float angle = i * angleStep;
                    positions[i] = new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0f);
                }

                circleRenderer.SetPositions(positions);
            }

            if (dotRenderer != null)
            {
                dotRenderer.positionCount = 2;
                dotRenderer.loop = false;
                dotRenderer.startWidth = dotRadius * 2f;
                dotRenderer.endWidth = dotRadius * 2f;
                dotRenderer.startColor = ringColor;
                dotRenderer.endColor = ringColor;
                dotRenderer.enabled = showDot;

                dotRenderer.SetPositions(new[] { Vector3.zero, Vector3.forward * 0.001f });
            }
        }

        // ── Follow ───────────────────────────────────

        private void FollowMouse()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null || Camera.main == null) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z));
            worldPos.z = transform.position.z;
            transform.position = worldPos;
#endif
        }

        private void UpdateRadius()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<SandboxPlayerController>();

            if (playerController == null) return;

            float targetRadius = Mathf.Max(0.05f, playerController.BrushPreviewRadius);
            float nextRadius = Mathf.Lerp(radius, targetRadius, 1f - Mathf.Exp(-18f * Time.deltaTime));
            if (Mathf.Abs(nextRadius - radius) < 0.005f) return;

            radius = nextRadius;
            RefreshRingVisuals();
        }

        // ── Material ─────────────────────────────────

        private static Material cachedLineMaterial;

        private static Material GetLineMaterial()
        {
            if (cachedLineMaterial != null) return cachedLineMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                cachedLineMaterial = new Material(shader)
                {
                    name = "Mouse Ring Material",
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            return cachedLineMaterial;
        }
    }
}
