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
    [SerializeField, Min(140f)] private float popupHeight = 220f;

    private GUIStyle hudBoxStyle;
    private GUIStyle hudLabelStyle;
    private GUIStyle popupBoxStyle;
    private GUIStyle popupTitleStyle;
    private GUIStyle popupBodyStyle;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnGUI()
    {
        ResolveReferences();
        EnsureStyles();
        DrawHud();
        DrawPopup();
    }

    private void DrawHud()
    {
        Rect hudRect = new Rect(screenMargin, screenMargin, hudWidth, hudHeight);
        GUILayout.BeginArea(hudRect, hudBoxStyle);
        GUILayout.Label($"测试关卡 波次: {GetWaveNumber()}", hudLabelStyle);
        GUILayout.Label($"剩余时间: {FormatTime(GetRemainingTime())}", hudLabelStyle);
        GUILayout.Label($"玩家血量: {GetHealthText()}", hudLabelStyle);
        GUILayout.EndArea();
    }

    private void DrawPopup()
    {
        if (levelManager == null || !levelManager.ShowTemporaryTestPopup)
        {
            return;
        }

        Rect popupRect = new Rect(
            (Screen.width - popupWidth) * 0.5f,
            (Screen.height - popupHeight) * 0.5f,
            popupWidth,
            popupHeight);

        GUI.Box(popupRect, GUIContent.none, popupBoxStyle);

        Rect contentRect = new Rect(popupRect.x + 20f, popupRect.y + 20f, popupRect.width - 40f, popupRect.height - 40f);
        GUILayout.BeginArea(contentRect);
        GUILayout.Label(levelManager.TemporaryTestPopupTitle, popupTitleStyle);
        GUILayout.Space(16f);
        GUILayout.Label(levelManager.TemporaryTestPopupMessage, popupBodyStyle);
        GUILayout.EndArea();
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

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainingSeconds:00}";
    }

    private void EnsureStyles()
    {
        if (hudBoxStyle != null)
        {
            return;
        }

        hudBoxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 16,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(12, 12, 12, 12)
        };

        hudLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            normal = { textColor = Color.white }
        };

        popupBoxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(20, 20, 20, 20)
        };

        popupTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = Color.white }
        };

        popupBodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = Color.white }
        };
    }

    private void OnValidate()
    {
        hudWidth = Mathf.Max(180f, hudWidth);
        hudHeight = Mathf.Max(80f, hudHeight);
        screenMargin = Mathf.Max(0f, screenMargin);
        popupWidth = Mathf.Max(240f, popupWidth);
        popupHeight = Mathf.Max(140f, popupHeight);
    }
}