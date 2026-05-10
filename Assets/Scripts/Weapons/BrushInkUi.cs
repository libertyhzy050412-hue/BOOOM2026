using UnityEngine;

[DisallowMultipleComponent]
public sealed class BrushInkUi : MonoBehaviour
{
    [SerializeField] private BrushWeapon brushWeapon;
    [SerializeField] private bool showWhenWeaponDisabled;
    [SerializeField] private bool showNumericValue = true;
    [SerializeField] private string inkLabel = "颜料";
    [SerializeField, Min(120f)] private float uiWidth = 240f;
    [SerializeField, Min(24f)] private float uiHeight = 26f;
    [SerializeField, Min(0f)] private float screenMarginX = 18f;
    [SerializeField, Min(0f)] private float screenMarginY = 18f;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color fillColor = new Color(0.95f, 0.74f, 0.2f, 1f);
    [SerializeField] private Color recoveringFillColor = new Color(0.45f, 0.9f, 0.55f, 1f);
    [SerializeField] private Color textColor = Color.white;

    private static Texture2D whiteTexture;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnGUI()
    {
        ResolveReferences();
        if (!ShouldDraw())
        {
            return;
        }

        EnsureGuiResources();
        DrawInkBar();
    }

    private bool ShouldDraw()
    {
        if (brushWeapon == null)
        {
            return false;
        }

        return showWhenWeaponDisabled || brushWeapon.WeaponEnabled;
    }

    private void DrawInkBar()
    {
        float normalizedInk = brushWeapon.NormalizedInkAmount;
        Rect outerRect = new Rect(screenMarginX, screenMarginY, uiWidth, uiHeight);
        Rect innerRect = new Rect(outerRect.x + 2f, outerRect.y + 2f, outerRect.width - 4f, outerRect.height - 4f);
        Rect fillRect = new Rect(innerRect.x, innerRect.y, innerRect.width * normalizedInk, innerRect.height);

        DrawSolidRect(outerRect, backgroundColor);
        DrawSolidRect(innerRect, new Color(0.12f, 0.12f, 0.12f, 0.95f));

        if (fillRect.width > 0f)
        {
            DrawSolidRect(fillRect, brushWeapon.InkRecoveryActive ? recoveringFillColor : fillColor);
        }

        GUI.Label(new Rect(outerRect.x, outerRect.y - 24f, uiWidth, 22f), inkLabel, labelStyle);

        if (showNumericValue)
        {
            string valueText = $"{Mathf.CeilToInt(brushWeapon.CurrentInkAmount)} / {Mathf.CeilToInt(brushWeapon.MaxInkAmount)}";
            GUI.Label(new Rect(outerRect.x, outerRect.y + 2f, uiWidth - 8f, outerRect.height), valueText, valueStyle);
        }
    }

    private void ResolveReferences()
    {
        if (brushWeapon == null)
        {
            brushWeapon = GetComponentInParent<BrushWeapon>();
        }
    }

    private void EnsureGuiResources()
    {
        if (whiteTexture == null)
        {
            whiteTexture = Texture2D.whiteTexture;
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerLeft,
                normal = { textColor = textColor }
            };
        }

        if (valueStyle == null)
        {
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = textColor }
            };
        }
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, whiteTexture);
        GUI.color = previousColor;
    }

    private void OnValidate()
    {
        uiWidth = Mathf.Max(120f, uiWidth);
        uiHeight = Mathf.Max(24f, uiHeight);
        screenMarginX = Mathf.Max(0f, screenMarginX);
        screenMarginY = Mathf.Max(0f, screenMarginY);
    }
}