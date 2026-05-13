using UnityEngine;

[DisallowMultipleComponent]
public sealed class LevelTestGui : MonoBehaviour
{
    [Header("Temporary Test HUD")]
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private Player targetPlayer;
    [SerializeField, Min(180f)] private float hudWidth = 280f;
    [SerializeField, Min(80f)] private float hudHeight = 110f;
    [SerializeField, Min(0f)] private float screenMargin = 16f;

    [Header("Temporary Test Popup")]
    [SerializeField, Min(240f)] private float popupWidth = 460f;
    [SerializeField, Min(180f)] private float popupHeight = 280f;
    [SerializeField, Min(120f)] private float popupButtonWidth = 180f;
    [SerializeField, Min(32f)] private float popupButtonHeight = 42f;

    private GUIStyle hudTitleStyle;
    private GUIStyle hudMetaStyle;
    private GUIStyle hudValueStyle;
    private GUIStyle barValueStyle;
    private GUIStyle hudLabelStyle;
    private GUIStyle popupTitleStyle;
    private GUIStyle popupBodyStyle;
    private GUIStyle popupMetaStyle;
    private GUIStyle popupButtonStyle;
    private GUIStyle rewardTitleStyle;
    private GUIStyle rewardSubtitleStyle;
    private GUIStyle rewardCardTitleStyle;
    private GUIStyle rewardCardBodyStyle;
    private GUIStyle rewardStackStyle;
    private GUIStyle rewardButtonStyle;
    private GUIStyle rewardMissingIconStyle;
    private Vector2 rewardScrollPosition;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnGUI()
    {
        ResolveReferences();
        EnsureStyles();
        DrawHud();
        DrawRewardSelection();
        DrawPopup();
    }

