using UnityEngine;

[DisallowMultipleComponent]
public sealed class BowInkUi : MonoBehaviour
{
    private enum ScreenAnchor
    {
        BottomLeft,
        TopLeft,
        BottomRight,
        TopRight
    }

    [SerializeField] private BowWeapon bowWeapon;
    [SerializeField] private bool showWhenWeaponDisabled;
    [SerializeField] private bool showNumericValue = true;
    [SerializeField] private string inkLabel = "箭矢颜料";
    [SerializeField] private ScreenAnchor screenAnchor = ScreenAnchor.BottomRight;
    [SerializeField, Min(120f)] private float uiWidth = 240f;
    [SerializeField, Min(24f)] private float uiHeight = 26f;
    [SerializeField, Min(0f)] private float screenMarginX = 18f;
    [SerializeField, Min(0f)] private float screenMarginY = 84f;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color fillColor = new Color(0.92f, 0.67f, 0.25f, 1f);
    [SerializeField] private Color recoveringFillColor = new Color(0.45f, 0.9f, 0.55f, 1f);
    [SerializeField] private Color textColor = Color.white;

    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private int lastLayoutSignature = int.MinValue;

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
        if (bowWeapon == null)
        {
            return false;
        }

        return showWhenWeaponDisabled || bowWeapon.WeaponEnabled;
    }

    private void DrawInkBar()
    {
        float normalizedInk = bowWeapon.NormalizedInkAmount;
        float totalHeight = uiHeight + SimpleGuiTheme.Scale(38f);
        Rect panelRect = ResolvePanelRect(uiWidth, totalHeight);
        SimpleGuiTheme.DrawPanel(panelRect, new Color(0.1f, 0.14f, 0.18f, 0.92f));

        Rect contentRect = SimpleGuiTheme.Inset(panelRect, SimpleGuiTheme.Scale(12f), SimpleGuiTheme.Scale(12f));
        float headerHeight = SimpleGuiTheme.Scale(18f);
        Rect labelRect = new Rect(contentRect.x, contentRect.y, contentRect.width * 0.55f, headerHeight);
        Rect valueRect = new Rect(contentRect.x + contentRect.width * 0.45f, contentRect.y, contentRect.width * 0.55f, headerHeight);
        Rect outerRect = new Rect(contentRect.x, contentRect.yMax - uiHeight, contentRect.width, uiHeight);
        Rect innerRect = new Rect(outerRect.x + 2f, outerRect.y + 2f, outerRect.width - 4f, outerRect.height - 4f);
        Rect fillRect = new Rect(innerRect.x, innerRect.y, innerRect.width * normalizedInk, innerRect.height);

        SimpleGuiTheme.DrawSolidRect(outerRect, backgroundColor);
        SimpleGuiTheme.DrawSolidRect(innerRect, new Color(0.12f, 0.12f, 0.12f, 0.95f));

        if (fillRect.width > 0f)
        {
            SimpleGuiTheme.DrawSolidRect(fillRect, bowWeapon.InkRecoveryActive ? recoveringFillColor : fillColor);
        }

        GUI.Label(labelRect, inkLabel, labelStyle);

        if (showNumericValue)
        {
            string valueText = $"{Mathf.CeilToInt(bowWeapon.CurrentInkAmount)} / {Mathf.CeilToInt(bowWeapon.MaxInkAmount)}";
            GUI.Label(valueRect, valueText, valueStyle);
        }
    }

    private Rect ResolvePanelRect(float width, float height)
    {
        float x = screenMarginX;
        float y = screenMarginY;

        switch (screenAnchor)
        {
            case ScreenAnchor.TopLeft:
                break;
            case ScreenAnchor.BottomLeft:
                y = Screen.height - screenMarginY - height;
                break;
            case ScreenAnchor.BottomRight:
                x = Screen.width - screenMarginX - width;
                y = Screen.height - screenMarginY - height;
                break;
            case ScreenAnchor.TopRight:
                x = Screen.width - screenMarginX - width;
                break;
        }

        return SimpleGuiTheme.ClampToSafeArea(new Rect(x, y, width, height));
    }

    private void ResolveReferences()
    {
        if (bowWeapon == null)
        {
            bowWeapon = GetComponentInParent<BowWeapon>();
        }
    }

    private void EnsureGuiResources()
    {
        int layoutSignature = SimpleGuiTheme.GetLayoutSignature();
        if (labelStyle != null && lastLayoutSignature == layoutSignature)
        {
            return;
        }

        lastLayoutSignature = layoutSignature;
        labelStyle = SimpleGuiTheme.CreateLabelStyle(15, FontStyle.Bold, TextAnchor.UpperLeft, textColor, false);
        valueStyle = SimpleGuiTheme.CreateLabelStyle(14, FontStyle.Bold, TextAnchor.UpperRight, textColor, false);
    }

    private void OnValidate()
    {
        uiWidth = Mathf.Max(120f, uiWidth);
        uiHeight = Mathf.Max(24f, uiHeight);
        screenMarginX = Mathf.Max(0f, screenMarginX);
        screenMarginY = Mathf.Max(0f, screenMarginY);
    }
}