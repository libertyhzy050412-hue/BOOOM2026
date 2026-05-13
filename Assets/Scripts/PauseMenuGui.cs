#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PauseMenuGui : MonoBehaviour
{
    [SerializeField] private LevelManager levelManager;
    [SerializeField, Min(80f)] private float settingsButtonWidth = 92f;
    [SerializeField, Min(32f)] private float settingsButtonHeight = 38f;
    [SerializeField, Min(360f)] private float menuWidth = 440f;
    [SerializeField, Min(300f)] private float menuHeight = 420f;
    [SerializeField, Min(0f)] private float screenMargin = 16f;
    [SerializeField] private string settingsButtonText = "设置";
    [SerializeField] private string resumeButtonText = "继续游戏";
    [SerializeField] private string returnButtonText = "返回开始界面";
    [SerializeField] private bool allowEscapeShortcut = true;

    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle sectionTitleStyle;
    private GUIStyle settingLabelStyle;
    private GUIStyle settingValueStyle;
    private GUIStyle warningStyle;
    private GUIStyle settingsButtonStyle;
    private GUIStyle buttonStyle;
    private GUIStyle returnButtonStyle;
    private GUIStyle hintStyle;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        if (!allowEscapeShortcut || levelManager == null || levelManager.ShowTemporaryTestPopup)
        {
            return;
        }

        if (!IsEscapePressedThisFrame())
        {
            return;
        }

        levelManager.TogglePauseMenu();
    }

    private void OnGUI()
    {
        ResolveReferences();
        if (levelManager == null)
        {
            return;
        }

        EnsureStyles();
        DrawSettingsButton();
        DrawPauseMenu();
    }

    private void DrawSettingsButton()
    {
        if (levelManager.PauseMenuOpen || levelManager.ShowTemporaryTestPopup || levelManager.ShowRewardSelection)
        {
            return;
        }

        Rect buttonRect = new Rect(
            Screen.width - screenMargin - settingsButtonWidth,
            screenMargin,
            settingsButtonWidth,
            settingsButtonHeight);

        if (GuiAudioButton.Button("PauseMenu/Settings", buttonRect, settingsButtonText, settingsButtonStyle))
        {
            levelManager.OpenPauseMenu();
        }
    }

    private void DrawPauseMenu()
    {
        if (!levelManager.PauseMenuOpen)
        {
            return;
        }

        SimpleGuiTheme.DrawOverlay(0.66f);

        Rect panelRect = SimpleGuiTheme.CenterRect(0.34f, 0.36f, menuWidth, menuHeight, SimpleGuiTheme.Scale(24f));
        SimpleGuiTheme.DrawPanel(panelRect, SimpleGuiTheme.PanelFillLightColor);

        Rect contentRect = SimpleGuiTheme.Inset(panelRect, SimpleGuiTheme.Scale(24f), SimpleGuiTheme.Scale(22f));
        float buttonHeight = Mathf.Max(SimpleGuiTheme.Scale(44f), 42f);

        GUILayout.BeginArea(contentRect);
        GUILayout.Label("游戏设置", titleStyle);
        GUILayout.Space(SimpleGuiTheme.Scale(8f));
        GUILayout.Label("可以在这里直接调整音乐和音效音量。", bodyStyle);
        GUILayout.Space(SimpleGuiTheme.Scale(16f));

        DrawAudioSettings();

        GUILayout.FlexibleSpace();

        if (GuiAudioButton.LayoutButton("PauseMenu/Resume", resumeButtonText, buttonStyle, GUILayout.Height(buttonHeight)))
        {
            levelManager.ClosePauseMenu();
        }

        GUILayout.Space(SimpleGuiTheme.Scale(14f));

        if (GuiAudioButton.LayoutButton("PauseMenu/Return", returnButtonText, returnButtonStyle, GUILayout.Height(buttonHeight)))
        {
            levelManager.ReturnToStartMenu();
        }

        GUILayout.Space(SimpleGuiTheme.Scale(10f));
        GUILayout.Label("按 Esc 也可以继续游戏", hintStyle);

        GUILayout.EndArea();
    }

    private void DrawAudioSettings()
    {
        GUILayout.Label("音量设置", sectionTitleStyle);
        GUILayout.Space(SimpleGuiTheme.Scale(6f));

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            GUILayout.Label("当前场景未找到 AudioManager，无法调整音量。", warningStyle);
            return;
        }

        float musicVolume = AudioManager.GetMusicVolume();
        float newMusicVolume = DrawVolumeSlider("背景音乐", musicVolume);
        if (!Mathf.Approximately(newMusicVolume, musicVolume))
        {
            AudioManager.SetMusicVolume(newMusicVolume);
        }

        float uiVolume = AudioManager.GetUiSoundVolume();
        float newUiVolume = DrawVolumeSlider("界面音效", uiVolume);
        if (!Mathf.Approximately(newUiVolume, uiVolume))
        {
            AudioManager.SetUiSoundVolume(newUiVolume);
        }

        float combatVolume = AudioManager.GetCombatSoundVolume();
        float newCombatVolume = DrawVolumeSlider("战斗音效", combatVolume);
        if (!Mathf.Approximately(newCombatVolume, combatVolume))
        {
            AudioManager.SetCombatSoundVolume(newCombatVolume);
        }

        float loopVolume = AudioManager.GetLoopSoundVolume();
        float newLoopVolume = DrawVolumeSlider("循环音效", loopVolume);
        if (!Mathf.Approximately(newLoopVolume, loopVolume))
        {
            AudioManager.SetLoopSoundVolume(newLoopVolume);
        }
    }

    private float DrawVolumeSlider(string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, settingLabelStyle, GUILayout.Width(SimpleGuiTheme.Scale(96f)));
        float newValue = GUILayout.HorizontalSlider(value, 0f, 1f, GUILayout.ExpandWidth(true));
        GUILayout.Label($"{Mathf.RoundToInt(newValue * 100f)}%", settingValueStyle, GUILayout.Width(SimpleGuiTheme.Scale(54f)));
        GUILayout.EndHorizontal();
        GUILayout.Space(SimpleGuiTheme.Scale(6f));
        return newValue;
    }

    private void ResolveReferences()
    {
        if (levelManager == null)
        {
            levelManager = FindFirstObjectByType<LevelManager>();
        }
    }

    private bool IsEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = SimpleGuiTheme.CreateLabelStyle(26, FontStyle.Bold, TextAnchor.UpperCenter, SimpleGuiTheme.TextPrimaryColor, true);
        bodyStyle = SimpleGuiTheme.CreateLabelStyle(16, FontStyle.Normal, TextAnchor.UpperCenter, SimpleGuiTheme.TextSecondaryColor, true);
        sectionTitleStyle = SimpleGuiTheme.CreateLabelStyle(17, FontStyle.Bold, TextAnchor.UpperLeft, SimpleGuiTheme.TextPrimaryColor, false);
        settingLabelStyle = SimpleGuiTheme.CreateLabelStyle(15, FontStyle.Bold, TextAnchor.MiddleLeft, SimpleGuiTheme.TextSecondaryColor, false);
        settingValueStyle = SimpleGuiTheme.CreateLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleRight, SimpleGuiTheme.AccentColor, false);
        warningStyle = SimpleGuiTheme.CreateLabelStyle(14, FontStyle.Normal, TextAnchor.UpperLeft, SimpleGuiTheme.DangerColor, true);
        hintStyle = SimpleGuiTheme.CreateLabelStyle(14, FontStyle.Normal, TextAnchor.UpperCenter, SimpleGuiTheme.AccentMutedColor, false);

        settingsButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            16,
            new Color(0.2f, 0.27f, 0.34f, 1f),
            new Color(0.25f, 0.33f, 0.41f, 1f),
            new Color(0.15f, 0.21f, 0.28f, 1f),
            SimpleGuiTheme.TextPrimaryColor,
            TextAnchor.MiddleCenter,
            12,
            8,
            false);

        buttonStyle = SimpleGuiTheme.CreateButtonStyle(
            18,
            new Color(0.92f, 0.75f, 0.32f, 1f),
            new Color(0.97f, 0.81f, 0.4f, 1f),
            new Color(0.84f, 0.65f, 0.24f, 1f),
            new Color(0.08f, 0.1f, 0.12f, 1f));

        returnButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            18,
            new Color(0.28f, 0.2f, 0.2f, 1f),
            new Color(0.34f, 0.24f, 0.24f, 1f),
            new Color(0.22f, 0.16f, 0.16f, 1f),
            SimpleGuiTheme.TextPrimaryColor);
    }

    private void OnValidate()
    {
        settingsButtonWidth = Mathf.Max(80f, settingsButtonWidth);
        settingsButtonHeight = Mathf.Max(32f, settingsButtonHeight);
        menuWidth = Mathf.Max(360f, menuWidth);
        menuHeight = Mathf.Max(300f, menuHeight);
        screenMargin = Mathf.Max(0f, screenMargin);
    }
}