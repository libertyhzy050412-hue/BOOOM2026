using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldRevealMaskController : MonoBehaviour
{
    private const string DefaultMapRootName = "MapRoot";
    private const string DefaultRevealMaskName = "WorldRevealMask";
    private const float DefaultMaskPixelsPerUnit = 16f;
    private const int DefaultMaxMaskTextureSize = 2048;

    [SerializeField] private Transform mapRoot;
    [SerializeField] private string mapRootName = DefaultMapRootName;
    [SerializeField] private string revealMaskName = DefaultRevealMaskName;
    [SerializeField, Range(1f, 64f)] private float maskPixelsPerUnit = DefaultMaskPixelsPerUnit;
    [SerializeField, Min(256)] private int maxMaskTextureSize = DefaultMaxMaskTextureSize;
    [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.01f;
    [SerializeField] private bool ignoreRuntimeBoundsChangesAfterBuild = true;

    private static WorldRevealMaskController instance;

    private SpriteMask revealMask;
    private Sprite generatedMaskSprite;
    private Texture2D generatedMaskTexture;
    private Color32[] maskPixels;
    private Color32[] dirtyPixels;
    private Bounds revealBounds;
    private Vector2 revealMin;
    private Vector2 revealSize;
    private int maskTextureWidth;
    private int maskTextureHeight;
    private bool maskDirty;
    private bool hasDirtyRect;
    private bool hasLoggedMissingMapBounds;
    private bool configurationDirty;
    private int dirtyMinX;
    private int dirtyMinY;
    private int dirtyMaxX;
    private int dirtyMaxY;

    public static WorldRevealMaskController Instance => instance;

    public static WorldRevealMaskController GetOrCreate(
        Transform preferredMapRoot = null,
        string preferredMapRootName = DefaultMapRootName,
        string preferredRevealMaskName = DefaultRevealMaskName,
        float preferredMaskPixelsPerUnit = DefaultMaskPixelsPerUnit,
        int preferredMaxMaskTextureSize = DefaultMaxMaskTextureSize)
    {
        if (instance == null)
        {
            instance = FindFirstObjectByType<WorldRevealMaskController>();
        }

        if (instance == null)
        {
            GameObject host = new GameObject("WorldRevealMaskController");
            instance = host.AddComponent<WorldRevealMaskController>();
        }

        instance.ApplyConfiguration(
            preferredMapRoot,
            preferredMapRootName,
            preferredRevealMaskName,
            preferredMaskPixelsPerUnit,
            preferredMaxMaskTextureSize);

        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        DestroyGeneratedMask();
    }

    private void OnValidate()
    {
        maskPixelsPerUnit = Mathf.Clamp(maskPixelsPerUnit, 1f, 64f);
        maxMaskTextureSize = Mathf.Clamp(maxMaskTextureSize, 256, 4096);
        alphaCutoff = Mathf.Clamp01(alphaCutoff);

        if (Application.isPlaying)
        {
            TryEnsureRevealMask();
        }
    }

    public bool RevealStamp(Vector2 center, float radius, float hardness)
    {
        return PaintStamp(center, radius, hardness, true, 1f);
    }

    public bool ConcealStamp(Vector2 center, float radius, float hardness, float strength = 1f)
    {
        return PaintStamp(center, radius, hardness, false, strength);
    }

    public bool RevealStroke(Vector2 from, Vector2 to, float fromRadius, float toRadius, float hardness, float spacingRatio)
    {
        return PaintStroke(from, to, fromRadius, toRadius, hardness, spacingRatio, true, 1f);
    }

    public bool ConcealStroke(Vector2 from, Vector2 to, float fromRadius, float toRadius, float hardness, float spacingRatio, float strength = 1f)
    {
        return PaintStroke(from, to, fromRadius, toRadius, hardness, spacingRatio, false, strength);
    }

    public void Flush()
    {
        FlushRevealMaskTexture();
    }

    public void ClearMask()
    {
        if (maskPixels == null || generatedMaskTexture == null)
        {
            return;
        }

        System.Array.Clear(maskPixels, 0, maskPixels.Length);
        generatedMaskTexture.SetPixels32(maskPixels);
        generatedMaskTexture.Apply(false, false);
        maskDirty = false;
        hasDirtyRect = false;
    }

    public bool TryGetMapBounds(out Bounds bounds)
    {
        ResolveMapRoot();
        bounds = default;

        if (mapRoot == null)
        {
            return false;
        }

        SpriteRenderer[] renderers = mapRoot.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer rendererReference = renderers[index];
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

    public bool TryGetRandomRevealedPosition(out Vector3 worldPosition, float minAlpha01 = 0.05f, int randomAttempts = 32)
    {
        worldPosition = default;
        if (!TryEnsureRevealMask() || maskPixels == null || maskPixels.Length == 0)
        {
            return false;
        }

        byte minAlpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.Max(minAlpha01, alphaCutoff)) * 255f);
        int totalPixels = maskPixels.Length;
        int clampedAttempts = Mathf.Max(1, randomAttempts);

        for (int attempt = 0; attempt < clampedAttempts; attempt++)
        {
            int pixelIndex = Random.Range(0, totalPixels);
            if (maskPixels[pixelIndex].a < minAlpha)
            {
                continue;
            }

            worldPosition = PixelIndexToWorldPosition(pixelIndex);
            return true;
        }

        int startIndex = Random.Range(0, totalPixels);
        for (int offset = 0; offset < totalPixels; offset++)
        {
            int pixelIndex = (startIndex + offset) % totalPixels;
            if (maskPixels[pixelIndex].a < minAlpha)
            {
                continue;
            }

            worldPosition = PixelIndexToWorldPosition(pixelIndex);
            return true;
        }

        return false;
    }

    public bool IsRevealedAtWorldPosition(Vector2 worldPosition, float minAlpha01 = 0.05f)
    {
        if (!TryEnsureRevealMask() || maskPixels == null)
        {
            return false;
        }

        if (!TryGetPixelIndexFromWorldPosition(worldPosition, out int pixelIndex))
        {
            return false;
        }

        byte minAlpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.Max(minAlpha01, alphaCutoff)) * 255f);
        return maskPixels[pixelIndex].a >= minAlpha;
    }

    private void ApplyConfiguration(
        Transform preferredMapRoot,
        string preferredMapRootName,
        string preferredRevealMaskName,
        float preferredMaskPixelsPerUnit,
        int preferredMaxMaskTextureSize)
    {
        bool configurationChanged = false;

        if (preferredMapRoot != null)
        {
            if (mapRoot != preferredMapRoot)
            {
                mapRoot = preferredMapRoot;
                configurationChanged = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredMapRootName) && mapRootName != preferredMapRootName)
        {
            mapRootName = preferredMapRootName;
            configurationChanged = true;
        }

        if (!string.IsNullOrWhiteSpace(preferredRevealMaskName) && revealMaskName != preferredRevealMaskName)
        {
            revealMaskName = preferredRevealMaskName;
            configurationChanged = true;
        }

        float clampedMaskPixelsPerUnit = Mathf.Clamp(preferredMaskPixelsPerUnit, 1f, 64f);
        if (!Mathf.Approximately(maskPixelsPerUnit, clampedMaskPixelsPerUnit))
        {
            maskPixelsPerUnit = clampedMaskPixelsPerUnit;
            configurationChanged = true;
        }

        int clampedMaxMaskTextureSize = Mathf.Clamp(preferredMaxMaskTextureSize, 256, 4096);
        if (maxMaskTextureSize != clampedMaxMaskTextureSize)
        {
            maxMaskTextureSize = clampedMaxMaskTextureSize;
            configurationChanged = true;
        }

        ResolveMapRoot();
        if (mapRoot != null && transform.parent != mapRoot)
        {
            transform.SetParent(mapRoot, false);
            configurationChanged = true;
        }

        if (configurationChanged)
        {
            configurationDirty = true;
        }
    }

    private bool PaintStroke(Vector2 from, Vector2 to, float fromRadius, float toRadius, float hardness, float spacingRatio, bool reveal, float strength)
    {
        float distance = Vector2.Distance(from, to);
        if (distance <= 0.0001f)
        {
            return PaintStamp(to, toRadius, hardness, reveal, strength);
        }

        float clampedSpacingRatio = Mathf.Clamp(spacingRatio, 0.1f, 0.9f);
        float averageRadius = Mathf.Max(0.01f, (fromRadius + toRadius) * 0.5f);
        float spacing = Mathf.Max(averageRadius * clampedSpacingRatio, 0.03f);
        int stampCount = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));
        bool wroteAnyPixel = false;

        for (int index = 1; index <= stampCount; index++)
        {
            float t = index / (float)stampCount;
            Vector2 stampPosition = Vector2.Lerp(from, to, t);
            float radius = Mathf.Lerp(fromRadius, toRadius, t);
            wroteAnyPixel |= PaintStamp(stampPosition, radius, hardness, reveal, strength);
        }

        return wroteAnyPixel;
    }

    private bool PaintStamp(Vector2 center, float radius, float hardness, bool reveal, float strength)
    {
        if (radius <= 0f || !TryEnsureRevealMask())
        {
            return false;
        }

        float normalizedHardness = Mathf.Clamp(hardness, 0.05f, 0.95f);
        float normalizedStrength = Mathf.Max(0f, strength);
        float pixelWidth = revealSize.x / maskTextureWidth;
        float pixelHeight = revealSize.y / maskTextureHeight;
        int minX = Mathf.Clamp(Mathf.FloorToInt((center.x - radius - revealMin.x) / revealSize.x * maskTextureWidth), 0, maskTextureWidth - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt((center.x + radius - revealMin.x) / revealSize.x * maskTextureWidth), 0, maskTextureWidth - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt((center.y - radius - revealMin.y) / revealSize.y * maskTextureHeight), 0, maskTextureHeight - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt((center.y + radius - revealMin.y) / revealSize.y * maskTextureHeight), 0, maskTextureHeight - 1);
        bool wroteAnyPixel = false;

        for (int y = minY; y <= maxY; y++)
        {
            float worldY = revealMin.y + (y + 0.5f) * pixelHeight;
            for (int x = minX; x <= maxX; x++)
            {
                float worldX = revealMin.x + (x + 0.5f) * pixelWidth;
                float distance01 = Vector2.Distance(new Vector2(worldX, worldY), center) / radius;
                if (distance01 > 1f)
                {
                    continue;
                }

                float alpha01 = 1f - Mathf.SmoothStep(normalizedHardness, 1f, distance01);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha01) * 255f * normalizedStrength);
                int pixelIndex = y * maskTextureWidth + x;
                byte currentAlpha = maskPixels[pixelIndex].a;
                byte nextAlpha = reveal
                    ? (byte)Mathf.Max(currentAlpha, alpha)
                    : (byte)Mathf.Max(0, currentAlpha - alpha);

                if (nextAlpha == currentAlpha)
                {
                    continue;
                }

                maskPixels[pixelIndex] = new Color32(255, 255, 255, nextAlpha);
                wroteAnyPixel = true;
            }
        }

        if (!wroteAnyPixel)
        {
            return false;
        }

        MarkDirtyRect(minX, minY, maxX, maxY);
        return true;
    }

    private bool TryEnsureRevealMask()
    {
        if (!TryGetMapBounds(out Bounds currentBounds))
        {
            LogMissingMapBoundsOnce();
            return false;
        }

        hasLoggedMissingMapBounds = false;
        if (revealMask != null && generatedMaskTexture != null)
        {
            if (!configurationDirty)
            {
                if (Application.isPlaying && ignoreRuntimeBoundsChangesAfterBuild)
                {
                    return true;
                }

                if (!BoundsChanged(currentBounds))
                {
                    return true;
                }
            }
        }

        ResolveMapRoot();
        if (mapRoot == null)
        {
            LogMissingMapBoundsOnce();
            return false;
        }

        RebuildRevealMask(currentBounds);
        return revealMask != null && generatedMaskTexture != null;
    }

    private void RebuildRevealMask(Bounds bounds)
    {
        DestroyGeneratedMask();

        Transform existingMaskTransform = mapRoot != null ? mapRoot.Find(revealMaskName) : null;
        if (existingMaskTransform == null)
        {
            GameObject maskObject = new GameObject(revealMaskName);
            existingMaskTransform = maskObject.transform;
            existingMaskTransform.SetParent(mapRoot, false);
        }

        revealMask = existingMaskTransform.GetComponent<SpriteMask>();
        if (revealMask == null)
        {
            revealMask = existingMaskTransform.gameObject.AddComponent<SpriteMask>();
        }

        revealBounds = bounds;
        revealMin = bounds.min;
        revealSize = new Vector2(Mathf.Max(bounds.size.x, 0.01f), Mathf.Max(bounds.size.y, 0.01f));
        ComputeMaskTextureSize(revealSize, out maskTextureWidth, out maskTextureHeight);

        generatedMaskTexture = new Texture2D(maskTextureWidth, maskTextureHeight, TextureFormat.RGBA32, false)
        {
            name = "WorldRevealMaskTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        maskPixels = new Color32[maskTextureWidth * maskTextureHeight];
        generatedMaskTexture.SetPixels32(maskPixels);
        generatedMaskTexture.Apply(false, false);

        generatedMaskSprite = Sprite.Create(
            generatedMaskTexture,
            new Rect(0f, 0f, maskTextureWidth, maskTextureHeight),
            new Vector2(0.5f, 0.5f),
            1f);
        generatedMaskSprite.name = "WorldRevealMaskSprite";
        generatedMaskSprite.hideFlags = HideFlags.HideAndDontSave;

        revealMask.sprite = generatedMaskSprite;
        revealMask.alphaCutoff = alphaCutoff;
        revealMask.isCustomRangeActive = false;
        revealMask.transform.SetParent(mapRoot, false);
        revealMask.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0f);
        revealMask.transform.rotation = Quaternion.identity;
        revealMask.transform.localScale = new Vector3(revealSize.x / maskTextureWidth, revealSize.y / maskTextureHeight, 1f);
        
        //地图涂色与未涂色交界特效
        Shader.SetGlobalTexture("_GlobalRevealMask", generatedMaskTexture);
        Shader.SetGlobalVector("_GlobalRevealMask_TexelSize", new Vector4(1f / maskTextureWidth, 1f / maskTextureHeight, maskTextureWidth, maskTextureHeight));
        Shader.SetGlobalVector("_GlobalRevealMaskMin", new Vector4(revealMin.x, revealMin.y, 0, 0));
        Shader.SetGlobalVector("_GlobalRevealMaskSize", new Vector4(revealSize.x, revealSize.y, 0, 0));

        maskDirty = false;
        hasDirtyRect = false;
        dirtyPixels = null;
        configurationDirty = false;
    }

    private void ComputeMaskTextureSize(Vector2 size, out int width, out int height)
    {
        width = Mathf.Max(64, Mathf.CeilToInt(size.x * maskPixelsPerUnit));
        height = Mathf.Max(64, Mathf.CeilToInt(size.y * maskPixelsPerUnit));

        float scale = Mathf.Min(
            maxMaskTextureSize / (float)width,
            maxMaskTextureSize / (float)height,
            1f);

        width = Mathf.Max(64, Mathf.RoundToInt(width * scale));
        height = Mathf.Max(64, Mathf.RoundToInt(height * scale));
    }

    private void FlushRevealMaskTexture()
    {
        if (!maskDirty || generatedMaskTexture == null || !hasDirtyRect)
        {
            return;
        }

        int width = dirtyMaxX - dirtyMinX + 1;
        int height = dirtyMaxY - dirtyMinY + 1;
        int count = width * height;

        if (dirtyPixels == null || dirtyPixels.Length != count)
        {
            dirtyPixels = new Color32[count];
        }

        int bufferIndex = 0;
        for (int y = dirtyMinY; y <= dirtyMaxY; y++)
        {
            int rowOffset = y * maskTextureWidth;
            for (int x = dirtyMinX; x <= dirtyMaxX; x++)
            {
                dirtyPixels[bufferIndex++] = maskPixels[rowOffset + x];
            }
        }

        generatedMaskTexture.SetPixels32(dirtyMinX, dirtyMinY, width, height, dirtyPixels);
        generatedMaskTexture.Apply(false, false);
        maskDirty = false;
        hasDirtyRect = false;
    }

    private void MarkDirtyRect(int minX, int minY, int maxX, int maxY)
    {
        if (!hasDirtyRect)
        {
            dirtyMinX = minX;
            dirtyMinY = minY;
            dirtyMaxX = maxX;
            dirtyMaxY = maxY;
            hasDirtyRect = true;
            maskDirty = true;
            return;
        }

        dirtyMinX = Mathf.Min(dirtyMinX, minX);
        dirtyMinY = Mathf.Min(dirtyMinY, minY);
        dirtyMaxX = Mathf.Max(dirtyMaxX, maxX);
        dirtyMaxY = Mathf.Max(dirtyMaxY, maxY);
        maskDirty = true;
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
            if (transform.parent != mapRoot)
            {
                transform.SetParent(mapRoot, false);
            }
        }
    }

    private bool TryGetPixelIndexFromWorldPosition(Vector2 worldPosition, out int pixelIndex)
    {
        pixelIndex = -1;
        if (maskTextureWidth <= 0 || maskTextureHeight <= 0)
        {
            return false;
        }

        float normalizedX = (worldPosition.x - revealMin.x) / revealSize.x;
        float normalizedY = (worldPosition.y - revealMin.y) / revealSize.y;
        if (normalizedX < 0f || normalizedX > 1f || normalizedY < 0f || normalizedY > 1f)
        {
            return false;
        }

        int pixelX = Mathf.Clamp(Mathf.FloorToInt(normalizedX * maskTextureWidth), 0, maskTextureWidth - 1);
        int pixelY = Mathf.Clamp(Mathf.FloorToInt(normalizedY * maskTextureHeight), 0, maskTextureHeight - 1);
        pixelIndex = pixelY * maskTextureWidth + pixelX;
        return true;
    }

    private Vector3 PixelIndexToWorldPosition(int pixelIndex)
    {
        int pixelX = pixelIndex % maskTextureWidth;
        int pixelY = pixelIndex / maskTextureWidth;
        return PixelToWorldPosition(pixelX, pixelY);
    }

    private Vector3 PixelToWorldPosition(int pixelX, int pixelY)
    {
        float pixelWidth = revealSize.x / maskTextureWidth;
        float pixelHeight = revealSize.y / maskTextureHeight;
        return new Vector3(
            revealMin.x + (pixelX + 0.5f) * pixelWidth,
            revealMin.y + (pixelY + 0.5f) * pixelHeight,
            0f);
    }

    private bool BoundsChanged(Bounds currentBounds)
    {
        return revealMask == null
            || generatedMaskTexture == null
            || Vector3.Distance(revealBounds.center, currentBounds.center) > 0.01f
            || Vector3.Distance(revealBounds.size, currentBounds.size) > 0.01f;
    }

    private void LogMissingMapBoundsOnce()
    {
        if (hasLoggedMissingMapBounds)
        {
            return;
        }

        hasLoggedMissingMapBounds = true;
        Debug.LogWarning("[WorldRevealMaskController] 未找到可用于遮罩的地图范围。请确认 MapRoot 已生成，并且地图块下存在可渲染的 SpriteRenderer。", this);
    }

    private void DestroyGeneratedMask()
    {
        DestroyRuntimeObject(generatedMaskSprite);
        DestroyRuntimeObject(generatedMaskTexture);

        if (revealMask != null)
        {
            revealMask.sprite = null;
        }

        revealMask = null;
        generatedMaskSprite = null;
        generatedMaskTexture = null;
        maskPixels = null;
        dirtyPixels = null;
        maskDirty = false;
        hasDirtyRect = false;
        configurationDirty = false;
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
            return;
        }

        DestroyImmediate(target);
    }
}