    private void DrawHud()
    {
        float cardWidth = Mathf.Min(Mathf.Max(hudWidth, Screen.width * 0.22f), Mathf.Max(hudWidth, SimpleGuiTheme.Scale(360f)));
        float cardHeight = Mathf.Max(hudHeight + SimpleGuiTheme.Scale(48f), SimpleGuiTheme.Scale(164f));
        Rect hudRect = new Rect(screenMargin, screenMargin, cardWidth, cardHeight);

        SimpleGuiTheme.DrawPanel(hudRect, new Color(0.11f, 0.15f, 0.2f, 0.93f));

        Rect contentRect = SimpleGuiTheme.Inset(hudRect, SimpleGuiTheme.Scale(16f), SimpleGuiTheme.Scale(14f));
        float lineHeight = SimpleGuiTheme.Scale(20f);
        float valueHeight = SimpleGuiTheme.Scale(30f);
        float gap = SimpleGuiTheme.Scale(8f);

        GUI.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, lineHeight), "战斗概览", hudTitleStyle);
        GUI.Label(new Rect(contentRect.x, contentRect.y + lineHeight + gap, contentRect.width, lineHeight), "当前波次", hudMetaStyle);
        GUI.Label(new Rect(contentRect.x, contentRect.y + lineHeight + gap + SimpleGuiTheme.Scale(18f), contentRect.width, valueHeight), GetWaveProgressText(), hudValueStyle);

        float timerY = contentRect.y + lineHeight + gap + valueHeight + SimpleGuiTheme.Scale(14f);
        GUI.Label(new Rect(contentRect.x, timerY, contentRect.width, lineHeight), "剩余时间", hudMetaStyle);
        GUI.Label(new Rect(contentRect.x, timerY + SimpleGuiTheme.Scale(18f), contentRect.width, valueHeight), FormatTime(GetRemainingTime()), hudLabelStyle);

        float healthY = contentRect.yMax - SimpleGuiTheme.Scale(48f);
        GUI.Label(new Rect(contentRect.x, healthY - SimpleGuiTheme.Scale(22f), contentRect.width, lineHeight), "玩家生命", hudMetaStyle);
        DrawHealthBar(new Rect(contentRect.x, healthY, contentRect.width, SimpleGuiTheme.Scale(24f)), GetHealthText(), GetNormalizedHealth());
    }

    private void DrawPopup()
    {
        if (levelManager == null || !levelManager.ShowTemporaryTestPopup)
        {
            return;
        }

        SimpleGuiTheme.DrawOverlay(0.66f);

        Rect popupRect = SimpleGuiTheme.CenterRect(0.42f, 0.4f, popupWidth, popupHeight, SimpleGuiTheme.Scale(24f));
        SimpleGuiTheme.DrawPanel(popupRect, SimpleGuiTheme.PanelFillLightColor);

        Rect contentRect = SimpleGuiTheme.Inset(popupRect, SimpleGuiTheme.Scale(24f), SimpleGuiTheme.Scale(22f));
        float buttonWidth = Mathf.Min(popupButtonWidth, contentRect.width);
        float buttonHeight = Mathf.Max(popupButtonHeight, SimpleGuiTheme.Scale(46f));

        GUILayout.BeginArea(contentRect);
        GUILayout.Label(levelManager.TemporaryTestPopupTitle, popupTitleStyle);
        GUILayout.Space(SimpleGuiTheme.Scale(10f));
        GUILayout.Label($"当前波次: {GetPopupCurrentWaveText()}", popupMetaStyle);
        GUILayout.Label($"下一波次: {GetPopupNextWaveText()}", popupMetaStyle);
        GUILayout.Space(SimpleGuiTheme.Scale(14f));
        GUILayout.Label(levelManager.TemporaryTestPopupMessage, popupBodyStyle);
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(levelManager.PopupPrimaryActionLabel, popupButtonStyle, GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
        {
            levelManager.ExecutePopupPrimaryAction();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    private void DrawRewardSelection()
    {
        if (levelManager == null || !levelManager.ShowRewardSelection)
        {
            return;
        }

        SimpleGuiTheme.DrawOverlay(0.72f);

        Rect panelRect = SimpleGuiTheme.CenterRect(0.86f, 0.74f, SimpleGuiTheme.Scale(920f), SimpleGuiTheme.Scale(520f), SimpleGuiTheme.Scale(24f));
        SimpleGuiTheme.DrawPanel(panelRect, SimpleGuiTheme.PanelFillLightColor);

        Rect contentRect = SimpleGuiTheme.Inset(panelRect, SimpleGuiTheme.Scale(28f), SimpleGuiTheme.Scale(26f));
        Rect headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, SimpleGuiTheme.Scale(88f));
        GUI.Label(new Rect(headerRect.x, headerRect.y, headerRect.width, SimpleGuiTheme.Scale(32f)), "波次完成", rewardTitleStyle);
        GUI.Label(new Rect(headerRect.x, headerRect.y + SimpleGuiTheme.Scale(36f), headerRect.width, SimpleGuiTheme.Scale(22f)), $"当前波次: {GetPopupCurrentWaveText()}    下一波次: {GetPopupNextWaveText()}", rewardSubtitleStyle);
        GUI.Label(new Rect(headerRect.x, headerRect.y + SimpleGuiTheme.Scale(58f), headerRect.width, SimpleGuiTheme.Scale(22f)), "选择一个奖励后将直接进入下一波。", rewardSubtitleStyle);

        Rect cardsRect = new Rect(contentRect.x, headerRect.yMax + SimpleGuiTheme.Scale(16f), contentRect.width, contentRect.height - headerRect.height - SimpleGuiTheme.Scale(16f));
        DrawRewardCards(cardsRect);
    }

    private void DrawRewardCards(Rect rect)
    {
        int rewardCount = levelManager != null ? levelManager.RewardOfferCount : 0;
        if (rewardCount <= 0)
        {
            return;
        }

        float gap = SimpleGuiTheme.Scale(18f);
        int columnCount = rect.width >= SimpleGuiTheme.Scale(980f) ? 3 : rect.width >= SimpleGuiTheme.Scale(660f) ? 2 : 1;
        int rowCount = Mathf.CeilToInt(rewardCount / (float)columnCount);
        float preferredCardHeight = columnCount == 1 ? SimpleGuiTheme.Scale(210f) : SimpleGuiTheme.Scale(280f);
        float contentHeight = rowCount * preferredCardHeight + Mathf.Max(0, rowCount - 1) * gap;
        bool needsScroll = contentHeight > rect.height;
        float viewportWidth = rect.width - (needsScroll ? SimpleGuiTheme.Scale(18f) : 0f);
        float cardWidth = (viewportWidth - gap * (columnCount - 1)) / columnCount;

        if (!needsScroll && rowCount > 0)
        {
            float extraHeight = Mathf.Max(0f, rect.height - contentHeight);
            preferredCardHeight += extraHeight / rowCount;
            contentHeight = rect.height;
        }

        Rect viewRect = new Rect(0f, 0f, viewportWidth, contentHeight);
        rewardScrollPosition = GUI.BeginScrollView(rect, rewardScrollPosition, viewRect, false, needsScroll);

        for (int index = 0; index < rewardCount; index++)
        {
            if (!levelManager.TryGetRewardOffer(index, out RewardType rewardType))
            {
                continue;
            }

            int row = index / columnCount;
            int column = index % columnCount;
            Rect cardRect = new Rect(
                column * (cardWidth + gap),
                row * (preferredCardHeight + gap),
                cardWidth,
                preferredCardHeight);

            DrawRewardCard(cardRect, rewardType);
        }

        GUI.EndScrollView();
    }

    private void DrawRewardCard(Rect cardRect, RewardType rewardType)
    {
        SimpleGuiTheme.DrawPanel(cardRect, new Color(0.1f, 0.14f, 0.18f, 0.96f));

        Rect contentRect = SimpleGuiTheme.Inset(cardRect, SimpleGuiTheme.Scale(18f), SimpleGuiTheme.Scale(16f));
        float iconSize = Mathf.Min(Mathf.Min(contentRect.width * 0.42f, contentRect.height * 0.32f), SimpleGuiTheme.Scale(120f));
        float buttonHeight = Mathf.Max(SimpleGuiTheme.Scale(44f), popupButtonHeight);
        Rect iconRect = new Rect(contentRect.center.x - iconSize * 0.5f, contentRect.y, iconSize, iconSize);
        Rect titleRect = new Rect(contentRect.x, iconRect.yMax + SimpleGuiTheme.Scale(10f), contentRect.width, SimpleGuiTheme.Scale(26f));
        Rect stackRect = new Rect(contentRect.x, titleRect.yMax + SimpleGuiTheme.Scale(4f), contentRect.width, SimpleGuiTheme.Scale(18f));
        Rect buttonRect = new Rect(contentRect.x, contentRect.yMax - buttonHeight, contentRect.width, buttonHeight);
        Rect bodyRect = new Rect(contentRect.x, stackRect.yMax + SimpleGuiTheme.Scale(10f), contentRect.width, buttonRect.y - stackRect.yMax - SimpleGuiTheme.Scale(18f));

        DrawRewardIcon(iconRect, rewardType);
        GUI.Label(titleRect, RewardSelectionSession.GetDisplayName(rewardType), rewardCardTitleStyle);
        GUI.Label(stackRect, RewardSelectionSession.GetStackSummary(rewardType), rewardStackStyle);
        GUI.Label(bodyRect, RewardSelectionSession.GetDescription(rewardType), rewardCardBodyStyle);

        if (GUI.Button(buttonRect, "选择这个奖励", rewardButtonStyle))
        {
            levelManager.SelectReward(rewardType);
        }
    }

    private void DrawRewardIcon(Rect rect, RewardType rewardType)
    {
        Sprite icon = RewardSelectionSession.GetIcon(rewardType);
        if (icon == null)
        {
            SimpleGuiTheme.DrawSolidRect(rect, new Color(1f, 1f, 1f, 0.05f));
            GUI.Label(rect, RewardSelectionSession.GetDisplayName(rewardType), rewardMissingIconStyle);
            return;
        }

        SimpleGuiTheme.DrawSprite(icon, rect, Color.white);
    }

    private void DrawHealthBar(Rect rect, string valueText, float normalizedValue)
    {
        SimpleGuiTheme.DrawSolidRect(rect, new Color(1f, 1f, 1f, 0.08f));

        Rect innerRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
        SimpleGuiTheme.DrawSolidRect(innerRect, new Color(0.08f, 0.11f, 0.14f, 0.95f));

        float fillWidth = Mathf.Max(0f, innerRect.width * normalizedValue);
        if (fillWidth > 0f)
        {
            Color fillColor = Color.Lerp(SimpleGuiTheme.DangerColor, SimpleGuiTheme.SuccessColor, normalizedValue);
            SimpleGuiTheme.DrawSolidRect(new Rect(innerRect.x, innerRect.y, fillWidth, innerRect.height), fillColor);
        }

        GUI.Label(rect, valueText, barValueStyle);
    }

    private void ResolveReferences()
    {
        if (levelManager == null)
        {
            levelManager = FindFirstObjectByType<LevelManager>();
        }

        if (targetPlayer == null)
        {
            targetPlayer = levelManager != null && levelManager.TargetPlayer != null
                ? levelManager.TargetPlayer
                : FindFirstObjectByType<Player>();
        }
    }

    private int GetWaveNumber()
    {
        return levelManager != null ? levelManager.CurrentWaveNumber : 1;
    }

    private string GetWaveProgressText()
    {
        if (levelManager == null || levelManager.TotalWaveCount <= 0)
        {
            return "-- / --";
        }

        return $"{levelManager.CurrentWaveNumber} / {levelManager.TotalWaveCount}";
    }

    private float GetRemainingTime()
    {
        return levelManager != null ? levelManager.RemainingTimeSeconds : 0f;
    }

    private string GetHealthText()
    {
        if (targetPlayer == null)
        {
            return "-- / --";
        }

        return $"{Mathf.CeilToInt(targetPlayer.CurrentHealth)} / {Mathf.CeilToInt(targetPlayer.MaxHealth)}";
    }

    private float GetNormalizedHealth()
    {
        if (targetPlayer == null || targetPlayer.MaxHealth <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(targetPlayer.CurrentHealth / targetPlayer.MaxHealth);
    }

    private string GetPopupCurrentWaveText()
    {
        if (levelManager == null || levelManager.PopupCurrentWaveNumber <= 0)
        {
            return "--";
        }

        return $"{levelManager.PopupCurrentWaveNumber} / {levelManager.TotalWaveCount}";
    }

    private string GetPopupNextWaveText()
    {
        if (levelManager == null)
        {
            return "--";
        }

        if (levelManager.PopupNextWaveNumber <= 0)
        {
            return "无";
        }

        return $"{levelManager.PopupNextWaveNumber} / {levelManager.TotalWaveCount}";
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainingSeconds:00}";
    }

    private void EnsureStyles()
    {
        if (hudTitleStyle != null)
        {
            return;
        }

        hudTitleStyle = SimpleGuiTheme.CreateLabelStyle(18, FontStyle.Bold, TextAnchor.UpperLeft, SimpleGuiTheme.TextPrimaryColor, false);
        hudMetaStyle = SimpleGuiTheme.CreateLabelStyle(14, FontStyle.Bold, TextAnchor.UpperLeft, SimpleGuiTheme.TextSecondaryColor, false);
        hudValueStyle = SimpleGuiTheme.CreateLabelStyle(24, FontStyle.Bold, TextAnchor.UpperLeft, SimpleGuiTheme.AccentColor, false);
        hudLabelStyle = SimpleGuiTheme.CreateLabelStyle(20, FontStyle.Bold, TextAnchor.UpperLeft, SimpleGuiTheme.TextPrimaryColor, false);
        barValueStyle = SimpleGuiTheme.CreateLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleCenter, SimpleGuiTheme.TextPrimaryColor, false);

        popupTitleStyle = SimpleGuiTheme.CreateLabelStyle(26, FontStyle.Bold, TextAnchor.UpperCenter, SimpleGuiTheme.TextPrimaryColor, true);
        popupBodyStyle = SimpleGuiTheme.CreateLabelStyle(18, FontStyle.Normal, TextAnchor.UpperLeft, SimpleGuiTheme.TextSecondaryColor, true);
        popupMetaStyle = SimpleGuiTheme.CreateLabelStyle(17, FontStyle.Bold, TextAnchor.UpperLeft, SimpleGuiTheme.AccentMutedColor, false);
        popupButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            18,
            new Color(0.92f, 0.75f, 0.32f, 1f),
            new Color(0.97f, 0.81f, 0.4f, 1f),
            new Color(0.84f, 0.65f, 0.24f, 1f),
            new Color(0.08f, 0.1f, 0.12f, 1f));

        rewardTitleStyle = SimpleGuiTheme.CreateLabelStyle(28, FontStyle.Bold, TextAnchor.UpperCenter, SimpleGuiTheme.TextPrimaryColor, true);
        rewardSubtitleStyle = SimpleGuiTheme.CreateLabelStyle(16, FontStyle.Normal, TextAnchor.UpperCenter, SimpleGuiTheme.TextSecondaryColor, true);
        rewardCardTitleStyle = SimpleGuiTheme.CreateLabelStyle(22, FontStyle.Bold, TextAnchor.UpperCenter, SimpleGuiTheme.TextPrimaryColor, true);
        rewardCardBodyStyle = SimpleGuiTheme.CreateLabelStyle(16, FontStyle.Normal, TextAnchor.UpperCenter, SimpleGuiTheme.TextSecondaryColor, true);
        rewardStackStyle = SimpleGuiTheme.CreateLabelStyle(14, FontStyle.Bold, TextAnchor.UpperCenter, SimpleGuiTheme.AccentColor, false);
        rewardButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            17,
            new Color(0.92f, 0.75f, 0.32f, 1f),
            new Color(0.97f, 0.81f, 0.4f, 1f),
            new Color(0.84f, 0.65f, 0.24f, 1f),
            new Color(0.08f, 0.1f, 0.12f, 1f));
        rewardMissingIconStyle = SimpleGuiTheme.CreateLabelStyle(13, FontStyle.Bold, TextAnchor.MiddleCenter, SimpleGuiTheme.TextSecondaryColor, true);
    }

    private void OnValidate()
    {
        hudWidth = Mathf.Max(180f, hudWidth);
        hudHeight = Mathf.Max(80f, hudHeight);
        screenMargin = Mathf.Max(0f, screenMargin);
        popupWidth = Mathf.Max(240f, popupWidth);
        popupHeight = Mathf.Max(180f, popupHeight);
        popupButtonWidth = Mathf.Max(120f, popupButtonWidth);
        popupButtonHeight = Mathf.Max(32f, popupButtonHeight);
    }
}