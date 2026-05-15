using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OpenMenuController : MonoBehaviour
{
    private const string ContinueScenePreferenceKey = "OpenMenuController.ContinueScene";
    private const string DefaultMenuSceneName = "OpenMenu";
    private static bool sceneTrackerRegistered;
    private static string runtimeContinueSceneName = string.Empty;

    [Header("Scene Routing")]
    [SerializeField] private string menuSceneName = DefaultMenuSceneName;
    [SerializeField] private string weaponSelectionSceneName = "WeaponSelectionScene";
    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private bool allowCrossLaunchContinue;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private bool autoBindButtonClicks = true;
    [SerializeField, Min(0f)] private float initialButtonInputDelay = 0.2f;

    [Header("New Run")]
    [SerializeField] private bool clearWeaponSelectionOnStart = true;
    [SerializeField] private bool clearRewardSelectionOnStart = true;
    [SerializeField] private bool resetTrackedContinueSceneOnStart = true;

    private bool listenersBound;
    private float buttonInputUnlockTime;
    private bool waitingForButtonUnlock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneTracker()
    {
        if (sceneTrackerRegistered)
        {
            return;
        }

        runtimeContinueSceneName = string.Empty;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        sceneTrackerRegistered = true;
    }

    private void Awake()
    {
        BindButtonCallbacks();
        BeginInitialButtonInputDelay();
        RefreshContinueButtonState();
    }

    private void OnEnable()
    {
        AudioManager.PlayMenuMusic();
        BeginInitialButtonInputDelay();
        RefreshContinueButtonState();
    }

    private void Update()
    {
        if (!waitingForButtonUnlock)
        {
            return;
        }

        if (Time.unscaledTime < buttonInputUnlockTime)
        {
            return;
        }

        waitingForButtonUnlock = false;
        RefreshContinueButtonState();
    }

    private void OnDestroy()
    {
        UnbindButtonCallbacks();
    }

    public void StartGame()
    {
        if (!AreMenuButtonsReady())
        {
            return;
        }

        AudioManager.PlayUiClick();

        if (clearWeaponSelectionOnStart)
        {
            WeaponSelectionSession.ClearSelection();
        }

        if (clearRewardSelectionOnStart)
        {
            RewardSelectionSession.ClearRewards();
        }

        if (resetTrackedContinueSceneOnStart)
        {
            ClearTrackedContinueScene();
        }

        string sceneToLoad = ResolveNewGameScene();
        if (string.IsNullOrWhiteSpace(sceneToLoad))
        {
            Debug.LogWarning("[OpenMenuController] 未配置可加载的新游戏场景。请检查 Build Profiles 和 Inspector 场景名。", this);
            RefreshContinueButtonState();
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneToLoad);
    }

    public void ContinueGame()
    {
        if (!AreMenuButtonsReady())
        {
            return;
        }

        AudioManager.PlayUiClick();

        string sceneToLoad = ResolveContinueScene();
        if (string.IsNullOrWhiteSpace(sceneToLoad))
        {
            Debug.LogWarning("[OpenMenuController] 当前没有可继续的场景记录。", this);
            RefreshContinueButtonState();
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneToLoad);
    }

    public void QuitGame()
    {
        if (!AreMenuButtonsReady())
        {
            return;
        }

        AudioManager.PlayUiClick();
        Time.timeScale = 1f;
        Application.Quit();

        if (Application.isEditor)
        {
            Debug.Log("[OpenMenuController] 编辑器内调用了退出游戏，运行版本中会真正退出。", this);
        }
    }

    public void RefreshContinueButtonState()
    {
        bool buttonsReady = AreMenuButtonsReady();

        if (startButton != null)
        {
            startButton.interactable = buttonsReady;
        }

        if (continueButton != null)
        {
            continueButton.interactable = buttonsReady && !string.IsNullOrWhiteSpace(ResolveContinueScene());
        }

        if (quitButton != null)
        {
            quitButton.interactable = buttonsReady;
        }
    }

    private void BeginInitialButtonInputDelay()
    {
        if (initialButtonInputDelay <= 0f)
        {
            waitingForButtonUnlock = false;
            return;
        }

        waitingForButtonUnlock = true;
        buttonInputUnlockTime = Time.unscaledTime + initialButtonInputDelay;

        EventSystem currentEventSystem = EventSystem.current;
        if (currentEventSystem != null)
        {
            currentEventSystem.SetSelectedGameObject(null);
        }
    }

    private bool AreMenuButtonsReady()
    {
        return !waitingForButtonUnlock || Time.unscaledTime >= buttonInputUnlockTime;
    }

    private string ResolveNewGameScene()
    {
        if (CanLoadScene(weaponSelectionSceneName))
        {
            return weaponSelectionSceneName;
        }

        if (!string.IsNullOrWhiteSpace(weaponSelectionSceneName))
        {
            Debug.LogWarning($"[OpenMenuController] 场景 '{weaponSelectionSceneName}' 当前不可通过 SceneManager.LoadScene 加载，已回退到主游戏场景。", this);
        }

        if (CanLoadScene(mainSceneName))
        {
            return mainSceneName;
        }

        return string.Empty;
    }

    private string ResolveContinueScene()
    {
        if (CanUseContinueScene(runtimeContinueSceneName))
        {
            return runtimeContinueSceneName;
        }

        if (!allowCrossLaunchContinue)
        {
            return string.Empty;
        }

        string savedSceneName = PlayerPrefs.GetString(ContinueScenePreferenceKey, string.Empty);
        if (CanUseContinueScene(savedSceneName))
        {
            return savedSceneName;
        }

        if (!string.IsNullOrWhiteSpace(savedSceneName))
        {
            ClearSavedContinueScene();
        }

        return string.Empty;
    }

    private bool CanUseContinueScene(string sceneName)
    {
        if (!CanLoadScene(sceneName))
        {
            return false;
        }

        return !IsConfiguredMenuScene(sceneName);
    }

    private void BindButtonCallbacks()
    {
        if (!autoBindButtonClicks || listenersBound)
        {
            return;
        }

        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGame);
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(ContinueGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
        }

        listenersBound = true;
    }

    private void UnbindButtonCallbacks()
    {
        if (!listenersBound)
        {
            return;
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
        }

        listenersBound = false;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string loadedSceneName = scene.name;
        if (!CanLoadScene(loadedSceneName) || IsTrackedMenuScene(loadedSceneName))
        {
            return;
        }

        runtimeContinueSceneName = loadedSceneName;
        PlayerPrefs.SetString(ContinueScenePreferenceKey, loadedSceneName);
        PlayerPrefs.Save();
    }

    private static void ClearTrackedContinueScene()
    {
        runtimeContinueSceneName = string.Empty;
        ClearSavedContinueScene();
    }

    private static void ClearSavedContinueScene()
    {
        PlayerPrefs.DeleteKey(ContinueScenePreferenceKey);
        PlayerPrefs.Save();
    }

    private bool IsConfiguredMenuScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return true;
        }

        if (string.Equals(sceneName, menuSceneName, System.StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool IsTrackedMenuScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return true;
        }

        return string.Equals(sceneName, DefaultMenuSceneName, System.StringComparison.Ordinal);
    }

    private static bool CanLoadScene(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }
}