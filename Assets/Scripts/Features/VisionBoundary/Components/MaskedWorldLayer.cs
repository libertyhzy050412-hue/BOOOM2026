using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace VisionBoundary
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class MaskedWorldLayer : MonoBehaviour
    {
        private const string ProxyObjectSuffix = " [Reveal Physics]";

        public enum WorldVisibility
        {
            SurfaceWorld,
            InnerWorld
        }

        private enum ClipMode
        {
            Hidden,
            Full,
            Partial
        }

        private sealed class SpriteProxy
        {
            public SpriteRenderer sourceRenderer;
            public GameObject proxyObject;
            public PolygonCollider2D proxyCollider;
            public Texture2D generatedTexture;
            public Sprite generatedSprite;
            public bool sourceColliderStatesCaptured;
            public Collider2D[] sourceColliders = Array.Empty<Collider2D>();
            public bool[] sourceColliderEnabledStates = Array.Empty<bool>();
            public ClipMode lastClipMode;
            public Vector2 lastRevealCenterLocal;
            public float lastRevealRadiusLocal = -1f;
            public int lastResolution;
            public Sprite lastSourceSprite;
            public Color lastSourceColor;
        }

        [SerializeField] private WorldVisibility worldVisibility = WorldVisibility.SurfaceWorld;
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private bool autoRefreshTargets = true;
        [SerializeField] private bool forceUnlitMaterial = true;
        [SerializeField] private bool syncPhysicsWithReveal = true;
        [SerializeField] private MouseRevealMaskRig revealRig;
        [SerializeField] [Range(32, 256)] private int clipTextureResolution = 128;
        [SerializeField] private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();
        [SerializeField] private TilemapRenderer[] tilemapRenderers = Array.Empty<TilemapRenderer>();

        private Material unlitMaterial;
        private readonly Dictionary<SpriteRenderer, SpriteProxy> proxies = new Dictionary<SpriteRenderer, SpriteProxy>();
        private readonly List<Vector2> physicsShapeBuffer = new List<Vector2>();

        private void Reset()
        {
            RefreshTargets();
            TryAutoAssignRevealRig();
            ApplyVisualSettings(false);
        }

        private void OnEnable()
        {
            if (autoRefreshTargets)
            {
                RefreshTargets();
            }

            UpdateLayerState();
        }

        private void Update()
        {
            UpdateLayerState();
        }

        private void OnValidate()
        {
            if (autoRefreshTargets)
            {
                RefreshTargets();
            }

            TryAutoAssignRevealRig();
            UpdateLayerState();
        }

        [ContextMenu("Auto Assign Reveal Rig")]
        private void AutoAssignRevealRig()
        {
            TryAutoAssignRevealRig();
        }

        private void OnDestroy()
        {
            RestoreSourceColliders();
            DestroyAllProxies();

            if (unlitMaterial != null)
            {
                DestroyGeneratedObject(unlitMaterial);
                unlitMaterial = null;
            }
        }

        [ContextMenu("Refresh Targets")]
        public void RefreshTargets()
        {
            SpriteRenderer[] foundRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive);
            List<SpriteRenderer> filteredRenderers = new List<SpriteRenderer>(foundRenderers.Length);

            for (int index = 0; index < foundRenderers.Length; index++)
            {
                SpriteRenderer spriteRenderer = foundRenderers[index];
                if (spriteRenderer == null || IsGeneratedProxy(spriteRenderer.transform))
                {
                    continue;
                }

                filteredRenderers.Add(spriteRenderer);
            }

            spriteRenderers = filteredRenderers.ToArray();
            tilemapRenderers = GetComponentsInChildren<TilemapRenderer>(includeInactive);
        }

        private void UpdateLayerState()
        {
            clipTextureResolution = Mathf.Clamp(clipTextureResolution, 32, 256);

            bool revealAvailable = revealRig != null;
            ApplyVisualSettings(revealAvailable);

            if (spriteRenderers == null || spriteRenderers.Length == 0)
            {
                return;
            }

            revealAvailable = syncPhysicsWithReveal && revealAvailable;
            HashSet<SpriteRenderer> activeSources = new HashSet<SpriteRenderer>();

            for (int index = 0; index < spriteRenderers.Length; index++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[index];
                if (spriteRenderer == null || spriteRenderer.sprite == null || IsGeneratedProxy(spriteRenderer.transform))
                {
                    continue;
                }

                activeSources.Add(spriteRenderer);
                UpdateSourceState(spriteRenderer, revealAvailable);
            }

            DestroyStaleProxies(activeSources);
        }

        private void TryAutoAssignRevealRig()
        {
            if (revealRig != null)
            {
                return;
            }

            revealRig = FindFirstObjectByType<MouseRevealMaskRig>();
        }

        private void ApplyVisualSettings(bool revealAvailable)
        {
            Material targetMaterial = forceUnlitMaterial ? GetUnlitMaterial() : null;
            SpriteMaskInteraction interaction = worldVisibility == WorldVisibility.InnerWorld
                ? SpriteMaskInteraction.VisibleInsideMask
                : SpriteMaskInteraction.VisibleOutsideMask;

            for (int index = 0; index < spriteRenderers.Length; index++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[index];
                if (spriteRenderer == null || IsGeneratedProxy(spriteRenderer.transform))
                {
                    continue;
                }

                spriteRenderer.enabled = revealAvailable || worldVisibility == WorldVisibility.SurfaceWorld;
                spriteRenderer.maskInteraction = revealAvailable ? interaction : SpriteMaskInteraction.None;
                if (targetMaterial != null)
                {
                    spriteRenderer.sharedMaterial = targetMaterial;
                }
            }

            for (int index = 0; index < tilemapRenderers.Length; index++)
            {
                TilemapRenderer tilemapRenderer = tilemapRenderers[index];
                if (tilemapRenderer == null)
                {
                    continue;
                }

                tilemapRenderer.enabled = revealAvailable || worldVisibility == WorldVisibility.SurfaceWorld;
                tilemapRenderer.maskInteraction = revealAvailable ? interaction : SpriteMaskInteraction.None;
                if (targetMaterial != null)
                {
                    tilemapRenderer.sharedMaterial = targetMaterial;
                }
            }
        }

        private void UpdateSourceState(SpriteRenderer spriteRenderer, bool revealAvailable)
        {
            SpriteProxy proxy = GetOrCreateProxy(spriteRenderer);

            if (!revealAvailable)
            {
                ApplyDefaultState(proxy);
                return;
            }

            Vector2 revealCenterLocal = spriteRenderer.transform.InverseTransformPoint(revealRig.RevealCenterWorld);
            float revealRadiusLocal = GetLocalRevealRadius(spriteRenderer.transform, revealRig.RevealRadiusWorld);
            ClipMode clipMode = DetermineClipMode(spriteRenderer.sprite.bounds, revealCenterLocal, revealRadiusLocal);

            SetSourceCollidersEnabled(proxy, false);

            if (clipMode == ClipMode.Hidden)
            {
                proxy.proxyObject.SetActive(false);
                proxy.lastClipMode = ClipMode.Hidden;
                return;
            }

            proxy.proxyObject.SetActive(spriteRenderer.gameObject.activeInHierarchy);

            if (clipMode == ClipMode.Full)
            {
                ApplyFullSprite(proxy, spriteRenderer.sprite);
            }
            else
            {
                if (NeedsPartialRebuild(proxy, spriteRenderer, revealCenterLocal, revealRadiusLocal))
                {
                    RebuildPartialProxy(proxy, spriteRenderer, revealCenterLocal, revealRadiusLocal);
                }
            }

            proxy.proxyCollider.enabled = proxy.proxyCollider.pathCount > 0;
            proxy.lastClipMode = clipMode;
            proxy.lastRevealCenterLocal = revealCenterLocal;
            proxy.lastRevealRadiusLocal = revealRadiusLocal;
            proxy.lastResolution = clipTextureResolution;
            proxy.lastSourceSprite = spriteRenderer.sprite;
            proxy.lastSourceColor = spriteRenderer.color;
        }

        private void ApplyDefaultState(SpriteProxy proxy)
        {
            bool enableDefaultColliders = worldVisibility == WorldVisibility.SurfaceWorld;
            SetSourceCollidersEnabled(proxy, enableDefaultColliders);

            if (proxy.proxyObject != null)
            {
                proxy.proxyObject.SetActive(false);
            }

            if (proxy.proxyCollider != null)
            {
                proxy.proxyCollider.enabled = false;
            }

            proxy.lastClipMode = enableDefaultColliders ? ClipMode.Full : ClipMode.Hidden;
        }

        private ClipMode DetermineClipMode(Bounds spriteBounds, Vector2 revealCenterLocal, float revealRadiusLocal)
        {
            bool intersects = CircleIntersectsBounds(revealCenterLocal, revealRadiusLocal, spriteBounds);
            bool fullyInside = BoundsFullyInsideCircle(spriteBounds, revealCenterLocal, revealRadiusLocal);

            switch (worldVisibility)
            {
                case WorldVisibility.InnerWorld:
                    if (!intersects)
                    {
                        return ClipMode.Hidden;
                    }

                    return fullyInside ? ClipMode.Full : ClipMode.Partial;

                case WorldVisibility.SurfaceWorld:
                    if (!intersects)
                    {
                        return ClipMode.Full;
                    }

                    return fullyInside ? ClipMode.Hidden : ClipMode.Partial;

                default:
                    return ClipMode.Hidden;
            }
        }

        private SpriteProxy GetOrCreateProxy(SpriteRenderer sourceRenderer)
        {
            if (proxies.TryGetValue(sourceRenderer, out SpriteProxy existingProxy) && existingProxy.proxyObject != null)
            {
                return existingProxy;
            }

            SpriteProxy proxy = new SpriteProxy
            {
                sourceRenderer = sourceRenderer
            };

            Transform existingChild = sourceRenderer.transform.Find(sourceRenderer.name + ProxyObjectSuffix);
            if (existingChild != null)
            {
                proxy.proxyObject = existingChild.gameObject;
            }
            else
            {
                proxy.proxyObject = new GameObject(sourceRenderer.name + ProxyObjectSuffix);
                proxy.proxyObject.transform.SetParent(sourceRenderer.transform, false);
            }

            proxy.proxyObject.hideFlags = HideFlags.HideInHierarchy;
            proxy.proxyObject.transform.localPosition = Vector3.zero;
            proxy.proxyObject.transform.localRotation = Quaternion.identity;
            proxy.proxyObject.transform.localScale = Vector3.one;

            proxy.proxyCollider = proxy.proxyObject.GetComponent<PolygonCollider2D>();
            if (proxy.proxyCollider == null)
            {
                proxy.proxyCollider = proxy.proxyObject.AddComponent<PolygonCollider2D>();
            }

            proxy.proxyCollider.isTrigger = false;
            proxy.proxyCollider.enabled = false;
            proxy.sourceColliders = sourceRenderer.GetComponents<Collider2D>();
            proxy.sourceColliderEnabledStates = new bool[proxy.sourceColliders.Length];

            proxies[sourceRenderer] = proxy;
            return proxy;
        }

        private void ApplyFullSprite(SpriteProxy proxy, Sprite sprite)
        {
            DestroyGeneratedAssets(proxy);
            ApplySpritePhysicsShape(proxy.proxyCollider, sprite);
        }

        private void RebuildPartialProxy(SpriteProxy proxy, SpriteRenderer sourceRenderer, Vector2 revealCenterLocal, float revealRadiusLocal)
        {
            Sprite sourceSprite = sourceRenderer.sprite;
            Bounds bounds = sourceSprite.bounds;
            Vector2 size = bounds.size;
            float maxDimension = Mathf.Max(size.x, size.y, 0.0001f);
            float pixelsPerUnit = clipTextureResolution / maxDimension;
            int width = Mathf.Max(8, Mathf.RoundToInt(size.x * pixelsPerUnit));
            int height = Mathf.Max(8, Mathf.RoundToInt(size.y * pixelsPerUnit));

            if (proxy.generatedTexture == null || proxy.generatedTexture.width != width || proxy.generatedTexture.height != height)
            {
                DestroyGeneratedAssets(proxy);
                proxy.generatedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = sourceRenderer.name + " Reveal Proxy Texture",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            List<Vector2[]> sourceShapes = GetSpriteShapes(sourceSprite);
            bool textureReadable = sourceSprite.texture != null && sourceSprite.texture.isReadable;
            Color tintColor = sourceRenderer.color;
            Rect textureRect = sourceSprite.textureRect;
            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                float sampleY = Mathf.Lerp(bounds.min.y, bounds.max.y, (y + 0.5f) / height);
                float normalizedY = Mathf.InverseLerp(bounds.min.y, bounds.max.y, sampleY);

                for (int x = 0; x < width; x++)
                {
                    float sampleX = Mathf.Lerp(bounds.min.x, bounds.max.x, (x + 0.5f) / width);
                    float normalizedX = Mathf.InverseLerp(bounds.min.x, bounds.max.x, sampleX);
                    Vector2 samplePoint = new Vector2(sampleX, sampleY);

                    bool insideSource = IsPointInsideAnyShape(samplePoint, sourceShapes);
                    bool insideReveal = (samplePoint - revealCenterLocal).sqrMagnitude <= revealRadiusLocal * revealRadiusLocal;
                    bool visible = insideSource && ShouldBeVisibleAtPoint(insideReveal);

                    if (!visible)
                    {
                        pixels[(y * width) + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    Color pixelColor;
                    if (textureReadable)
                    {
                        float u = Mathf.Lerp(textureRect.xMin, textureRect.xMax, normalizedX) / sourceSprite.texture.width;
                        float v = Mathf.Lerp(textureRect.yMin, textureRect.yMax, normalizedY) / sourceSprite.texture.height;
                        pixelColor = sourceSprite.texture.GetPixelBilinear(u, v) * tintColor;
                    }
                    else
                    {
                        pixelColor = tintColor;
                    }

                    pixels[(y * width) + x] = pixelColor;
                }
            }

            proxy.generatedTexture.SetPixels32(pixels);
            proxy.generatedTexture.Apply(false, false);

            if (proxy.generatedSprite != null)
            {
                DestroyGeneratedObject(proxy.generatedSprite);
            }

            Vector2 pivot = new Vector2(
                Mathf.InverseLerp(bounds.min.x, bounds.max.x, 0f),
                Mathf.InverseLerp(bounds.min.y, bounds.max.y, 0f));

            proxy.generatedSprite = Sprite.Create(
                proxy.generatedTexture,
                new Rect(0f, 0f, width, height),
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.Tight,
                Vector4.zero,
                true);

            proxy.generatedSprite.name = sourceRenderer.name + " Reveal Proxy Sprite";
            proxy.generatedSprite.hideFlags = HideFlags.HideAndDontSave;
            ApplySpritePhysicsShape(proxy.proxyCollider, proxy.generatedSprite);
        }

        private bool NeedsPartialRebuild(SpriteProxy proxy, SpriteRenderer sourceRenderer, Vector2 revealCenterLocal, float revealRadiusLocal)
        {
            return proxy.lastClipMode != ClipMode.Partial
                || proxy.generatedTexture == null
                || proxy.generatedSprite == null
                || proxy.lastResolution != clipTextureResolution
                || proxy.lastSourceSprite != sourceRenderer.sprite
                || proxy.lastSourceColor != sourceRenderer.color
                || (proxy.lastRevealCenterLocal - revealCenterLocal).sqrMagnitude > 0.0001f
                || !Mathf.Approximately(proxy.lastRevealRadiusLocal, revealRadiusLocal);
        }

        private bool ShouldBeVisibleAtPoint(bool insideReveal)
        {
            switch (worldVisibility)
            {
                case WorldVisibility.InnerWorld:
                    return insideReveal;
                case WorldVisibility.SurfaceWorld:
                    return !insideReveal;
                default:
                    return false;
            }
        }

        private void ApplySpritePhysicsShape(PolygonCollider2D collider, Sprite sprite)
        {
            int shapeCount = sprite.GetPhysicsShapeCount();
            if (shapeCount <= 0)
            {
                Bounds bounds = sprite.bounds;
                collider.pathCount = 1;
                collider.SetPath(0, new[]
                {
                    new Vector2(bounds.min.x, bounds.min.y),
                    new Vector2(bounds.min.x, bounds.max.y),
                    new Vector2(bounds.max.x, bounds.max.y),
                    new Vector2(bounds.max.x, bounds.min.y)
                });
                return;
            }

            collider.pathCount = shapeCount;
            for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
            {
                physicsShapeBuffer.Clear();
                sprite.GetPhysicsShape(shapeIndex, physicsShapeBuffer);
                collider.SetPath(shapeIndex, physicsShapeBuffer);
            }
        }

        private static List<Vector2[]> GetSpriteShapes(Sprite sprite)
        {
            int shapeCount = sprite.GetPhysicsShapeCount();
            List<Vector2[]> shapes = new List<Vector2[]>(Mathf.Max(shapeCount, 1));

            if (shapeCount <= 0)
            {
                Bounds bounds = sprite.bounds;
                shapes.Add(new[]
                {
                    new Vector2(bounds.min.x, bounds.min.y),
                    new Vector2(bounds.min.x, bounds.max.y),
                    new Vector2(bounds.max.x, bounds.max.y),
                    new Vector2(bounds.max.x, bounds.min.y)
                });
                return shapes;
            }

            List<Vector2> points = new List<Vector2>();
            for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
            {
                points.Clear();
                sprite.GetPhysicsShape(shapeIndex, points);
                shapes.Add(points.ToArray());
            }

            return shapes;
        }

        private static bool IsPointInsideAnyShape(Vector2 point, List<Vector2[]> shapes)
        {
            for (int index = 0; index < shapes.Count; index++)
            {
                if (IsPointInsidePolygon(point, shapes[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointInsidePolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            int previousIndex = polygon.Length - 1;

            for (int index = 0; index < polygon.Length; index++)
            {
                Vector2 current = polygon[index];
                Vector2 previous = polygon[previousIndex];
                bool intersects = ((current.y > point.y) != (previous.y > point.y))
                    && (point.x < ((previous.x - current.x) * (point.y - current.y) / ((previous.y - current.y) + Mathf.Epsilon)) + current.x);

                if (intersects)
                {
                    inside = !inside;
                }

                previousIndex = index;
            }

            return inside;
        }

        private static float GetLocalRevealRadius(Transform targetTransform, float worldRadius)
        {
            Vector3 lossyScale = targetTransform.lossyScale;
            float scale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), 0.0001f);
            return worldRadius / scale;
        }

        private static bool CircleIntersectsBounds(Vector2 center, float radius, Bounds bounds)
        {
            float clampedX = Mathf.Clamp(center.x, bounds.min.x, bounds.max.x);
            float clampedY = Mathf.Clamp(center.y, bounds.min.y, bounds.max.y);
            float deltaX = center.x - clampedX;
            float deltaY = center.y - clampedY;
            return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
        }

        private static bool BoundsFullyInsideCircle(Bounds bounds, Vector2 center, float radius)
        {
            Vector2[] corners =
            {
                new Vector2(bounds.min.x, bounds.min.y),
                new Vector2(bounds.min.x, bounds.max.y),
                new Vector2(bounds.max.x, bounds.min.y),
                new Vector2(bounds.max.x, bounds.max.y)
            };

            float radiusSquared = radius * radius;
            for (int index = 0; index < corners.Length; index++)
            {
                if ((corners[index] - center).sqrMagnitude > radiusSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private static void SetSourceCollidersEnabled(SpriteProxy proxy, bool enabled)
        {
            if (!proxy.sourceColliderStatesCaptured)
            {
                for (int index = 0; index < proxy.sourceColliders.Length; index++)
                {
                    Collider2D sourceCollider = proxy.sourceColliders[index];
                    proxy.sourceColliderEnabledStates[index] = sourceCollider != null && sourceCollider.enabled;
                }

                proxy.sourceColliderStatesCaptured = true;
            }

            for (int index = 0; index < proxy.sourceColliders.Length; index++)
            {
                Collider2D sourceCollider = proxy.sourceColliders[index];
                if (sourceCollider != null)
                {
                    sourceCollider.enabled = enabled && proxy.sourceColliderEnabledStates[index];
                }
            }
        }

        private void RestoreSourceColliders()
        {
            foreach (KeyValuePair<SpriteRenderer, SpriteProxy> pair in proxies)
            {
                SpriteProxy proxy = pair.Value;
                SetSourceCollidersEnabled(proxy, true);
            }
        }

        private void DestroyStaleProxies(HashSet<SpriteRenderer> activeSources)
        {
            List<SpriteRenderer> staleSources = null;

            foreach (KeyValuePair<SpriteRenderer, SpriteProxy> pair in proxies)
            {
                if (pair.Key != null && activeSources.Contains(pair.Key))
                {
                    continue;
                }

                staleSources ??= new List<SpriteRenderer>();
                staleSources.Add(pair.Key);
            }

            if (staleSources == null)
            {
                return;
            }

            for (int index = 0; index < staleSources.Count; index++)
            {
                SpriteRenderer sourceRenderer = staleSources[index];
                if (sourceRenderer != null && proxies.TryGetValue(sourceRenderer, out SpriteProxy proxy))
                {
                    DestroyProxy(proxy);
                }

                proxies.Remove(sourceRenderer);
            }
        }

        private void DestroyAllProxies()
        {
            foreach (KeyValuePair<SpriteRenderer, SpriteProxy> pair in proxies)
            {
                DestroyProxy(pair.Value);
            }

            proxies.Clear();
        }

        private void DestroyProxy(SpriteProxy proxy)
        {
            SetSourceCollidersEnabled(proxy, true);
            DestroyGeneratedAssets(proxy);

            if (proxy.proxyObject != null)
            {
                DestroyGeneratedObject(proxy.proxyObject);
            }
        }

        private static void DestroyGeneratedAssets(SpriteProxy proxy)
        {
            if (proxy.generatedSprite != null)
            {
                DestroyGeneratedObject(proxy.generatedSprite);
                proxy.generatedSprite = null;
            }

            if (proxy.generatedTexture != null)
            {
                DestroyGeneratedObject(proxy.generatedTexture);
                proxy.generatedTexture = null;
            }
        }

        private static bool IsGeneratedProxy(Transform target)
        {
            while (target != null)
            {
                if (target.name.EndsWith(ProxyObjectSuffix, StringComparison.Ordinal))
                {
                    return true;
                }

                target = target.parent;
            }

            return false;
        }

        private Material GetUnlitMaterial()
        {
            if (unlitMaterial != null)
            {
                return unlitMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            shader ??= Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            unlitMaterial = new Material(shader)
            {
                name = name + " Reveal Unlit Material",
                hideFlags = HideFlags.HideAndDontSave
            };

            return unlitMaterial;
        }

        private static void DestroyGeneratedObject(UnityEngine.Object target)
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