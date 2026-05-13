using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class StartMenuGui : MonoBehaviour
{
    [SerializeField] private string gameTitle = "MCP Test";
    [SerializeField, TextArea(2, 3)] private string subtitle = "选择武器并进入当前测试流程";
    [SerializeField] private string weaponSelectionSceneName = "WeaponSelectionScene";
    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField, Min(280f)] private float panelWidth = 420f;
    [SerializeField, Min(220f)] private float panelHeight = 320f;
    [SerializeField, Min(160f)] private float buttonWidth = 220f;
    [SerializeField, Min(40f)] private float buttonHeight = 48f;
    [SerializeField] private string startButtonText = "开始游戏";
    [SerializeField] private string quitButtonText = "退出游戏";

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle hintStyle;
    private GUIStyle primaryButtonStyle;
    private GUIStyle secondaryButtonStyle;

    private void Awake()
    {
        WeaponSelectionSession.ClearSelection();
        RewardSelectionSession.ClearRewards();
    }

    private void OnEnable()
    {
        AudioManager.PlayMenuMusic();
    }

    private void OnGUI()
    {
        EnsureStyles();

        SimpleGuiTheme.DrawBackdrop();

        float margin = SimpleGuiTheme.Scale(24f);
        Rect panelRect = SimpleGuiTheme.CenterRect(0.9f, 0.84f, panelWidth, panelHeight, margin);
        SimpleGuiTheme.DrawPanel(panelRect, SimpleGuiTheme.PanelFillLightColor);

        Rect contentRect = SimpleGuiTheme.Inset(panelRect, SimpleGuiTheme.Scale(42f), SimpleGuiTheme.Scale(36f));
        float actionWidth = Mathf.Min(Mathf.Max(buttonWidth, panelRect.width * 0.32f), contentRect.width);
        float actionHeight = Mathf.Max(buttonHeight, SimpleGuiTheme.Scale(56f));

        GUILayout.BeginArea(contentRect);
        GUILayout.Space(SimpleGuiTheme.Scale(16f));
        GUILayout.Label(gameTitle, titleStyle);
        GUILayout.Space(SimpleGuiTheme.Scale(12f));
        GUILayout.Label(subtitle, subtitleStyle);
        GUILayout.FlexibleSpace();

        GUILayout.Label(GetStartHintText(), hintStyle);
        GUILayout.Space(SimpleGuiTheme.Scale(20f));

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();

        if (GuiAudioButton.LayoutButton("StartMenu/Start", startButtonText, primaryButtonStyle, GUILayout.Width(actionWidth), GUILayout.Height(actionHeight)))
        {
            StartGame();
        }

        GUILayout.Space(SimpleGuiTheme.Scale(16f));

        if (GuiAudioButton.LayoutButton("StartMenu/Quit", quitButtonText, secondaryButtonStyle, GUILayout.Width(actionWidth), GUILayout.Height(actionHeight)))
        {
            QuitGame();
        }

        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
    }

    public void StartGame()
    {
        WeaponSelectionSession.ClearSelection();

        string sceneToLoad = ResolveStartSceneToLoad();

        if (string.IsNullOrWhiteSpace(sceneToLoad))
        {
            Debug.LogWarning("[StartMenuGui] 未配置武器选择场景或主游戏场景，无法开始游戏。", this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneToLoad);
    }

    private string ResolveStartSceneToLoad()
    {
        if (CanLoadScene(weaponSelectionSceneName))
        {
            return weaponSelectionSceneName;
        }

        if (!string.IsNullOrWhiteSpace(weaponSelectionSceneName))
        {
            Debug.LogWarning($"[StartMenuGui] 场景 '{weaponSelectionSceneName}' 存在于工程中但当前不可通过 SceneManager.LoadScene 加载。请把它加入 File -> Build Profiles。已回退到主游戏场景。", this);
        }

        if (CanLoadScene(mainSceneName))
        {
            return mainSceneName;
        }

        return string.Empty;
    }

    private static bool CanLoadScene(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();

        if (Application.isEditor)
        {
            Debug.Log("[StartMenuGui] 编辑器内调用了退出游戏，运行版本中会真正退出。", this);
        }
    }

    private string GetStartHintText()
    {
        if (CanLoadScene(weaponSelectionSceneName))
        {
            return "开始后会先进入武器选择界面，再进入主场景。";
        }

        if (CanLoadScene(mainSceneName))
        {
            return "武器选择场景当前不可加载，本次会直接进入主场景。";
        }

        return "当前没有可加载的战斗场景，请先检查 Build Profiles。";
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = SimpleGuiTheme.CreateLabelStyle(42, FontStyle.Bold, TextAnchor.MiddleCenter, SimpleGuiTheme.TextPrimaryColor, true);
        subtitleStyle = SimpleGuiTheme.CreateLabelStyle(20, FontStyle.Normal, TextAnchor.MiddleCenter, SimpleGuiTheme.TextSecondaryColor, true);
        hintStyle = SimpleGuiTheme.CreateLabelStyle(18, FontStyle.Normal, TextAnchor.MiddleCenter, SimpleGuiTheme.AccentMutedColor, true);

        primaryButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            20,
            new Color(0.92f, 0.75f, 0.32f, 1f),
            new Color(0.97f, 0.81f, 0.4f, 1f),
            new Color(0.84f, 0.65f, 0.24f, 1f),
            new Color(0.08f, 0.1f, 0.12f, 1f));

        secondaryButtonStyle = SimpleGuiTheme.CreateButtonStyle(
            20,
            new Color(0.2f, 0.27f, 0.34f, 1f),
            new Color(0.25f, 0.33f, 0.41f, 1f),
            new Color(0.15f, 0.21f, 0.28f, 1f),
            SimpleGuiTheme.TextPrimaryColor);
    }

    private void OnValidate()
    {
        panelWidth = Mathf.Max(280f, panelWidth);
        panelHeight = Mathf.Max(220f, panelHeight);
        buttonWidth = Mathf.Max(160f, buttonWidth);
        buttonHeight = Mathf.Max(40f, buttonHeight);
    }
}