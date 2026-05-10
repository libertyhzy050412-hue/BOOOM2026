#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.SceneManagement;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LevelManager : MonoBehaviour
{
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

    [Header("Wave Settings")]
    [SerializeField, Min(1f)] private float waveDurationSeconds = 60f;
    [SerializeField] private bool resetSpawnerStateOnStart = true;
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
    [SerializeField, TextArea(2, 4)] private string temporaryTestSuccessMessage = "当前波次结束。\n按空格进入下一关。\n测试阶段会重开当前关卡。";
    [SerializeField, TextArea(2, 4)] private string temporaryTestFailureMessage = "游戏失败。\n按空格重试。";

    private LevelState currentState = LevelState.Idle;
    private float remainingTimeSeconds;
    private bool showTemporaryTestPopup;
    private string temporaryTestPopupTitle = string.Empty;
    private string temporaryTestPopupMessage = string.Empty;

    public LevelState CurrentState => currentState;
    public float RemainingTimeSeconds => remainingTimeSeconds;
    public float NormalizedRemainingTime => waveDurationSeconds <= 0f ? 0f : Mathf.Clamp01(remainingTimeSeconds / waveDurationSeconds);
    public bool IsRunning => currentState == LevelState.Running;
    public int CurrentWaveNumber => 1;
    public Player TargetPlayer => targetPlayer;
    public bool ShowTemporaryTestPopup => showTemporaryTestPopup;
    public string TemporaryTestPopupTitle => temporaryTestPopupTitle;
    public string TemporaryTestPopupMessage => temporaryTestPopupMessage;
    public KeyCode TemporaryTestContinueKey => continueKey;

    private void Awake()
    {
        Time.timeScale = 1f;
        ResolveReferences();
        remainingTimeSeconds = Mathf.Max(0f, waveDurationSeconds);
        ClearTemporaryTestPopup();
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
        Time.timeScale = 1f;
        ClearTemporaryTestPopup();
        ResolveReferences();
        PreparePlayer();
        ResolvePlayerReference();

        if (targetPlayer == null)
        {
            Debug.LogWarning("[LevelManager] 没有找到玩家对象，无法开始关卡。", this);
            return;
        }

        remainingTimeSeconds = waveDurationSeconds;
        currentState = LevelState.Running;

        if (enemySpawner != null)
        {
            if (resetSpawnerStateOnStart)
            {
                enemySpawner.ResetSpawnState();
            }

            enemySpawner.SetAutoSpawn(enableEnemySpawnerOnStart);
            enemySpawner.enabled = enableEnemySpawnerOnStart;
        }
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
        OpenTemporaryTestPopup("波次完成", temporaryTestSuccessMessage);
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
        OpenTemporaryTestPopup("游戏失败", temporaryTestFailureMessage);
    }

    [ContextMenu("Reset Level State")]
    public void ResetLevelState()
    {
        Time.timeScale = 1f;
        ClearTemporaryTestPopup();
        ResolveReferences();
        remainingTimeSeconds = waveDurationSeconds;
        currentState = LevelState.Idle;

        if (enemySpawner != null)
        {
            enemySpawner.SetAutoSpawn(false);
            enemySpawner.enabled = false;
            if (resetSpawnerStateOnStart)
            {
                enemySpawner.ResetSpawnState();
            }
        }
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

        RestartCurrentTestLevel();
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

    private void OpenTemporaryTestPopup(string title, string message)
    {
        showTemporaryTestPopup = true;
        temporaryTestPopupTitle = title;
        temporaryTestPopupMessage = message;

        if (pauseTimeScaleOnWaveEnd)
        {
            Time.timeScale = 0f;
        }
    }

    private void ClearTemporaryTestPopup()
    {
        showTemporaryTestPopup = false;
        temporaryTestPopupTitle = string.Empty;
        temporaryTestPopupMessage = string.Empty;
    }

    [ContextMenu("Restart Current Test Level")]
    public void RestartCurrentTestLevel()
    {
        Time.timeScale = 1f;
        ClearTemporaryTestPopup();

        Scene activeScene = SceneManager.GetActiveScene();
        string sceneReference = !string.IsNullOrWhiteSpace(activeScene.path)
            ? activeScene.path
            : activeScene.name;

        if (string.IsNullOrWhiteSpace(sceneReference))
        {
            Debug.LogWarning("[LevelManager] 当前场景没有可重载的引用，无法重开测试关卡。", this);
            return;
        }

        SceneManager.LoadScene(sceneReference);
    }

    private void OnValidate()
    {
        waveDurationSeconds = Mathf.Max(1f, waveDurationSeconds);
    }
}