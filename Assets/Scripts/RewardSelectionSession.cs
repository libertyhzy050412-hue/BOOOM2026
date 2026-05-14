using System;
using System.Collections.Generic;
using UnityEngine;

public enum RewardType
{
    MaxHealth = 0,
    AttackPower = 1,
    BrushInkCapacity = 2,
    BrushInkRecovery = 3,
    MoveSpeed = 4
}

public static class RewardSelectionSession
{
    private const string BalanceConfigResourcePath = "RewardSelectionBalance";

    private sealed class RewardDefinition
    {
        public RewardDefinition(RewardType type, string displayName, string descriptionFormat, string iconResourcePath, bool requiresInkWeapon)
        {
            Type = type;
            DisplayName = displayName;
            DescriptionFormat = descriptionFormat;
            IconResourcePath = iconResourcePath;
            RequiresInkWeapon = requiresInkWeapon;
        }

        public RewardType Type { get; }
        public string DisplayName { get; }
        public string DescriptionFormat { get; }
        public string IconResourcePath { get; }
        public bool RequiresInkWeapon { get; }
    }

    [Serializable]
    private sealed class RewardBalanceConfig
    {
        public float maxHealthBonusPerStack = 20f;
        public float attackPowerPercentBonusPerStack = 20f;
        public float brushInkCapacityBonusPerStack = 4f;
        public float brushInkRecoveryBonusPerStack = 0.125f;
        public float moveSpeedBonusPerStack = 0.75f;

        public void ClampValues()
        {
            maxHealthBonusPerStack = Mathf.Max(0f, maxHealthBonusPerStack);
            attackPowerPercentBonusPerStack = Mathf.Max(0f, attackPowerPercentBonusPerStack);
            brushInkCapacityBonusPerStack = Mathf.Max(0f, brushInkCapacityBonusPerStack);
            brushInkRecoveryBonusPerStack = Mathf.Max(0f, brushInkRecoveryBonusPerStack);
            moveSpeedBonusPerStack = Mathf.Max(0f, moveSpeedBonusPerStack);
        }
    }

    private static readonly RewardType[] AllRewardTypes =
    {
        RewardType.MaxHealth,
        RewardType.AttackPower,
        RewardType.BrushInkCapacity,
        RewardType.BrushInkRecovery,
        RewardType.MoveSpeed
    };

    private static readonly Dictionary<RewardType, RewardDefinition> RewardDefinitions = new Dictionary<RewardType, RewardDefinition>
    {
        { RewardType.MaxHealth, new RewardDefinition(RewardType.MaxHealth, "小熊", "+{0} 最大生命值", "商店道具/生命值-小熊", false) },
        { RewardType.AttackPower, new RewardDefinition(RewardType.AttackPower, "魔爪", "+{0}% 攻击力", "商店道具/攻击力·-魔爪", false) },
        { RewardType.BrushInkCapacity, new RewardDefinition(RewardType.BrushInkCapacity, "颜料桶", "+{0} 颜料容量上限", "商店道具/脑容量最大值-桶", true) },
        { RewardType.BrushInkRecovery, new RewardDefinition(RewardType.BrushInkRecovery, "枕头", "+{0}% 最大颜料/秒", "商店道具/脑容量恢复速度-枕头", true) },
        { RewardType.MoveSpeed, new RewardDefinition(RewardType.MoveSpeed, "哥特风服饰", "+{0} 移动速度", "商店道具/移动速度-裙子", false) }
    };

    private static readonly Dictionary<RewardType, int> RewardStacks = new Dictionary<RewardType, int>();
    private static readonly Dictionary<RewardType, Sprite> IconCache = new Dictionary<RewardType, Sprite>();
    private static RewardBalanceConfig cachedBalanceConfig;

    public static int TotalSelectedRewardCount
    {
        get
        {
            int total = 0;
            foreach (KeyValuePair<RewardType, int> pair in RewardStacks)
            {
                total += Mathf.Max(0, pair.Value);
            }

            return total;
        }
    }

    public static void ClearRewards()
    {
        RewardStacks.Clear();
    }

    public static List<RewardType> BuildRewardOffers(Player player, int offerCount)
    {
        List<RewardType> candidates = new List<RewardType>(AllRewardTypes.Length);
        bool hasInkWeapon = player != null &&
            (player.GetComponentInChildren<BrushWeapon>(true) != null || player.GetComponentInChildren<BowWeapon>(true) != null);

        for (int index = 0; index < AllRewardTypes.Length; index++)
        {
            RewardType rewardType = AllRewardTypes[index];
            RewardDefinition definition = GetDefinition(rewardType);
            if (definition == null)
            {
                continue;
            }

            if (definition.RequiresInkWeapon && !hasInkWeapon)
            {
                continue;
            }

            candidates.Add(rewardType);
        }

        if (candidates.Count == 0)
        {
            return new List<RewardType>();
        }

        Shuffle(candidates, new System.Random(unchecked(Environment.TickCount ^ (TotalSelectedRewardCount << 8) ^ offerCount)));
        int finalOfferCount = Mathf.Min(Mathf.Max(1, offerCount), candidates.Count);
        if (candidates.Count > finalOfferCount)
        {
            candidates.RemoveRange(finalOfferCount, candidates.Count - finalOfferCount);
        }

        return candidates;
    }

    public static void AddReward(RewardType rewardType)
    {
        RewardStacks[rewardType] = GetStackCount(rewardType) + 1;
    }

    public static int GetStackCount(RewardType rewardType)
    {
        return RewardStacks.TryGetValue(rewardType, out int stackCount) ? Mathf.Max(0, stackCount) : 0;
    }

