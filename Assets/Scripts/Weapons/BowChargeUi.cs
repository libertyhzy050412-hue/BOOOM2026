using UnityEngine;

[DisallowMultipleComponent]
public sealed class BowChargeUi : MonoBehaviour
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
    [SerializeField] private bool showWhenIdle = true;
    [SerializeField] private bool showNumericValue = true;
    [SerializeField] private bool showChargeThreshold = true;
    [SerializeField] private string chargeLabel = "弓箭蓄力";
    [SerializeField] private string idleText = "按住攻击开始蓄力";
    [SerializeField] private string chargingText = "蓄力中";
    [SerializeField] private string cooldownText = "冷却中";
    [SerializeField] private ScreenAnchor screenAnchor = ScreenAnchor.BottomRight;
    [SerializeField, Min(140f)] private float uiWidth = 260f;
    [SerializeField, Min(24f)] private float uiHeight = 26f;
    [SerializeField, Min(0f)] private float screenMarginX = 18f;
    [SerializeField, Min(0f)] private float screenMarginY = 18f;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color fillColor = new Color(0.92f, 0.67f, 0.25f, 1f);
    [SerializeField] private Color readyLineColor = new Color(0.47f, 0.79f, 0.98f, 0.9f);
    [SerializeField] private Color thresholdColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color textColor = Color.white;

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
        DrawChargeBar();
    }

    private bool ShouldDraw()
    {
        if (bowWeapon == null)
        {
            return false;
        }

        if (!showWhenWeaponDisabled && !bowWeapon.WeaponEnabled)
        {
            return false;
        }

        return showWhenIdle || bowWeapon.IsCharging || bowWeapon.IsInAttackCooldown;
    }

    private void DrawChargeBar()
    {
        float normalizedCharge = bowWeapon.ChargeNormalized;
        float totalHeight = uiHeight + SimpleGuiTheme.Scale(38f);
        Rect panelRect = ResolvePanelRect(uiWidth, totalHeight);
        SimpleGuiTheme.DrawPanel(panelRect, new Color(0.1f, 0.14f, 0.18f, 0.92f));

        Rect contentRect = SimpleGuiTheme.Inset(panelRect, SimpleGuiTheme.Scale(12f), SimpleGuiTheme.Scale(12f));
        float headerHeight = SimpleGuiTheme.Scale(18f);
        Rect labelRect = new Rect(contentRect.x, contentRect.y, contentRect.width * 0.52f, headerHeight);
        Rect valueRect = new Rect(contentRect.x + contentRect.width * 0.48f, contentRect.y, contentRect.width * 0.52f, headerHeight);
        Rect outerRect = new Rect(contentRect.x, contentRect.yMax - uiHeight, contentRect.width, uiHeight);
        Rect innerRect = new Rect(outerRect.x + 2f, outerRect.y + 2f, outerRect.width - 4f, outerRect.height - 4f);
        Rect fillRect = new Rect(innerRect.x, innerRect.y, innerRect.width * normalizedCharge, innerRect.height);

        SimpleGuiTheme.DrawSolidRect(outerRect, backgroundColor);
        SimpleGuiTheme.DrawSolidRect(innerRect, new Color(0.12f, 0.12f, 0.12f, 0.95f));

        if (fillRect.width > 0f)
        {
            SimpleGuiTheme.DrawSolidRect(fillRect, fillColor);
        }

        if (!bowWeapon.IsCharging && !bowWeapon.IsInAttackCooldown)
        {
            float readyLineHeight = Mathf.Max(2f, SimpleGuiTheme.Scale(2f));
            float readyLineY = innerRect.center.y - readyLineHeight * 0.5f;
            SimpleGuiTheme.DrawSolidRect(new Rect(innerRect.x, readyLineY, innerRect.width, readyLineHeight), readyLineColor);
        }

        if (showChargeThreshold)
        {
            DrawThresholdMarker(innerRect);
        }

        GUI.Label(labelRect, chargeLabel, labelStyle);
        GUI.Label(valueRect, BuildStatusText(normalizedCharge), valueStyle);
    }

    private void DrawThresholdMarker(Rect innerRect)
    {
        float threshold = bowWeapon.MinimumChargeNormalizedToFire;
        if (threshold <= 0f || threshold >= 1f)
        {
            return;
        }

        float markerWidth = Mathf.Max(2f, SimpleGuiTheme.Scale(2f));
        float markerX = innerRect.x + innerRect.width * threshold - markerWidth * 0.5f;
        SimpleGuiTheme.DrawSolidRect(new Rect(markerX, innerRect.y, markerWidth, innerRect.height), thresholdColor);
    }

    private string BuildStatusText(float normalizedCharge)
    {
        if (bowWeapon.IsInAttackCooldown)
        {
            return showNumericValue
                ? $"{cooldownText} {bowWeapon.AttackCooldownRemaining:0.00}s"
                : cooldownText;
        }

        if (bowWeapon.IsCharging)
        {
            return showNumericValue
                ? $"{chargingText} {Mathf.RoundToInt(normalizedCharge * 100f)}%"
                : chargingText;
        }

        return idleText;
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

        return new Rect(x, y, width, height);
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
        if (labelStyle == null)
        {
            labelStyle = SimpleGuiTheme.CreateLabelStyle(15, FontStyle.Bold, TextAnchor.UpperLeft, textColor, false);
        }

        if (valueStyle == null)
        {
            valueStyle = SimpleGuiTheme.CreateLabelStyle(14, FontStyle.Bold, TextAnchor.UpperRight, textColor, false);
        }
    }

    private void OnValidate()
    {
        uiWidth = Mathf.Max(140f, uiWidth);
        uiHeight = Mathf.Max(24f, uiHeight);
        screenMarginX = Mathf.Max(0f, screenMarginX);
        screenMarginY = Mathf.Max(0f, screenMarginY);
    }
}