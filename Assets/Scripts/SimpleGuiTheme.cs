using UnityEngine;

public static class SimpleGuiTheme
{
    public static readonly Color BackdropBaseColor = new Color(0.08f, 0.11f, 0.15f, 1f);
    public static readonly Color BackdropTopBandColor = new Color(0.15f, 0.24f, 0.31f, 0.82f);
    public static readonly Color BackdropAccentColor = new Color(0.89f, 0.72f, 0.28f, 0.12f);
    public static readonly Color PanelFillColor = new Color(0.11f, 0.15f, 0.2f, 0.94f);
    public static readonly Color PanelFillLightColor = new Color(0.15f, 0.2f, 0.26f, 0.96f);
    public static readonly Color PanelOutlineColor = new Color(0.98f, 0.82f, 0.46f, 0.3f);
    public static readonly Color AccentColor = new Color(0.95f, 0.78f, 0.34f, 1f);
    public static readonly Color AccentMutedColor = new Color(0.73f, 0.82f, 0.93f, 1f);
    public static readonly Color TextPrimaryColor = new Color(0.96f, 0.97f, 0.98f, 1f);
    public static readonly Color TextSecondaryColor = new Color(0.77f, 0.83f, 0.9f, 1f);
    public static readonly Color SuccessColor = new Color(0.43f, 0.84f, 0.58f, 1f);
    public static readonly Color DangerColor = new Color(0.84f, 0.42f, 0.39f, 1f);

    private static Texture2D whiteTexture;

    public static float Scale(float baseValue)
    {
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.82f, 1.2f);
        return baseValue * scale;
    }

    public static Rect CenterRect(float widthRatio, float heightRatio, float minWidth, float minHeight, float margin)
    {
        float width = Screen.width * widthRatio;
        width = Mathf.Max(width, minWidth);
        width = Mathf.Min(width, Mathf.Max(0f, Screen.width - margin * 2f));

        float height = Screen.height * heightRatio;
        height = Mathf.Max(height, minHeight);
        height = Mathf.Min(height, Mathf.Max(0f, Screen.height - margin * 2f));

        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }

    public static Rect Inset(Rect rect, float horizontalPadding, float verticalPadding)
    {
        return new Rect(
            rect.x + horizontalPadding,
            rect.y + verticalPadding,
            rect.width - horizontalPadding * 2f,
            rect.height - verticalPadding * 2f);
    }

    public static void DrawBackdrop()
    {
        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), BackdropBaseColor);

        float topBandHeight = Mathf.Max(Scale(120f), Screen.height * 0.2f);
        DrawSolidRect(new Rect(0f, 0f, Screen.width, topBandHeight), BackdropTopBandColor);

        float leftAccentWidth = Mathf.Min(Screen.width * 0.14f, Scale(190f));
        DrawSolidRect(new Rect(0f, 0f, leftAccentWidth, Screen.height), BackdropAccentColor);

        float rightAccentWidth = Mathf.Min(Screen.width * 0.2f, Scale(260f));
        DrawSolidRect(new Rect(Screen.width - rightAccentWidth, 0f, rightAccentWidth, Screen.height), new Color(0.07f, 0.11f, 0.16f, 0.42f));

        float footerHeight = Mathf.Max(Scale(80f), Screen.height * 0.16f);
        DrawSolidRect(new Rect(0f, Screen.height - footerHeight, Screen.width, footerHeight), new Color(0f, 0f, 0f, 0.16f));

        Rect glowRect = CenterRect(0.72f, 0.56f, Scale(520f), Scale(320f), Scale(24f));
        DrawSolidRect(glowRect, new Color(1f, 1f, 1f, 0.03f));
    }

    public static void DrawOverlay(float alpha = 0.58f)
    {
        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.05f, alpha));
    }

    public static void DrawPanel(Rect rect)
    {
        DrawPanel(rect, PanelFillColor);
    }

    public static void DrawPanel(Rect rect, Color fillColor)
    {
        float shadowOffset = Scale(10f);
        DrawSolidRect(new Rect(rect.x + shadowOffset, rect.y + shadowOffset, rect.width, rect.height), new Color(0f, 0f, 0f, 0.18f));
        DrawSolidRect(rect, fillColor);

        float accentHeight = Mathf.Max(Scale(6f), 4f);
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, accentHeight), new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.95f));
        DrawOutline(rect, PanelOutlineColor, Mathf.Max(1f, Scale(2f)));
    }

    public static GUIStyle CreateLabelStyle(int fontSize, FontStyle fontStyle, TextAnchor alignment, Color textColor, bool wordWrap = false)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Scale(fontSize)),
            fontStyle = fontStyle,
            alignment = alignment,
            wordWrap = wordWrap,
            clipping = wordWrap ? TextClipping.Clip : TextClipping.Overflow
        };

        style.normal.textColor = textColor;
        return style;
    }

    public static GUIStyle CreateButtonStyle(
        int fontSize,
        Color backgroundColor,
        Color hoverColor,
        Color activeColor,
        Color textColor,
        TextAnchor alignment = TextAnchor.MiddleCenter,
        int horizontalPadding = 18,
        int verticalPadding = 12,
        bool wordWrap = false)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(Scale(fontSize)),
            fontStyle = FontStyle.Bold,
            alignment = alignment,
            wordWrap = wordWrap,
            padding = new RectOffset(
                Mathf.RoundToInt(Scale(horizontalPadding)),
                Mathf.RoundToInt(Scale(horizontalPadding)),
                Mathf.RoundToInt(Scale(verticalPadding)),
                Mathf.RoundToInt(Scale(verticalPadding)))
        };

        style.normal.background = CreateColorTexture(backgroundColor);
        style.hover.background = CreateColorTexture(hoverColor);
        style.active.background = CreateColorTexture(activeColor);
        style.focused.background = style.normal.background;
        style.onNormal.background = style.normal.background;
        style.onHover.background = style.hover.background;
        style.onActive.background = style.active.background;
        style.onFocused.background = style.focused.background;

        style.normal.textColor = textColor;
        style.hover.textColor = textColor;
        style.active.textColor = textColor;
        style.focused.textColor = textColor;
        style.onNormal.textColor = textColor;
        style.onHover.textColor = textColor;
        style.onActive.textColor = textColor;
        style.onFocused.textColor = textColor;
        return style;
    }

    public static void DrawSolidRect(Rect rect, Color color)
    {
        EnsureWhiteTexture();

        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, whiteTexture);
        GUI.color = previousColor;
    }

    private static void DrawOutline(Rect rect, Color color, float thickness)
    {
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private static void EnsureWhiteTexture()
    {
        if (whiteTexture == null)
        {
            whiteTexture = Texture2D.whiteTexture;
        }
    }

    private static Texture2D CreateColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}