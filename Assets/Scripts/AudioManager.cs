using UnityEngine;

[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    [Header("背景音乐")]
    [SerializeField, InspectorName("开始界面背景音乐"), Tooltip("开始界面和武器选择界面共用的背景音乐。")]
    private AudioClip startMenuMusic;
    [SerializeField, InspectorName("战斗场景背景音乐"), Tooltip("进入战斗场景后循环播放的背景音乐。")]
    private AudioClip battleMusic;

    [Header("界面音效")]
    [SerializeField, InspectorName("按钮悬停音效"), Tooltip("鼠标第一次移动到可交互按钮上时播放。")]
    private AudioClip uiHoverSound;
    [SerializeField, InspectorName("按钮按下音效"), Tooltip("鼠标按下可交互按钮时播放。")]
    private AudioClip uiClickSound;

    [Header("战斗音效")]
    [SerializeField, InspectorName("玩家脚步循环音效"), Tooltip("玩家移动时循环播放的脚步音效。")]
    private AudioClip playerFootstepLoop;
    [SerializeField, InspectorName("笔刷攻击循环音效"), Tooltip("笔刷持续攻击时循环播放的音效。")]
    private AudioClip brushAttackLoop;
    [SerializeField, InspectorName("弓箭射出音效"), Tooltip("每次成功射出弓箭时播放的音效。")]
    private AudioClip bowShootSound;
    [SerializeField, InspectorName("弓箭命中音效"), Tooltip("弓箭命中敌人或阻挡物时播放的音效。")]
    private AudioClip bowImpactSound;
    [SerializeField, InspectorName("玩家受伤音效"), Tooltip("玩家成功受到伤害时播放的音效。")]
    private AudioClip playerHurtSound;

    [Header("音量设置")]
    [SerializeField, Range(0f, 1f), InspectorName("背景音乐音量")]
    private float musicVolume = 0.8f;
    [SerializeField, Range(0f, 1f), InspectorName("界面音效音量")]
    private float uiSoundVolume = 1f;
    [SerializeField, Range(0f, 1f), InspectorName("战斗音效音量")]
    private float combatSoundVolume = 1f;
    [SerializeField, Range(0f, 1f), InspectorName("循环音效音量")]
    private float loopSoundVolume = 0.85f;

    private AudioSource musicSource;
    private AudioSource oneShotSource;
    private AudioSource footstepLoopSource;
    private AudioSource brushLoopSource;

    public static AudioManager Instance => FindInstance();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSources();
        ApplySourceVolumes();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static void PlayMenuMusic()
    {
        AudioManager manager = FindInstance();
        manager?.PlayMusic(manager.startMenuMusic);
    }

    public static void PlayBattleMusic()
    {
        AudioManager manager = FindInstance();
        manager?.PlayMusic(manager.battleMusic);
    }

    public static void PlayUiHover()
    {
        AudioManager manager = FindInstance();
        manager?.PlayOneShot(manager.uiHoverSound, manager.uiSoundVolume);
    }

    public static void PlayUiClick()
    {
        AudioManager manager = FindInstance();
        manager?.PlayOneShot(manager.uiClickSound, manager.uiSoundVolume);
    }

    public static void SetFootstepLoopActive(bool active)
    {
        AudioManager manager = FindInstance();
        manager?.SetLoopState(manager.footstepLoopSource, manager.playerFootstepLoop, active);
    }

    public static void SetBrushAttackLoopActive(bool active)
    {
        AudioManager manager = FindInstance();
        manager?.SetLoopState(manager.brushLoopSource, manager.brushAttackLoop, active);
    }

    public static void PlayBowShoot()
    {
        AudioManager manager = FindInstance();
        manager?.PlayOneShot(manager.bowShootSound, manager.combatSoundVolume);
    }

    public static void PlayBowImpact()
    {
        AudioManager manager = FindInstance();
        manager?.PlayOneShot(manager.bowImpactSound, manager.combatSoundVolume);
    }

    public static void PlayPlayerHurt()
    {
        AudioManager manager = FindInstance();
        manager?.PlayOneShot(manager.playerHurtSound, manager.combatSoundVolume);
    }

    public static float GetMusicVolume()
    {
        AudioManager manager = FindInstance();
        return manager != null ? manager.musicVolume : 0f;
    }

    public static float GetUiSoundVolume()
    {
        AudioManager manager = FindInstance();
        return manager != null ? manager.uiSoundVolume : 0f;
    }

    public static float GetCombatSoundVolume()
    {
        AudioManager manager = FindInstance();
        return manager != null ? manager.combatSoundVolume : 0f;
    }

    public static float GetLoopSoundVolume()
    {
        AudioManager manager = FindInstance();
        return manager != null ? manager.loopSoundVolume : 0f;
    }

    public static void SetMusicVolume(float value)
    {
        AudioManager manager = FindInstance();
        if (manager == null)
        {
            return;
        }

        manager.musicVolume = Mathf.Clamp01(value);
        manager.ApplySourceVolumes();
    }

    public static void SetUiSoundVolume(float value)
    {
        AudioManager manager = FindInstance();
        if (manager == null)
        {
            return;
        }

        manager.uiSoundVolume = Mathf.Clamp01(value);
    }

    public static void SetCombatSoundVolume(float value)
    {
        AudioManager manager = FindInstance();
        if (manager == null)
        {
            return;
        }

        manager.combatSoundVolume = Mathf.Clamp01(value);
    }

    public static void SetLoopSoundVolume(float value)
    {
        AudioManager manager = FindInstance();
        if (manager == null)
        {
            return;
        }

        manager.loopSoundVolume = Mathf.Clamp01(value);
        manager.ApplySourceVolumes();
    }

    private void PlayMusic(AudioClip clip)
    {
        EnsureSources();
        if (musicSource == null)
        {
            return;
        }

        if (clip == null)
        {
            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        EnsureSources();
        if (clip == null || oneShotSource == null)
        {
            return;
        }

        oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void SetLoopState(AudioSource source, AudioClip clip, bool active)
    {
        EnsureSources();
        if (source == null)
        {
            return;
        }

        if (!active || clip == null)
        {
            if (source.isPlaying)
            {
                source.Stop();
            }

            source.clip = clip;
            return;
        }

        source.volume = loopSoundVolume;
        if (source.clip != clip)
        {
            source.clip = clip;
        }

        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    private void EnsureSources()
    {
        musicSource = EnsureChildSource(musicSource, "MusicSource", true);
        oneShotSource = EnsureChildSource(oneShotSource, "OneShotSource", false);
        footstepLoopSource = EnsureChildSource(footstepLoopSource, "FootstepLoopSource", true);
        brushLoopSource = EnsureChildSource(brushLoopSource, "BrushLoopSource", true);
    }

    private AudioSource EnsureChildSource(AudioSource source, string childName, bool loop)
    {
        if (source != null)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        Transform existingChild = transform.Find(childName);
        GameObject childObject = existingChild != null ? existingChild.gameObject : new GameObject(childName);
        if (existingChild == null)
        {
            childObject.transform.SetParent(transform, false);
        }

        source = childObject.GetComponent<AudioSource>();
        if (source == null)
        {
            source = childObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    private void ApplySourceVolumes()
    {
        EnsureSources();
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }

        if (footstepLoopSource != null)
        {
            footstepLoopSource.volume = loopSoundVolume;
        }

        if (brushLoopSource != null)
        {
            brushLoopSource.volume = loopSoundVolume;
        }
    }

    private static AudioManager FindInstance()
    {
        if (instance == null)
        {
            instance = FindFirstObjectByType<AudioManager>();
        }

        return instance;
    }

    private void OnValidate()
    {
        musicVolume = Mathf.Clamp01(musicVolume);
        uiSoundVolume = Mathf.Clamp01(uiSoundVolume);
        combatSoundVolume = Mathf.Clamp01(combatSoundVolume);
        loopSoundVolume = Mathf.Clamp01(loopSoundVolume);

        if (Application.isPlaying)
        {
            ApplySourceVolumes();
        }
    }
}