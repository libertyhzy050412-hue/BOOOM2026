using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitDamageFlash : MonoBehaviour
{
    [SerializeField] private Transform spriteRoot;
    [SerializeField] private Color flashColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField, Min(0.01f)] private float flashDuration = 0.08f;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private float flashTimer;
    private bool flashing;

    private void Awake()
    {
        RefreshSpriteCache();
    }

    private void LateUpdate()
    {
        if (!flashing)
        {
            return;
        }

        flashTimer -= Time.deltaTime;
        if (flashTimer > 0f)
        {
            return;
        }

        RestoreColors();
    }

    public void PlayFlash()
    {
        RefreshSpriteCache();
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return;
        }

        CacheOriginalColors();
        for (int index = 0; index < spriteRenderers.Length; index++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[index];
            if (spriteRenderer == null)
            {
                continue;
            }

            Color original = originalColors[index];
            spriteRenderer.color = new Color(flashColor.r, flashColor.g, flashColor.b, original.a);
        }

        flashTimer = flashDuration;
        flashing = true;
    }

    private void RefreshSpriteCache()
    {
        Transform root = spriteRoot != null ? spriteRoot : transform;
        spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        if (originalColors == null || originalColors.Length != spriteRenderers.Length)
        {
            originalColors = new Color[spriteRenderers.Length];
            CacheOriginalColors();
        }
    }

    private void CacheOriginalColors()
    {
        if (spriteRenderers == null)
        {
            return;
        }

        if (originalColors == null || originalColors.Length != spriteRenderers.Length)
        {
            originalColors = new Color[spriteRenderers.Length];
        }

        for (int index = 0; index < spriteRenderers.Length; index++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[index];
            originalColors[index] = spriteRenderer != null ? spriteRenderer.color : Color.white;
        }
    }

    private void RestoreColors()
    {
        if (spriteRenderers != null)
        {
            for (int index = 0; index < spriteRenderers.Length; index++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[index];
                if (spriteRenderer == null)
                {
                    continue;
                }

                spriteRenderer.color = originalColors[index];
            }
        }

        flashing = false;
        flashTimer = 0f;
    }

    private void OnDisable()
    {
        RestoreColors();
    }

    private void OnValidate()
    {
        flashDuration = Mathf.Max(0.01f, flashDuration);
    }
}