    public static string GetDisplayName(RewardType rewardType)
    {
        RewardDefinition definition = GetDefinition(rewardType);
        return definition != null ? definition.DisplayName : rewardType.ToString();
    }

    public static string GetDescription(RewardType rewardType)
    {
        RewardDefinition definition = GetDefinition(rewardType);
        return definition == null ? string.Empty : string.Format(definition.DescriptionFormat, FormatStepValue(rewardType));
    }

    public static string GetStackSummary(RewardType rewardType)
    {
        int stackCount = GetStackCount(rewardType);
        return stackCount > 0 ? $"已拥有 x{stackCount}" : "未持有";
    }

    public static Sprite GetIcon(RewardType rewardType)
    {
        if (IconCache.TryGetValue(rewardType, out Sprite cachedIcon))
        {
            return cachedIcon;
        }

        RewardDefinition definition = GetDefinition(rewardType);
        if (definition == null || string.IsNullOrWhiteSpace(definition.IconResourcePath))
        {
            return null;
        }

        Sprite loadedIcon = Resources.Load<Sprite>(definition.IconResourcePath);
        IconCache[rewardType] = loadedIcon;
        return loadedIcon;
    }

    public static void ApplyRunBonuses(Player player)
    {
        if (player == null)
        {
            return;
        }

        player.ApplyRuntimeRewardModifiers(GetTotalMoveSpeedBonus(), GetTotalMaxHealthBonus(), GetTotalAttackPowerPercentBonus());

        BrushWeapon[] brushWeapons = player.GetComponentsInChildren<BrushWeapon>(true);
        for (int index = 0; index < brushWeapons.Length; index++)
        {
            BrushWeapon brushWeapon = brushWeapons[index];
            if (brushWeapon == null)
            {
                continue;
            }

            brushWeapon.ApplyRuntimeRewardModifiers(GetTotalBrushInkCapacityBonus(), GetTotalBrushInkRecoveryBonus());
        }

        BowWeapon[] bowWeapons = player.GetComponentsInChildren<BowWeapon>(true);
        for (int index = 0; index < bowWeapons.Length; index++)
        {
            BowWeapon bowWeapon = bowWeapons[index];
            if (bowWeapon == null)
            {
                continue;
            }

            bowWeapon.ApplyRuntimeRewardModifiers(GetTotalBrushInkCapacityBonus(), GetTotalBrushInkRecoveryBonus());
        }
    }

    private static RewardDefinition GetDefinition(RewardType rewardType)
    {
        RewardDefinitions.TryGetValue(rewardType, out RewardDefinition definition);
        return definition;
    }

    private static string FormatStepValue(RewardType rewardType)
    {
        RewardBalanceConfig config = GetBalanceConfig();
        switch (rewardType)
        {
            case RewardType.MaxHealth:
                return config.maxHealthBonusPerStack.ToString("0");
            case RewardType.AttackPower:
                return config.attackPowerPercentBonusPerStack.ToString("0");
            case RewardType.BrushInkCapacity:
                return config.brushInkCapacityBonusPerStack.ToString("0.0");
            case RewardType.BrushInkRecovery:
                return (config.brushInkRecoveryBonusPerStack * 100f).ToString("0.0");
            case RewardType.MoveSpeed:
                return config.moveSpeedBonusPerStack.ToString("0.0");
            default:
                return "0";
        }
    }

    private static float GetTotalMaxHealthBonus()
    {
        RewardBalanceConfig config = GetBalanceConfig();
        return GetStackCount(RewardType.MaxHealth) * config.maxHealthBonusPerStack;
    }

    private static float GetTotalAttackPowerPercentBonus()
    {
        RewardBalanceConfig config = GetBalanceConfig();
        return GetStackCount(RewardType.AttackPower) * config.attackPowerPercentBonusPerStack;
    }

    private static float GetTotalBrushInkCapacityBonus()
    {
        RewardBalanceConfig config = GetBalanceConfig();
        return GetStackCount(RewardType.BrushInkCapacity) * config.brushInkCapacityBonusPerStack;
    }

    private static float GetTotalBrushInkRecoveryBonus()
    {
        RewardBalanceConfig config = GetBalanceConfig();
        return GetStackCount(RewardType.BrushInkRecovery) * config.brushInkRecoveryBonusPerStack;
    }

    private static float GetTotalMoveSpeedBonus()
    {
        RewardBalanceConfig config = GetBalanceConfig();
        return GetStackCount(RewardType.MoveSpeed) * config.moveSpeedBonusPerStack;
    }

    private static RewardBalanceConfig GetBalanceConfig()
    {
        if (cachedBalanceConfig != null)
        {
            return cachedBalanceConfig;
        }

        RewardBalanceConfig config = new RewardBalanceConfig();
        TextAsset configAsset = Resources.Load<TextAsset>(BalanceConfigResourcePath);
        if (configAsset != null && !string.IsNullOrWhiteSpace(configAsset.text))
        {
            try
            {
                JsonUtility.FromJsonOverwrite(configAsset.text, config);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RewardSelectionSession] 读取奖励数值配置失败，将回退到默认值。资源路径: Resources/{BalanceConfigResourcePath}.json\n{exception.Message}");
            }
        }

        config.ClampValues();
        cachedBalanceConfig = config;
        return cachedBalanceConfig;
    }

    private static void Shuffle<T>(IList<T> values, System.Random random)
    {
        if (values == null || random == null)
        {
            return;
        }

        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            T temp = values[index];
            values[index] = values[swapIndex];
            values[swapIndex] = temp;
        }
    }
}