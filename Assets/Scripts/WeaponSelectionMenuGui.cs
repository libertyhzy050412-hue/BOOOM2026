using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class WeaponSelectionMenuGui : MonoBehaviour
{
    private static readonly Color BackgroundBaseColor = CreateColor(28, 33, 52);
    private static readonly Color BackgroundTopBandColor = CreateColor(34, 39, 63, 0.96f);
    private static readonly Color BackgroundSideGlowColor = CreateColor(197, 145, 191, 0.08f);
    private static readonly Color BackgroundFooterShadeColor = CreateColor(12, 15, 24, 0.34f);
    private static readonly Color MainPanelFillColor = CreateColor(34, 38, 61, 0.95f);
    private static readonly Color CardFillColor = CreateColor(39, 44, 69, 0.97f);
    private static readonly Color CardOutlineColor = CreateColor(197, 145, 191, 0.34f);
    private static readonly Color DividerColor = CreateColor(197, 145, 191, 0.16f);
    private static readonly Color AccentPurpleColor = CreateColor(197, 145, 191);
    private static readonly Color AccentPurpleSoftColor = CreateColor(197, 145, 191, 0.72f);
    private static readonly Color TitleTextColor = CreateColor(236, 221, 239);
    private static readonly Color SubtitleTextColor = CreateColor(198, 189, 218);
    private static readonly Color ButtonTextColor = CreateColor(44, 32, 52);
    private static readonly Color StatusReadyColor = CreateColor(164, 225, 190);
    private static readonly Color StatusMissingColor = CreateColor(225, 144, 159);

    [System.Serializable]
    private sealed class WeaponOption
    {
        public string id = "weapon";
        public string displayName = "武器";
        [TextArea(2, 4)] public string description = "武器描述";
        public WeaponBase weaponPrefab;
        public bool enabled = true;
    }

    [SerializeField] private string title = "选择武器";
    [SerializeField] private string subtitle = "选择一个本局要使用的武器";
    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private string startSceneName = "OpenMenu";
    [SerializeField] private bool showBackButton = true;
    [SerializeField] private string backButtonText = "返回开始界面";
    [SerializeField] private List<WeaponOption> weaponOptions = new List<WeaponOption>();
    [SerializeField, Min(360f)] private float panelWidth = 760f;
    [SerializeField, Min(260f)] private float panelHeight = 460f;
    [SerializeField, Min(220f)] private float optionButtonWidth = 300f;
    [SerializeField, Min(88f)] private float optionButtonHeight = 108f;

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle cardTitleStyle;
    private GUIStyle cardDescriptionStyle;
    private GUIStyle optionButtonStyle;
    private GUIStyle disabledOptionButtonStyle;
    private GUIStyle descriptionStyle;
    private GUIStyle readyStateStyle;
    private GUIStyle missingStateStyle;
    private GUIStyle backButtonStyle;
    private Vector2 scrollPosition;
    private int lastLayoutSignature = int.MinValue;

    private void OnEnable()
    {
        AudioManager.PlayMenuMusic();
    }

    private void OnGUI()
    {
        EnsureStyles();

        DrawWeaponSelectionBackdrop();

        float margin = SimpleGuiTheme.Scale(14f);
        Rect panelRect = SimpleGuiTheme.CenterRect(0.978f, 0.9f, panelWidth, panelHeight, margin);
        DrawSurface(panelRect, MainPanelFillColor, CardOutlineColor, true);

        Rect contentRect = SimpleGuiTheme.Inset(panelRect, SimpleGuiTheme.Scale(20f), SimpleGuiTheme.Scale(20f));
        GUIContent titleContent = new GUIContent(title);
        GUIContent subtitleContent = new GUIContent(subtitle);
        float titleHeight = titleStyle.CalcHeight(titleContent, contentRect.width);
        Rect titleRect = new Rect(contentRect.x, contentRect.y, contentRect.width, titleHeight);
        float subtitleHeight = subtitleStyle.CalcHeight(subtitleContent, contentRect.width);
        Rect subtitleRect = new Rect(
            contentRect.x,
            titleRect.yMax + SimpleGuiTheme.Scale(6f),
            contentRect.width,
            subtitleHeight);

        GUI.Label(titleRect, titleContent, titleStyle);
        GUI.Label(subtitleRect, subtitleContent, subtitleStyle);

        float footerHeight = showBackButton ? Mathf.Max(SimpleGuiTheme.Scale(56f), 50f) : 0f;
        float footerSpacing = showBackButton ? SimpleGuiTheme.Scale(14f) : 0f;
        float optionTop = subtitleRect.yMax + SimpleGuiTheme.Scale(18f);
        Rect optionRect = new Rect(
            contentRect.x,
            optionTop,
            contentRect.width,
            Mathf.Max(0f, contentRect.yMax - optionTop - footerHeight - footerSpacing));

        DrawWeaponOptions(optionRect);
        DrawFooterDivider(contentRect, footerHeight, footerSpacing);

        if (showBackButton)
        {
            Rect backRect = new Rect(contentRect.x, contentRect.yMax - footerHeight,
                Mathf.Min(contentRect.width, SimpleGuiTheme.Scale(230f)), footerHeight);
            if (GuiAudioButton.Button("WeaponSelection/Back", backRect, backButtonText, backButtonStyle))
            {
                ReturnToStartScene();
            }
        }
    }

    private void DrawWeaponOptions(Rect rect)
    {
        List<WeaponOption> visibleOptions = GetVisibleOptions();
        if (visibleOptions.Count == 0)
        {
            DrawSurface(rect, CardFillColor, CardOutlineColor, false);
            GUI.Label(SimpleGuiTheme.Inset(rect, SimpleGuiTheme.Scale(24f), SimpleGuiTheme.Scale(24f)),
                "当前没有可选武器，请先在 Inspector 里配置 weaponOptions。", descriptionStyle);
            return;
        }

        float gap = SimpleGuiTheme.Scale(18f);
        int columnCount = rect.width >= SimpleGuiTheme.Scale(860f) ? 2 : 1;
        float cardWidth = columnCount == 1
            ? rect.width - SimpleGuiTheme.Scale(8f)
            : (rect.width - gap * (columnCount - 1) - SimpleGuiTheme.Scale(14f)) / columnCount;

        int rowCount = Mathf.CeilToInt(visibleOptions.Count / (float)columnCount);
        float availableRowHeight = (rect.height - Mathf.Max(0f, rowCount - 1) * gap) / Mathf.Max(1, rowCount);
        float minimumCardHeight = Mathf.Max(optionButtonHeight + SimpleGuiTheme.Scale(132f), SimpleGuiTheme.Scale(260f));
        float desiredCardHeight = rowCount == 1
            ? rect.height * (columnCount == 1 ? 0.8f : 0.62f)
            : availableRowHeight;
        float cardHeight = Mathf.Clamp(desiredCardHeight, minimumCardHeight, Mathf.Max(minimumCardHeight, availableRowHeight));
        float contentHeight = rowCount * cardHeight + Mathf.Max(0, rowCount - 1) * gap;
        float topOffset = contentHeight < rect.height ? Mathf.Max(0f, (rect.height - contentHeight) * 0.26f) : 0f;
        float viewWidth = rect.width - (contentHeight > rect.height ? SimpleGuiTheme.Scale(18f) : 0f);
        Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentHeight + topOffset, rect.height));

        scrollPosition = GUI.BeginScrollView(rect, scrollPosition, viewRect, false, contentHeight > rect.height);

        for (int index = 0; index < visibleOptions.Count; index++)
        {
            int row = index / columnCount;
            int column = index % columnCount;
            Rect cardRect = new Rect(
                column * (cardWidth + gap),
                topOffset + row * (cardHeight + gap),
                cardWidth,
                cardHeight);

            DrawWeaponCard(cardRect, visibleOptions[index]);
        }

        GUI.EndScrollView();
    }

    private void DrawWeaponCard(Rect cardRect, WeaponOption weaponOption)
    {
        bool isSelectable = weaponOption != null && weaponOption.weaponPrefab != null;
        DrawSurface(cardRect, CardFillColor, CardOutlineColor, false);

        Rect contentRect = SimpleGuiTheme.Inset(cardRect, SimpleGuiTheme.Scale(20f), SimpleGuiTheme.Scale(18f));
        float buttonHeight = Mathf.Max(SimpleGuiTheme.Scale(56f), optionButtonHeight * 0.44f);
        float statusWidth = Mathf.Min(SimpleGuiTheme.Scale(104f), contentRect.width * 0.24f);
        float titleWidth = Mathf.Max(0f, contentRect.width - statusWidth - SimpleGuiTheme.Scale(18f));
        GUIContent titleContent = new GUIContent(weaponOption.displayName);
        string statusText = isSelectable ? "可选择" : "未配置预制体";
        GUIContent statusContent = new GUIContent(statusText);
        string descriptionText = string.IsNullOrWhiteSpace(weaponOption.description) ? "暂无描述。" : weaponOption.description;
        GUIContent descriptionContent = new GUIContent(descriptionText);
        float titleHeight = Mathf.Max(SimpleGuiTheme.Scale(42f), cardTitleStyle.CalcHeight(titleContent, titleWidth));
        float statusHeight = Mathf.Max(SimpleGuiTheme.Scale(22f), readyStateStyle.CalcHeight(statusContent, statusWidth));
        float headerHeight = Mathf.Max(titleHeight, statusHeight);
        float descriptionHeight = Mathf.Max(SimpleGuiTheme.Scale(34f), cardDescriptionStyle.CalcHeight(descriptionContent, contentRect.width));
        float headerGap = SimpleGuiTheme.Scale(14f);
        float buttonGap = SimpleGuiTheme.Scale(20f);
        float contentBlockHeight = headerHeight + headerGap + descriptionHeight + buttonGap + buttonHeight;
        float blockTop = contentRect.y + Mathf.Max(0f, (contentRect.height - contentBlockHeight) * 0.34f);

        Rect titleRect = new Rect(contentRect.x, blockTop, titleWidth, titleHeight);
        Rect statusRect = new Rect(
            contentRect.xMax - statusWidth,
            blockTop + Mathf.Max(0f, (headerHeight - statusHeight) * 0.12f),
            statusWidth,
            statusHeight);
        float descriptionTop = blockTop + headerHeight + headerGap;
        Rect descriptionRect = new Rect(
            contentRect.x,
            descriptionTop,
            contentRect.width,
            descriptionHeight);
        Rect buttonRect = new Rect(
            contentRect.x,
            descriptionRect.yMax + buttonGap,
            contentRect.width,
            buttonHeight);

        GUI.Label(titleRect, titleContent, cardTitleStyle);
        GUI.Label(statusRect, statusContent, isSelectable ? readyStateStyle : missingStateStyle);
        GUI.Label(descriptionRect, descriptionContent, cardDescriptionStyle);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = isSelectable;
        if (GuiAudioButton.Button($"WeaponSelection/Option/{weaponOption.id}", buttonRect,
                isSelectable ? $"选择 {weaponOption.displayName}" : "尚未配置",
                isSelectable ? optionButtonStyle : disabledOptionButtonStyle, isSelectable))
        {
            SelectWeapon(weaponOption);
        }

        GUI.enabled = previousEnabled;
    }

    private List<WeaponOption> GetVisibleOptions()
    {
        List<WeaponOption> visibleOptions = new List<WeaponOption>();
        if (weaponOptions == null)
        {
            return visibleOptions;
        }

        for (int index = 0; index < weaponOptions.Count; index++)
        {
            WeaponOption weaponOption = weaponOptions[index];
            if (weaponOption == null || !weaponOption.enabled)
            {
                continue;
            }

            visibleOptions.Add(weaponOption);
        }

        return visibleOptions;
    }

    private void SelectWeapon(WeaponOption weaponOption)
    {
        if (weaponOption == null || weaponOption.weaponPrefab == null)
        {
            Debug.LogWarning("[WeaponSelectionMenuGui] 选中的武器选项没有绑定 weaponPrefab。", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(mainSceneName))
        {
            Debug.LogWarning("[WeaponSelectionMenuGui] 未配置主游戏场景名，无法进入游戏。", this);
            return;
        }

        WeaponSelectionSession.SelectWeapon(weaponOption.id, weaponOption.displayName, weaponOption.weaponPrefab);
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainSceneName);
    }

    private void ReturnToStartScene()
    {
        WeaponSelectionSession.ClearSelection();
        if (string.IsNullOrWhiteSpace(startSceneName))
        {
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(startSceneName);
    }

    private void DrawWeaponSelectionBackdrop()
    {
        SimpleGuiTheme.DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), BackgroundBaseColor);

        float topBandHeight = Mathf.Max(SimpleGuiTheme.Scale(140f), Screen.height * 0.22f);
        SimpleGuiTheme.DrawSolidRect(new Rect(0f, 0f, Screen.width, topBandHeight), BackgroundTopBandColor);

        float sideGlowWidth = Mathf.Min(Screen.width * 0.14f, SimpleGuiTheme.Scale(190f));
        SimpleGuiTheme.DrawSolidRect(new Rect(0f, 0f, sideGlowWidth, Screen.height), BackgroundSideGlowColor);

        float rightShadeWidth = Mathf.Min(Screen.width * 0.22f, SimpleGuiTheme.Scale(300f));
        SimpleGuiTheme.DrawSolidRect(
            new Rect(Screen.width - rightShadeWidth, 0f, rightShadeWidth, Screen.height),
            CreateColor(18, 21, 35, 0.28f));

        float footerHeight = Mathf.Max(SimpleGuiTheme.Scale(96f), Screen.height * 0.16f);
        SimpleGuiTheme.DrawSolidRect(
            new Rect(0f, Screen.height - footerHeight, Screen.width, footerHeight),
            BackgroundFooterShadeColor);

        Rect centerGlowRect = SimpleGuiTheme.CenterRect(0.76f, 0.54f, SimpleGuiTheme.Scale(560f), SimpleGuiTheme.Scale(320f), SimpleGuiTheme.Scale(24f));
        SimpleGuiTheme.DrawSolidRect(centerGlowRect, CreateColor(197, 145, 191, 0.035f));
    }

    private void DrawFooterDivider(Rect contentRect, float footerHeight, float footerSpacing)
    {
        if (!showBackButton)
        {
            return;
        }

        float dividerY = contentRect.yMax - footerHeight - footerSpacing * 0.55f;
        float dividerHeight = Mathf.Max(1f, SimpleGuiTheme.Scale(1.5f));
        SimpleGuiTheme.DrawSolidRect(new Rect(contentRect.x, dividerY, contentRect.width, dividerHeight), DividerColor);
    }

    private void DrawSurface(Rect rect, Color fillColor, Color outlineColor, bool drawTopAccent)
    {
        float shadowOffset = SimpleGuiTheme.Scale(10f);
        SimpleGuiTheme.DrawSolidRect(
            new Rect(rect.x + shadowOffset, rect.y + shadowOffset, rect.width, rect.height),
            new Color(0f, 0f, 0f, 0.18f));
        SimpleGuiTheme.DrawSolidRect(rect, fillColor);

        if (drawTopAccent)
        {
            float accentHeight = Mathf.Max(SimpleGuiTheme.Scale(4f), 3f);
            SimpleGuiTheme.DrawSolidRect(
                new Rect(rect.x, rect.y, rect.width, accentHeight),
                AccentPurpleSoftColor);
        }

        DrawOutline(rect, outlineColor, Mathf.Max(1f, SimpleGuiTheme.Scale(1.5f)));
    }

    private static void DrawOutline(Rect rect, Color color, float thickness)
    {
        SimpleGuiTheme.DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        SimpleGuiTheme.DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        SimpleGuiTheme.DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        SimpleGuiTheme.DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private void EnsureStyles()
    {
        int layoutSignature = SimpleGuiTheme.GetLayoutSignature();
        if (titleStyle != null && lastLayoutSignature == layoutSignature)
        {
            return;
        }

        lastLayoutSignature = layoutSignature;

        titleStyle = SimpleGuiTheme.CreateLabelStyle(48, FontStyle.Bold, TextAnchor.UpperLeft,
            TitleTextColor, true);
        subtitleStyle = SimpleGuiTheme.CreateLabelStyle(24, FontStyle.Normal, TextAnchor.UpperLeft,
            SubtitleTextColor, true);
        cardTitleStyle = SimpleGuiTheme.CreateLabelStyle(36, FontStyle.Bold, TextAnchor.UpperLeft,
            TitleTextColor, false);
        cardDescriptionStyle = SimpleGuiTheme.CreateLabelStyle(22, FontStyle.Normal, TextAnchor.UpperLeft,
            SubtitleTextColor, true);
        descriptionStyle = SimpleGuiTheme.CreateLabelStyle(22, FontStyle.Normal, TextAnchor.MiddleCenter,
            SubtitleTextColor, true);
        readyStateStyle = SimpleGuiTheme.CreateLabelStyle(16, FontStyle.Bold, TextAnchor.UpperRight,
            StatusReadyColor, false);
        missingStateStyle = SimpleGuiTheme.CreateLabelStyle(16, FontStyle.Bold, TextAnchor.UpperRight,
            StatusMissingColor, false);

        optionButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            20,
            AccentPurpleColor,
            CreateColor(212, 163, 207),
            CreateColor(181, 129, 175),
            ButtonTextColor);

        disabledOptionButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            20,
            CreateColor(58, 63, 92),
            CreateColor(66, 72, 103),
            CreateColor(48, 53, 79),
            CreateColor(160, 151, 179));

        backButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            18,
            CreateColor(43, 48, 76),
            CreateColor(51, 57, 88),
            CreateColor(34, 39, 63),
            TitleTextColor);
    }

    private void OnValidate()
    {
        panelWidth = Mathf.Max(360f, panelWidth);
        panelHeight = Mathf.Max(260f, panelHeight);
        optionButtonWidth = Mathf.Max(220f, optionButtonWidth);
        optionButtonHeight = Mathf.Max(88f, optionButtonHeight);
    }

    private static Color CreateColor(byte red, byte green, byte blue, float alpha = 1f)
    {
        return new Color(red / 255f, green / 255f, blue / 255f, Mathf.Clamp01(alpha));
    }
}