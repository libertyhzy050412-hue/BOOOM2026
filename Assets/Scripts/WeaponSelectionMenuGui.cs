using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class WeaponSelectionMenuGui : MonoBehaviour
{
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
    [SerializeField] private string startSceneName = "StartScene";
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

        SimpleGuiTheme.DrawBackdrop();

        float margin = SimpleGuiTheme.Scale(24f);
        Rect panelRect = SimpleGuiTheme.CenterRect(0.92f, 0.86f, panelWidth, panelHeight, margin);
        SimpleGuiTheme.DrawPanel(panelRect, SimpleGuiTheme.PanelFillLightColor);

        Rect contentRect = SimpleGuiTheme.Inset(panelRect, SimpleGuiTheme.Scale(28f), SimpleGuiTheme.Scale(28f));
        float headerHeight = SimpleGuiTheme.Scale(84f);
        Rect headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, headerHeight);

        GUI.Label(new Rect(headerRect.x, headerRect.y, headerRect.width, SimpleGuiTheme.Scale(34f)), title, titleStyle);
        GUI.Label(
            new Rect(headerRect.x, headerRect.y + SimpleGuiTheme.Scale(40f), headerRect.width,
                SimpleGuiTheme.Scale(40f)), subtitle, subtitleStyle);

        float footerHeight = showBackButton ? SimpleGuiTheme.Scale(48f) : 0f;
        float footerSpacing = showBackButton ? SimpleGuiTheme.Scale(16f) : 0f;
        Rect optionRect = new Rect(
            contentRect.x,
            headerRect.yMax + SimpleGuiTheme.Scale(16f),
            contentRect.width,
            contentRect.height - headerHeight - SimpleGuiTheme.Scale(16f) - footerHeight - footerSpacing);

        DrawWeaponOptions(optionRect);

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
            SimpleGuiTheme.DrawPanel(rect, new Color(0.1f, 0.14f, 0.18f, 0.9f));
            GUI.Label(SimpleGuiTheme.Inset(rect, SimpleGuiTheme.Scale(24f), SimpleGuiTheme.Scale(24f)),
                "当前没有可选武器，请先在 Inspector 里配置 weaponOptions。", descriptionStyle);
            return;
        }

        float gap = SimpleGuiTheme.Scale(18f);
        int columnCount = rect.width >= SimpleGuiTheme.Scale(760f) ? 2 : 1;
        float cardHeight = Mathf.Max(optionButtonHeight + SimpleGuiTheme.Scale(70f), SimpleGuiTheme.Scale(210f));
        float cardWidth = columnCount == 1
            ? rect.width - SimpleGuiTheme.Scale(8f)
            : (rect.width - gap * (columnCount - 1) - SimpleGuiTheme.Scale(14f)) / columnCount;

        int rowCount = Mathf.CeilToInt(visibleOptions.Count / (float)columnCount);
        float contentHeight = rowCount * cardHeight + Mathf.Max(0, rowCount - 1) * gap;
        float viewWidth = rect.width - (contentHeight > rect.height ? SimpleGuiTheme.Scale(18f) : 0f);
        Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);

        scrollPosition = GUI.BeginScrollView(rect, scrollPosition, viewRect, false, contentHeight > rect.height);

        for (int index = 0; index < visibleOptions.Count; index++)
        {
            int row = index / columnCount;
            int column = index % columnCount;
            Rect cardRect = new Rect(
                column * (cardWidth + gap),
                row * (cardHeight + gap),
                cardWidth,
                cardHeight);

            DrawWeaponCard(cardRect, visibleOptions[index]);
        }

        GUI.EndScrollView();
    }

    private void DrawWeaponCard(Rect cardRect, WeaponOption weaponOption)
    {
        bool isSelectable = weaponOption != null && weaponOption.weaponPrefab != null;
        SimpleGuiTheme.DrawPanel(cardRect, new Color(0.11f, 0.16f, 0.2f, 0.96f));

        Rect contentRect = SimpleGuiTheme.Inset(cardRect, SimpleGuiTheme.Scale(18f), SimpleGuiTheme.Scale(16f));
        float buttonHeight = Mathf.Max(SimpleGuiTheme.Scale(46f), optionButtonHeight * 0.42f);
        float titleHeight = SimpleGuiTheme.Scale(34f);
        float statusHeight = SimpleGuiTheme.Scale(22f);

        Rect titleRect = new Rect(contentRect.x, contentRect.y, contentRect.width * 0.7f, titleHeight);
        Rect statusRect = new Rect(contentRect.x + contentRect.width * 0.52f, contentRect.y + SimpleGuiTheme.Scale(4f),
            contentRect.width * 0.48f, statusHeight);
        Rect buttonRect = new Rect(contentRect.x, contentRect.yMax - buttonHeight, contentRect.width, buttonHeight);
        Rect descriptionRect = new Rect(
            contentRect.x,
            titleRect.yMax + SimpleGuiTheme.Scale(12f),
            contentRect.width,
            buttonRect.y - titleRect.yMax - SimpleGuiTheme.Scale(22f));

        GUI.Label(titleRect, weaponOption.displayName, cardTitleStyle);
        GUI.Label(statusRect, isSelectable ? "可选择" : "未配置预制体", isSelectable ? readyStateStyle : missingStateStyle);
        GUI.Label(descriptionRect,
            string.IsNullOrWhiteSpace(weaponOption.description) ? "暂无描述。" : weaponOption.description,
            cardDescriptionStyle);

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

    private void EnsureStyles()
    {
        int layoutSignature = SimpleGuiTheme.GetLayoutSignature();
        if (titleStyle != null && lastLayoutSignature == layoutSignature)
        {
            return;
        }

        lastLayoutSignature = layoutSignature;

        titleStyle = SimpleGuiTheme.CreateLabelStyle(30, FontStyle.Bold, TextAnchor.UpperLeft,
            SimpleGuiTheme.TextPrimaryColor, true);
        subtitleStyle = SimpleGuiTheme.CreateLabelStyle(18, FontStyle.Normal, TextAnchor.UpperLeft,
            SimpleGuiTheme.TextSecondaryColor, true);
        cardTitleStyle = SimpleGuiTheme.CreateLabelStyle(22, FontStyle.Bold, TextAnchor.UpperLeft,
            SimpleGuiTheme.TextPrimaryColor, true);
        cardDescriptionStyle = SimpleGuiTheme.CreateLabelStyle(16, FontStyle.Normal, TextAnchor.UpperLeft,
            SimpleGuiTheme.TextSecondaryColor, true);
        descriptionStyle = SimpleGuiTheme.CreateLabelStyle(18, FontStyle.Normal, TextAnchor.MiddleCenter,
            SimpleGuiTheme.TextSecondaryColor, true);
        readyStateStyle = SimpleGuiTheme.CreateLabelStyle(14, FontStyle.Bold, TextAnchor.UpperRight,
            SimpleGuiTheme.SuccessColor, false);
        missingStateStyle = SimpleGuiTheme.CreateLabelStyle(14, FontStyle.Bold, TextAnchor.UpperRight,
            SimpleGuiTheme.DangerColor, false);

        optionButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            17,
            new Color32(226, 172, 208, 255),
            new Color32(226, 172, 208, 255),
            new Color32(226, 172, 208, 255),
            new Color32(66, 22, 52, 255));

        disabledOptionButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            17,
            new Color(0.21f, 0.24f, 0.28f, 1f),
            new Color(0.21f, 0.24f, 0.28f, 1f),
            new Color(0.19f, 0.22f, 0.26f, 1f),
            new Color(0.64f, 0.69f, 0.76f, 1f));

        backButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            16,
            new Color32(226, 172, 208, 255),
            new Color32(226, 172, 208, 255),
            new Color(0.15f, 0.21f, 0.28f, 1f),
            new Color32(66, 22, 52, 255));
    }

    private void OnValidate()
    {
        panelWidth = Mathf.Max(360f, panelWidth);
        panelHeight = Mathf.Max(260f, panelHeight);
        optionButtonWidth = Mathf.Max(220f, optionButtonWidth);
        optionButtonHeight = Mathf.Max(88f, optionButtonHeight);
    }
}