#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LevelManager : MonoBehaviour
{
    private const int NoPendingWaveIndex = -1;

    [System.Serializable]
    private sealed class WaveDefinition
    {
        public string name = "Wave";
        [Min(1f)] public float durationSeconds = 60f;
        [Min(0.01f)] public float enemyHealthMultiplier = 1f;
        [Min(0.01f)] public float enemyDamageMultiplier = 1f;
        [Min(0.01f)] public float enemyMoveSpeedMultiplier = 1f;
        [Min(0.01f)] public float spawnRateMultiplier = 1f;
    }

    private enum PopupAction
    {
        None = 0,
        NextWave = 1,
        RestartLevel = 2,
        ReturnToStartScene = 3,
        LoadVictoryScene = 4,
        LoadFailureScene = 5
    }

    public enum LevelState
    {
        Idle = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3
    }

    [Header("Level Setup")]
    [SerializeField] private GameInitializer gameInitializer;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private Player targetPlayer;
    [SerializeField] private bool autoStartOnPlay = true;
    [SerializeField] private bool initializeGameOnStart = true;
    [SerializeField] private string startMenuSceneName = "StartScene";

    [Header("Result Scene Routing")]
    [SerializeField, InspectorName("胜利后跳转场景"), Tooltip("最后一波胜利后按确认键要进入的场景名称。需要已加入 Build Profiles。")]
    private string victoryResultSceneName = "StartScene";
    [SerializeField, InspectorName("失败后跳转场景"), Tooltip("玩家失败后按确认键要进入的场景名称。需要已加入 Build Profiles。")]
    private string failureResultSceneName = "StartScene";

    [Header("Wave Settings")]
    [SerializeField] private List<WaveDefinition> waveDefinitions = CreateDefaultWaveDefinitions();
    [SerializeField] private bool resetSpawnerStateOnStart = true;
    [SerializeField] private bool resetSpawnerStateEachWave = true;
    [SerializeField] private bool enableEnemySpawnerOnStart = true;

    [Header("Wave End")]
    [SerializeField] private bool stopEnemySpawnerOnSuccess = true;
    [SerializeField] private bool stopEnemySpawnerOnFailure = true;
    [SerializeField] private bool disableRemainingEnemiesOnSuccess;
    [SerializeField] private bool disableRemainingEnemiesOnFailure;

    [Header("Temporary Test Flow")]
    [SerializeField] private bool pauseTimeScaleOnWaveEnd = true;
    [SerializeField] private bool allowSpaceToRestartCurrentTestLevel = true;
    [SerializeField] private KeyCode continueKey = KeyCode.Space;
    [SerializeField, TextArea(2, 4)] private string temporaryTestSuccessMessage = "当前波次结束。\n按空格进入下一波。";
    [SerializeField, TextArea(2, 4)] private string finalWaveSuccessMessage = "全部波次完成。\n按空格进入胜利结算场景。";
    [SerializeField, TextArea(2, 4)] private string temporaryTestFailureMessage = "游戏失败。\n按空格进入失败结算场景。";

    private LevelState currentState = LevelState.Idle;
    private static int pendingStartWaveIndex = NoPendingWaveIndex;
    private PopupAction popupAction;
    private int currentWaveIndex;
    private float remainingTimeSeconds;
    private bool showTemporaryTestPopup;
    private bool showRewardSelection;
    private string temporaryTestPopupTitle = string.Empty;
    private string temporaryTestPopupMessage = string.Empty;
    private int popupCurrentWaveNumber;
    private int popupNextWaveNumber;
    private bool pauseMenuOpen;
    private readonly List<RewardType> activeRewardOffers = new List<RewardType>();

    public LevelState CurrentState => currentState;
    public float RemainingTimeSeconds => remainingTimeSeconds;
    public float NormalizedRemainingTime => GetCurrentWaveDuration() <= 0f ? 0f : Mathf.Clamp01(remainingTimeSeconds / GetCurrentWaveDuration());
    public bool IsRunning => currentState == LevelState.Running;
    public int CurrentWaveNumber => TotalWaveCount <= 0 ? 0 : Mathf.Clamp(currentWaveIndex + 1, 1, TotalWaveCount);
    public int TotalWaveCount => waveDefinitions != null ? waveDefinitions.Count : 0;
    public int NextWaveNumber => HasNextWave ? currentWaveIndex + 2 : 0;
    public int PopupCurrentWaveNumber => popupCurrentWaveNumber;
    public int PopupNextWaveNumber => popupNextWaveNumber;
    public string PopupPrimaryActionLabel => GetPopupPrimaryActionLabel();
    public Player TargetPlayer => targetPlayer;
    public bool ShowTemporaryTestPopup => showTemporaryTestPopup;
    public bool ShowRewardSelection => showRewardSelection;
    public string TemporaryTestPopupTitle => temporaryTestPopupTitle;
    public string TemporaryTestPopupMessage => temporaryTestPopupMessage;
    public KeyCode TemporaryTestContinueKey => continueKey;
    public bool PauseMenuOpen => pauseMenuOpen;
    public bool HasNextWave => currentWaveIndex >= 0 && currentWaveIndex < TotalWaveCount - 1;
    public bool CanOpenPauseMenu => !showTemporaryTestPopup && !showRewardSelection && currentState == LevelState.Running;
    public string StartMenuSceneName => startMenuSceneName;
    public int RewardOfferCount => activeRewardOffers.Count;

    private void Awake()
    {
        EnsureWaveDefinitions();
        pauseMenuOpen = false;
        currentWaveIndex = 0;
        ResolveReferences();
        remainingTimeSeconds = GetCurrentWaveDuration();
        ClearTemporaryTestPopup();
        ClearRewardSelection();
        RefreshTimeScale();
    }

    private void OnEnable()
    {
        AudioManager.PlayBattleMusic();
    }

    private void Start()
    {
        if (autoStartOnPlay)
        {
            StartLevel();
        }
    }

    private void Update()
    {
        if (showRewardSelection)
        {
            return;
        }

        if (showTemporaryTestPopup)
        {
            HandleTemporaryTestPopupInput();
            return;
        }

        if (currentState != LevelState.Running)
        {
            return;
        }

        ResolvePlayerReference();
        if (targetPlayer == null)
        {
            return;
        }

        if (!targetPlayer.IsAlive)
        {
            FailLevel();
            return;
        }

        remainingTimeSeconds = Mathf.Max(0f, remainingTimeSeconds - Time.deltaTime);
        if (remainingTimeSeconds <= 0f)
        {
            CompleteLevel();
        }
    }

    [ContextMenu("Start Level")]
    public void StartLevel()
    {
        EnsureWaveDefinitions();
        pauseMenuOpen = false;
        ClearTemporaryTestPopup();
        ClearRewardSelection();
        ResolveReferences();
        PreparePlayer();
        ResolvePlayerReference();
        RewardSelectionSession.ApplyRunBonuses(targetPlayer);

        if (targetPlayer == null)
        {
            Debug.LogWarning("[LevelManager] 没有找到玩家对象，无法开始关卡。", this);
            return;
        }

        StartWave(ConsumePendingStartWaveIndex(), resetSpawnerStateOnStart, false);
    }

    [ContextMenu("Complete Level")]
    public void CompleteLevel()
    {
        if (currentState == LevelState.Succeeded)
        {
            return;
        }

        remainingTimeSeconds = 0f;
        currentState = LevelState.Succeeded;
        HandleLevelEnded(stopEnemySpawnerOnSuccess, disableRemainingEnemiesOnSuccess);

        if (HasNextWave)
        {
            OpenRewardSelection(CurrentWaveNumber, NextWaveNumber);
            return;
        }

        OpenTemporaryTestPopup("全部波次完成", finalWaveSuccessMessage, PopupAction.LoadVictoryScene, CurrentWaveNumber, 0);
    }

    [ContextMenu("Fail Level")]
    public void FailLevel()
    {
        if (currentState == LevelState.Failed)
        {
            return;
        }

        currentState = LevelState.Failed;
        HandleLevelEnded(stopEnemySpawnerOnFailure, disableRemainingEnemiesOnFailure);
        OpenTemporaryTestPopup("游戏失败", temporaryTestFailureMessage, PopupAction.LoadFailureScene, CurrentWaveNumber, 0);
    }

    [ContextMenu("Reset Level State")]
    public void ResetLevelState()
    {
        EnsureWaveDefinitions();
        pendingStartWaveIndex = NoPendingWaveIndex;
        pauseMenuOpen = false;
        ClearTemporaryTestPopup();
        ClearRewardSelection();
        RewardSelectionSession.ClearRewards();
        ResolveReferences();
        currentWaveIndex = 0;
        remainingTimeSeconds = GetCurrentWaveDuration();
        currentState = LevelState.Idle;

        if (enemySpawner != null)
        {
            enemySpawner.SetAutoSpawn(false);
            enemySpawner.enabled = false;
            enemySpawner.SetWaveMultipliers(1f, 1f, 1f, 1f);
            if (resetSpawnerStateOnStart)
            {
                enemySpawner.ResetSpawnState();
            }
        }

        RefreshTimeScale();
    }

    private void HandleLevelEnded(bool stopSpawner, bool disableRemainingEnemies)
    {
        if (enemySpawner != null && stopSpawner)
        {
            enemySpawner.SetAutoSpawn(false);
            enemySpawner.enabled = false;
        }

        if (!disableRemainingEnemies)
        {
            return;
        }

        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        for (int index = 0; index < enemies.Length; index++)
        {
            EnemyBase enemy = enemies[index];
            if (enemy == null)
            {
                continue;
            }

            enemy.SetEnemyEnabled(false);
        }
    }

    private void PreparePlayer()
    {
        if (!initializeGameOnStart)
        {
            return;
        }

        if (gameInitializer == null)
        {
            gameInitializer = FindFirstObjectByType<GameInitializer>();
        }

        if (gameInitializer == null)
        {
            return;
        }

        Player initializedPlayer = gameInitializer.InitializePlayer();
        if (initializedPlayer != null)
        {
            targetPlayer = initializedPlayer;
        }
    }

    private void ResolveReferences()
    {
        if (gameInitializer == null)
        {
            gameInitializer = FindFirstObjectByType<GameInitializer>();
        }

        if (enemySpawner == null)
        {
            enemySpawner = FindFirstObjectByType<EnemySpawner>();
        }

        ResolvePlayerReference();
    }

    public void TogglePauseMenu()
    {
        if (pauseMenuOpen)
        {
            ClosePauseMenu();
            return;
        }

        OpenPauseMenu();
    }

    public void OpenPauseMenu()
    {
        if (!CanOpenPauseMenu)
        {
            return;
        }

        pauseMenuOpen = true;
        RefreshTimeScale();
    }

    public void ClosePauseMenu()
    {
        if (!pauseMenuOpen)
        {
            return;
        }

        pauseMenuOpen = false;
        RefreshTimeScale();
    }

    public void ExecutePopupPrimaryAction()
    {
        switch (popupAction)
        {
            case PopupAction.NextWave:
                AdvanceToNextWave();
                break;
            case PopupAction.RestartLevel:
                RestartLevelFromFirstWave();
                break;
            case PopupAction.ReturnToStartScene:
                ReturnToStartMenu();
                break;
            case PopupAction.LoadVictoryScene:
                LoadVictoryResultScene();
                break;
            case PopupAction.LoadFailureScene:
                LoadFailureResultScene();
                break;
        }
    }

    private void ResolvePlayerReference()
    {
        if (targetPlayer == null)
        {
            targetPlayer = FindFirstObjectByType<Player>();
        }
    }

    private void HandleTemporaryTestPopupInput()
    {
        if (!allowSpaceToRestartCurrentTestLevel)
        {
            return;
        }

        if (!IsContinueKeyPressed())
        {
            return;
        }

        ExecutePopupPrimaryAction();
    }

    private bool IsContinueKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            switch (continueKey)
            {
                case KeyCode.Space:
                    return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                case KeyCode.Escape:
                    return keyboard.escapeKey.wasPressedThisFrame;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(continueKey);
#else
        return false;
#endif
    }

    private void OpenTemporaryTestPopup(string title, string message, PopupAction action, int currentWaveNumber, int nextWaveNumber)
    {
        ClearRewardSelection();
        showTemporaryTestPopup = true;
        temporaryTestPopupTitle = title;
        temporaryTestPopupMessage = message;
        popupAction = action;
        popupCurrentWaveNumber = currentWaveNumber;
        popupNextWaveNumber = nextWaveNumber;
        pauseMenuOpen = false;
        RefreshTimeScale();
    }

    private void ClearTemporaryTestPopup()
    {
        showTemporaryTestPopup = false;
        temporaryTestPopupTitle = string.Empty;
        temporaryTestPopupMessage = string.Empty;
        popupAction = PopupAction.None;
        popupCurrentWaveNumber = 0;
        popupNextWaveNumber = 0;
    }

    public bool TryGetRewardOffer(int index, out RewardType rewardType)
    {
        if (index < 0 || index >= activeRewardOffers.Count)
        {
            rewardType = RewardType.MaxHealth;
            return false;
        }

        rewardType = activeRewardOffers[index];
        return true;
    }

    public void SelectReward(RewardType rewardType)
    {
        if (!showRewardSelection || !activeRewardOffers.Contains(rewardType))
        {
            return;
        }

        RewardSelectionSession.AddReward(rewardType);
        ClearRewardSelection();
        AdvanceToNextWave();
    }

    private void OpenRewardSelection(int currentWaveNumber, int nextWaveNumber)
    {
        ClearTemporaryTestPopup();
        activeRewardOffers.Clear();
        activeRewardOffers.AddRange(RewardSelectionSession.BuildRewardOffers(targetPlayer, 3));

        if (activeRewardOffers.Count == 0)
        {
            OpenTemporaryTestPopup("波次完成", temporaryTestSuccessMessage, PopupAction.NextWave, currentWaveNumber, nextWaveNumber);
            return;
        }

        showRewardSelection = true;
        popupCurrentWaveNumber = currentWaveNumber;
        popupNextWaveNumber = nextWaveNumber;
        pauseMenuOpen = false;
        RefreshTimeScale();
    }

    private void ClearRewardSelection()
    {
        showRewardSelection = false;
        activeRewardOffers.Clear();
    }

    [ContextMenu("Restart Current Test Level")]
    public void RestartCurrentTestLevel()
    {
        RestartSceneAtWave(currentWaveIndex);
    }

    public void RestartLevelFromFirstWave()
    {
        RewardSelectionSession.ClearRewards();
        RestartSceneAtWave(0);
    }

    private void RestartSceneAtWave(int waveIndex)
    {
        pendingStartWaveIndex = Mathf.Max(0, waveIndex);
        pauseMenuOpen = false;
        ClearTemporaryTestPopup();
        ClearRewardSelection();
        RefreshTimeScale();

        Scene activeScene = SceneManager.GetActiveScene();
        string sceneReference = !string.IsNullOrWhiteSpace(activeScene.path)
            ? activeScene.path
            : activeScene.name;

        if (string.IsNullOrWhiteSpace(sceneReference))
        {
            pendingStartWaveIndex = NoPendingWaveIndex;
            Debug.LogWarning("[LevelManager] 当前场景没有可重载的引用，无法重开测试关卡。", this);
            return;
        }

        SceneManager.LoadScene(sceneReference);
    }

    public void ReturnToStartMenu()
    {
        if (string.IsNullOrWhiteSpace(startMenuSceneName))
        {
            Debug.LogWarning("[LevelManager] 未配置开始界面场景名，无法返回开始界面。", this);
            return;
        }

        pendingStartWaveIndex = NoPendingWaveIndex;
        WeaponSelectionSession.ClearSelection();
        RewardSelectionSession.ClearRewards();
        pauseMenuOpen = false;
        ClearTemporaryTestPopup();
        ClearRewardSelection();
        Time.timeScale = 1f;
        SceneManager.LoadScene(startMenuSceneName);
    }

    private void AdvanceToNextWave()
    {
        if (!HasNextWave)
        {
            return;
        }

        RestartSceneAtWave(currentWaveIndex + 1);
    }

    private void StartWave(int waveIndex, bool resetSpawnerState, bool preserveAliveEnemies)
    {
        EnsureWaveDefinitions();
        ResolveReferences();
        currentWaveIndex = Mathf.Clamp(waveIndex, 0, Mathf.Max(TotalWaveCount - 1, 0));
        remainingTimeSeconds = GetCurrentWaveDuration();
        currentState = LevelState.Running;
        pauseMenuOpen = false;
        ClearTemporaryTestPopup();
        ClearRewardSelection();

        if (enemySpawner != null)
        {
            WaveDefinition wave = GetCurrentWaveDefinition();
            if (wave != null)
            {
                enemySpawner.SetWaveMultipliers(
                    wave.enemyHealthMultiplier,
                    wave.enemyDamageMultiplier,
                    wave.enemyMoveSpeedMultiplier,
                    wave.spawnRateMultiplier);
            }
            else
            {
                enemySpawner.SetWaveMultipliers(1f, 1f, 1f, 1f);
            }

            if (resetSpawnerState)
            {
                enemySpawner.ResetSpawnState(preserveAliveEnemies);
            }

            enemySpawner.SetAutoSpawn(enableEnemySpawnerOnStart);
            enemySpawner.enabled = enableEnemySpawnerOnStart;
        }

        RefreshTimeScale();
    }

    private float GetCurrentWaveDuration()
    {
        WaveDefinition wave = GetCurrentWaveDefinition();
        return wave != null ? Mathf.Max(1f, wave.durationSeconds) : 0f;
    }

    private WaveDefinition GetCurrentWaveDefinition()
    {
        if (waveDefinitions == null || waveDefinitions.Count == 0)
        {
            return null;
        }

        int clampedIndex = Mathf.Clamp(currentWaveIndex, 0, waveDefinitions.Count - 1);
        return waveDefinitions[clampedIndex];
    }

    private string GetPopupPrimaryActionLabel()
    {
        switch (popupAction)
        {
            case PopupAction.NextWave:
                return "进入下一波";
            case PopupAction.RestartLevel:
                return "重新开始";
            case PopupAction.ReturnToStartScene:
                return "返回开始界面";
            case PopupAction.LoadVictoryScene:
                return "进入胜利场景";
            case PopupAction.LoadFailureScene:
                return "进入失败场景";
            default:
                return "继续";
        }
    }

    private void LoadVictoryResultScene()
    {
        if (!TryLoadConfiguredResultScene(victoryResultSceneName, "胜利"))
        {
            ReturnToStartMenu();
        }
    }

    private void LoadFailureResultScene()
    {
        if (!TryLoadConfiguredResultScene(failureResultSceneName, "失败"))
        {
            ReturnToStartMenu();
        }
    }

    private bool TryLoadConfiguredResultScene(string sceneName, string scenePurpose)
    {
        if (!CanLoadScene(sceneName))
        {
            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"[LevelManager] {scenePurpose}结果场景 '{sceneName}' 当前不可加载，请把它加入 Build Profiles。已回退到开始界面。", this);
            }

            return false;
        }

        pendingStartWaveIndex = NoPendingWaveIndex;
        pauseMenuOpen = false;
        ClearTemporaryTestPopup();
        ClearRewardSelection();
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
        return true;
    }

    private static bool CanLoadScene(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    private static int ConsumePendingStartWaveIndex()
    {
        int waveIndex = pendingStartWaveIndex;
        pendingStartWaveIndex = NoPendingWaveIndex;
        return waveIndex >= 0 ? waveIndex : 0;
    }

    private void RefreshTimeScale()
    {
        bool shouldPauseForPopup = pauseTimeScaleOnWaveEnd && (showTemporaryTestPopup || showRewardSelection);
        Time.timeScale = shouldPauseForPopup || pauseMenuOpen ? 0f : 1f;
    }

    private void EnsureWaveDefinitions()
    {
        if (waveDefinitions == null || waveDefinitions.Count == 0)
        {
            waveDefinitions = CreateDefaultWaveDefinitions();
        }
    }

    private static List<WaveDefinition> CreateDefaultWaveDefinitions()
    {
        return new List<WaveDefinition>
        {
            new WaveDefinition { name = "Wave 1", durationSeconds = 60f, enemyHealthMultiplier = 1f, enemyDamageMultiplier = 1f, enemyMoveSpeedMultiplier = 1f, spawnRateMultiplier = 1f },
            new WaveDefinition { name = "Wave 2", durationSeconds = 65f, enemyHealthMultiplier = 1.2f, enemyDamageMultiplier = 1.1f, enemyMoveSpeedMultiplier = 1.05f, spawnRateMultiplier = 1.15f },
            new WaveDefinition { name = "Wave 3", durationSeconds = 70f, enemyHealthMultiplier = 1.4f, enemyDamageMultiplier = 1.2f, enemyMoveSpeedMultiplier = 1.1f, spawnRateMultiplier = 1.3f },
            new WaveDefinition { name = "Wave 4", durationSeconds = 75f, enemyHealthMultiplier = 1.65f, enemyDamageMultiplier = 1.35f, enemyMoveSpeedMultiplier = 1.2f, spawnRateMultiplier = 1.45f },
            new WaveDefinition { name = "Wave 5", durationSeconds = 90f, enemyHealthMultiplier = 2f, enemyDamageMultiplier = 1.5f, enemyMoveSpeedMultiplier = 1.3f, spawnRateMultiplier = 1.6f }
        };
    }

    private void OnValidate()
    {
        EnsureWaveDefinitions();
        resetSpawnerStateOnStart = resetSpawnerStateOnStart;
        resetSpawnerStateEachWave = resetSpawnerStateEachWave;

        for (int index = 0; index < waveDefinitions.Count; index++)
        {
            WaveDefinition wave = waveDefinitions[index];
            if (wave == null)
            {
                continue;
            }

            wave.durationSeconds = Mathf.Max(1f, wave.durationSeconds);
            wave.enemyHealthMultiplier = Mathf.Max(0.01f, wave.enemyHealthMultiplier);
            wave.enemyDamageMultiplier = Mathf.Max(0.01f, wave.enemyDamageMultiplier);
            wave.enemyMoveSpeedMultiplier = Mathf.Max(0.01f, wave.enemyMoveSpeedMultiplier);
            wave.spawnRateMultiplier = Mathf.Max(0.01f, wave.spawnRateMultiplier);
        }
    }
}