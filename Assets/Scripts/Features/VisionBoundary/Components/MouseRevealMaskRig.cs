using UnityEngine;

namespace VisionBoundary
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MouseRevealFollower))]
    public class MouseRevealMaskRig : MonoBehaviour
    {
        private const string MaskVisualObjectName = "Reveal Mask Visual";
        private const string RingVisualObjectName = "Reveal Ring Visual";

        [Header("Reveal")]
        [SerializeField] private float radius = 2f;
        [SerializeField] [Range(0f, 0.45f)] private float edgeSoftness = 0.08f;
        [SerializeField] private int textureResolution = 256;

        [Header("Ring Visual")]
        [SerializeField] private bool showRing = true;
        [SerializeField] private Color ringColor = new Color(1f, 1f, 1f, 0.92f);
        [SerializeField] [Range(0.01f, 0.25f)] private float ringThickness = 0.05f;
        [SerializeField] [Range(0f, 0.08f)] private float pulseAmplitude = 0.02f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private int ringSortingOrder = 50;

        [SerializeField] [HideInInspector] private Transform maskVisual;
        [SerializeField] [HideInInspector] private Transform ringVisual;
        [SerializeField] [HideInInspector] private SpriteMask spriteMask;
        [SerializeField] [HideInInspector] private SpriteRenderer ringRenderer;
        [SerializeField] [HideInInspector] private Texture2D generatedMaskTexture;
        [SerializeField] [HideInInspector] private Sprite generatedMaskSprite;
        [SerializeField] [HideInInspector] private Texture2D generatedRingTexture;
        [SerializeField] [HideInInspector] private Sprite generatedRingSprite;
        [SerializeField] [HideInInspector] private Material ringMaterial;
        [SerializeField] [HideInInspector] private int cachedTextureResolution;
        [SerializeField] [HideInInspector] private float cachedEdgeSoftness;
        [SerializeField] [HideInInspector] private float cachedRingThickness;

        public Vector2 RevealCenterWorld => transform.position;

        public float RevealRadiusWorld => radius;

        public SpriteMask RevealMask => spriteMask;

        private void Reset()
        {
            EnsureRig(true);
        }

        private void OnEnable()
        {
            EnsureRig(true);
        }

        private void OnValidate()
        {
            EnsureRig(false);
        }

        private void OnDestroy()
        {
            DestroyGeneratedAssets();
        }

        private void LateUpdate()
        {
            UpdateRingVisual();
        }

        private void EnsureRig(bool createMissingObjects)
        {
            radius = Mathf.Max(0.05f, radius);
            textureResolution = Mathf.Clamp(textureResolution, 32, 1024);
            ringThickness = Mathf.Clamp(ringThickness, 0.01f, 0.25f);
            pulseSpeed = Mathf.Max(0f, pulseSpeed);

            EnsureMaskVisual(createMissingObjects);
            EnsureRingVisual(createMissingObjects);

            if (spriteMask == null || ringRenderer == null)
            {
                return;
            }

            EnsureGeneratedSprites();
            spriteMask.sprite = generatedMaskSprite;
            ringRenderer.sprite = generatedRingSprite;
            ringRenderer.enabled = showRing;
            ringRenderer.sortingOrder = ringSortingOrder;
            ringRenderer.maskInteraction = SpriteMaskInteraction.None;
            ringRenderer.sharedMaterial = GetRingMaterial();
            UpdateRingVisual();
        }

        private void EnsureMaskVisual(bool createMissingObjects)
        {
            if (maskVisual == null)
            {
                Transform existingChild = transform.Find(MaskVisualObjectName);
                if (existingChild != null)
                {
                    maskVisual = existingChild;
                }
                else if (createMissingObjects)
                {
                    GameObject child = new GameObject(MaskVisualObjectName);
                    child.transform.SetParent(transform, false);
                    maskVisual = child.transform;
                }
            }

            if (maskVisual == null)
            {
                return;
            }

            maskVisual.localPosition = Vector3.zero;
            maskVisual.localRotation = Quaternion.identity;
            maskVisual.localScale = Vector3.one * (radius * 2f);

            if (spriteMask == null)
            {
                spriteMask = maskVisual.GetComponent<SpriteMask>();
            }

            if (spriteMask == null && createMissingObjects)
            {
                spriteMask = maskVisual.gameObject.AddComponent<SpriteMask>();
            }

            if (spriteMask != null)
            {
                spriteMask.enabled = true;
            }
        }

        private void EnsureRingVisual(bool createMissingObjects)
        {
            if (ringVisual == null)
            {
                Transform existingChild = transform.Find(RingVisualObjectName);
                if (existingChild != null)
                {
                    ringVisual = existingChild;
                }
                else if (createMissingObjects)
                {
                    GameObject child = new GameObject(RingVisualObjectName);
                    child.transform.SetParent(transform, false);
                    ringVisual = child.transform;
                }
            }

            if (ringVisual == null)
            {
                return;
            }

            ringVisual.localPosition = Vector3.zero;
            ringVisual.localRotation = Quaternion.identity;

            if (ringRenderer == null || ringRenderer.transform != ringVisual)
            {
                ringRenderer = ringVisual.GetComponent<SpriteRenderer>();
            }

            if (ringRenderer == null && createMissingObjects)
            {
                ringRenderer = ringVisual.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        private void EnsureGeneratedSprites()
        {
            if (!NeedsRebuild())
            {
                return;
            }

            DestroyGeneratedAssets();

            generatedMaskTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false)
            {
                name = "Reveal Mask Texture",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            generatedRingTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false)
            {
                name = "Reveal Ring Texture",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float maxDistance = (textureResolution - 1) * 0.5f;
            float hardRadius = maxDistance * (1f - edgeSoftness);
            float ringHalfWidth = Mathf.Max(1f, maxDistance * ringThickness * 0.5f);
            Color[] maskPixels = new Color[textureResolution * textureResolution];
            Color[] ringPixels = new Color[textureResolution * textureResolution];

            for (int y = 0; y < textureResolution; y++)
            {
                for (int x = 0; x < textureResolution; x++)
                {
                    float offsetX = x - maxDistance;
                    float offsetY = y - maxDistance;
                    float distance = Mathf.Sqrt(offsetX * offsetX + offsetY * offsetY);

                    float maskAlpha = distance <= hardRadius
                        ? 1f
                        : 1f - Mathf.InverseLerp(hardRadius, maxDistance, distance);

                    float ringDistance = Mathf.Abs(distance - hardRadius);
                    float ringAlpha = 1f - Mathf.InverseLerp(0f, ringHalfWidth, ringDistance);
                    ringAlpha = Mathf.Clamp01(ringAlpha);
                    ringAlpha = Mathf.SmoothStep(0f, 1f, ringAlpha);

                    int pixelIndex = (y * textureResolution) + x;
                    maskPixels[pixelIndex] = new Color(1f, 1f, 1f, Mathf.Clamp01(maskAlpha));
                    ringPixels[pixelIndex] = new Color(1f, 1f, 1f, ringAlpha);
                }
            }

            generatedMaskTexture.SetPixels(maskPixels);
            generatedMaskTexture.Apply(false, false);

            generatedRingTexture.SetPixels(ringPixels);
            generatedRingTexture.Apply(false, false);

            generatedMaskSprite = Sprite.Create(
                generatedMaskTexture,
                new Rect(0f, 0f, textureResolution, textureResolution),
                new Vector2(0.5f, 0.5f),
                textureResolution);

            generatedMaskSprite.name = "Reveal Mask Sprite";
            generatedMaskSprite.hideFlags = HideFlags.HideAndDontSave;

            generatedRingSprite = Sprite.Create(
                generatedRingTexture,
                new Rect(0f, 0f, textureResolution, textureResolution),
                new Vector2(0.5f, 0.5f),
                textureResolution);

            generatedRingSprite.name = "Reveal Ring Sprite";
            generatedRingSprite.hideFlags = HideFlags.HideAndDontSave;

            cachedTextureResolution = textureResolution;
            cachedEdgeSoftness = edgeSoftness;
            cachedRingThickness = ringThickness;
        }

        private bool NeedsRebuild()
        {
            return generatedMaskTexture == null
                || generatedMaskSprite == null
                || generatedRingTexture == null
                || generatedRingSprite == null
                || cachedTextureResolution != textureResolution
                || !Mathf.Approximately(cachedEdgeSoftness, edgeSoftness)
                || !Mathf.Approximately(cachedRingThickness, ringThickness);
        }

        private void UpdateRingVisual()
        {
            if (ringVisual == null || ringRenderer == null)
            {
                return;
            }

            float pulse = 1f;
            float alphaPulse = 1f;

            if (showRing && pulseAmplitude > 0f && pulseSpeed > 0f)
            {
                float wave = Mathf.Sin((Application.isPlaying ? Time.time : Time.realtimeSinceStartup) * pulseSpeed);
                pulse += wave * pulseAmplitude;
                alphaPulse = 0.9f + ((wave + 1f) * 0.5f * 0.1f);
            }

            maskVisual.localScale = Vector3.one * (radius * 2f);
            ringVisual.localScale = Vector3.one * (radius * 2f * pulse);

            Color color = ringColor;
            color.a *= alphaPulse;
            ringRenderer.color = color;
        }

        private Material GetRingMaterial()
        {
            if (ringMaterial != null)
            {
                return ringMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            shader ??= Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            ringMaterial = new Material(shader)
            {
                name = "Reveal Ring Material",
                hideFlags = HideFlags.HideAndDontSave
            };

            return ringMaterial;
        }

        private void DestroyGeneratedAssets()
        {
            if (generatedMaskSprite != null)
            {
                DestroyGeneratedObject(generatedMaskSprite);
                generatedMaskSprite = null;
            }

            if (generatedMaskTexture != null)
            {
                DestroyGeneratedObject(generatedMaskTexture);
                generatedMaskTexture = null;
            }

            if (generatedRingSprite != null)
            {
                DestroyGeneratedObject(generatedRingSprite);
                generatedRingSprite = null;
            }

            if (generatedRingTexture != null)
            {
                DestroyGeneratedObject(generatedRingTexture);
                generatedRingTexture = null;
            }

            if (ringMaterial != null)
            {
                DestroyGeneratedObject(ringMaterial);
                ringMaterial = null;
            }
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}