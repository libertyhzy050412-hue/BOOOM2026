using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BowWeapon : WeaponBase
{
    private const float FullChargeNormalizedToFire = 0.999f;
    private const float MinimumInkEpsilon = 0.0001f;
    private static readonly int IsAttackHash = Animator.StringToHash("IsAttack");

    [SerializeField] private BowArrowProjectile arrowPrefab;
    [SerializeField] private Transform arrowSpawnPoint;
    // [SerializeField] private Animator attackAnimator;
    // [SerializeField, Min(0.01f)] private float attackAnimatorTrueDuration = 0.18f;
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private string projectileRootName = "ProjectileRoot";
    [SerializeField] private Transform mapRoot;
    [SerializeField] private string mapRootName = "MapRoot";
    [SerializeField] private string revealMaskName = "WorldRevealMask";
    [SerializeField, Range(1f, 64f)] private float maskPixelsPerUnit = 16f;
    [SerializeField, Min(256)] private int maxMaskTextureSize = 2048;

    [Header("颜料设置")]
    [SerializeField, Min(0.01f)] private float maxInkAmount = 12f;
    [SerializeField, Min(0f)] private float inkCostPerWorldUnit = 1f;
    [SerializeField, Min(0f)] private float inkRecoveryDelay = 0.8f;
    [SerializeField, Min(0f), InspectorName("每秒恢复最大颜料百分比"), Tooltip("按最大颜料值的百分比恢复。0.25 表示每秒恢复最大颜料的 25%。")]
    private float inkRecoveryNormalizedPerSecond = 0.33333334f;

    [Header("蓄力设置")]
    [SerializeField, Min(0.05f)] private float maxChargeDuration = 1.1f;
    [FormerlySerializedAs("minimumChargeNormalizedToFire")]
    [SerializeField, HideInInspector] private float legacyMinimumChargeNormalizedToFire = 1f;

    [Header("满蓄发射属性")]
    [SerializeField, HideInInspector, Min(0f)] private float minArrowDamage = 10f;
    [SerializeField, Min(0f), InspectorName("满蓄箭矢伤害"), Tooltip("当前弓箭只能在蓄力完成后发射，因此实际使用这个伤害值。")]
    private float maxArrowDamage = 24f;
    [SerializeField, HideInInspector, Min(0.1f)] private float minArrowSpeed = 12f;
    [SerializeField, Min(0.1f), InspectorName("满蓄箭矢速度"), Tooltip("当前弓箭只能在蓄力完成后发射，因此实际使用这个速度值。")]
    private float maxArrowSpeed = 28f;
    [SerializeField, Min(0.1f)] private float arrowLifetime = 4f;
    [SerializeField, HideInInspector, Min(0.05f)] private float minRevealRadius = 1.1f;
    [SerializeField, Min(0.05f), InspectorName("满蓄显现半径"), Tooltip("当前弓箭只能在蓄力完成后发射，因此实际使用这个显现半径。")]
    private float maxRevealRadius = 2.4f;
    [SerializeField, Range(0.05f, 0.95f)] private float revealHardness = 0.62f;
    [FormerlySerializedAs("postFireCooldown")]
    [SerializeField, Min(0f)] private float attackCooldownSeconds = 0.35f;
    [SerializeField] private bool rotateTowardPointer = true;
    [SerializeField] private float visualAngleOffset;

    private float currentChargeTime;
    private float fireCooldownTimer;
    // private float attackAnimatorTimer;
    private float currentInkAmount;
    private float inkRecoveryDelayTimer;
    private bool wasPrimaryUseHeld;
    private bool hasLoggedMissingCamera;
    // private bool attackAnimatorHasIsAttack;
    private bool inkRecoveryActive;
    private bool rewardBaseInkStatsCaptured;
    private Vector2 lastAimDirection = Vector2.right;
    private float rewardBaseMaxInkAmount;
    private float rewardBaseInkRecoveryNormalizedPerSecond;

    public float ChargeNormalized => Mathf.Clamp01(currentChargeTime / Mathf.Max(maxChargeDuration, 0.0001f));
    public float MinimumChargeNormalizedToFire => 1f;
    public float AttackCooldownRemaining => fireCooldownTimer;
    public bool IsInAttackCooldown => fireCooldownTimer > 0.0001f;
    public bool IsFullyCharged => !IsInAttackCooldown && ChargeNormalized >= FullChargeNormalizedToFire;
    public bool IsCharging => !IsInAttackCooldown && !IsFullyCharged;
    public float CurrentInkAmount => currentInkAmount;
    public float MaxInkAmount => maxInkAmount;
    public float NormalizedInkAmount => maxInkAmount <= MinimumInkEpsilon ? 0f : Mathf.Clamp01(currentInkAmount / maxInkAmount);
    public bool InkRecoveryActive => inkRecoveryActive;
    public bool HasUsableInk => inkCostPerWorldUnit <= MinimumInkEpsilon || currentInkAmount > MinimumInkEpsilon;

    private List<Animator> validAttackAnimators = new List<Animator>();

    protected override void Awake()
    {
        base.Awake();
        CacheInkBaseStatsIfNeeded();
        currentInkAmount = maxInkAmount;
        inkRecoveryDelayTimer = inkRecoveryDelay;
        inkRecoveryActive = false;
        ResolveMapRoot();
        ResolveAttackAnimator();
        ApplyAttackAnimation(false);
    }

    protected override void Tick(float deltaTime)
    {
        ResolveAttackAnimator();
        UpdateInkRecovery(deltaTime);
        fireCooldownTimer = Mathf.Max(0f, fireCooldownTimer - deltaTime);

        if (TryGetPointerWorldPosition(out Vector3 pointerPosition))
        {
            hasLoggedMissingCamera = false;
            UpdateAim(pointerPosition);
        }
        else
        {
            LogMissingCameraOnce();
        }

        bool primaryUseHeld = IsPrimaryUseHeld();
        bool primaryUsePressed = primaryUseHeld && !wasPrimaryUseHeld;
        // if (IsInAttackCooldown)
        // {
        //     currentChargeTime = 0f;
        //     wasPrimaryUseHeld = primaryUseHeld;
        //     return;
        // }

        currentChargeTime = Mathf.Min(maxChargeDuration, currentChargeTime + Mathf.Max(deltaTime, 0f));
        
        //颜料是否恢复完毕
        bool isInkFull = currentInkAmount >= maxInkAmount - MinimumInkEpsilon;
        
        ApplyAttackAnimation(isInkFull);
        
        if (primaryUsePressed && isInkFull)
        {
            TryFireArrow();
            primaryUseHeld = false;
        }

        wasPrimaryUseHeld = primaryUseHeld;
    }

    protected override void OnWeaponDisabled()
    {
        currentChargeTime = 0f;
        fireCooldownTimer = 0f;
        // attackAnimatorTimer = 0f;
        wasPrimaryUseHeld = false;
        ApplyAttackAnimation(false);
    }

    protected override void OnOwnerChanged()
    {
        ResolveAttackAnimator();
        ApplyAttackAnimation(false);
    }

    private void Reset()
    {
        arrowSpawnPoint = transform;
        // attackAnimator = GetComponentInParent<Animator>();
        projectileRoot = null;
    }

    private void OnValidate()
    {
        maskPixelsPerUnit = Mathf.Clamp(maskPixelsPerUnit, 1f, 64f);
        maxMaskTextureSize = Mathf.Clamp(maxMaskTextureSize, 256, 4096);
        maxInkAmount = Mathf.Max(0.01f, maxInkAmount);
        inkCostPerWorldUnit = Mathf.Max(0f, inkCostPerWorldUnit);
        inkRecoveryDelay = Mathf.Max(0f, inkRecoveryDelay);
        inkRecoveryNormalizedPerSecond = Mathf.Max(0f, inkRecoveryNormalizedPerSecond);
        maxChargeDuration = Mathf.Max(0.05f, maxChargeDuration);
        // attackAnimatorTrueDuration = Mathf.Max(0.01f, attackAnimatorTrueDuration);
        legacyMinimumChargeNormalizedToFire = 1f;
        minArrowDamage = Mathf.Max(0f, minArrowDamage);
        maxArrowDamage = Mathf.Max(minArrowDamage, maxArrowDamage);
        minArrowSpeed = Mathf.Max(0.1f, minArrowSpeed);
        maxArrowSpeed = Mathf.Max(minArrowSpeed, maxArrowSpeed);
        arrowLifetime = Mathf.Max(0.1f, arrowLifetime);
        minRevealRadius = Mathf.Max(0.05f, minRevealRadius);
        maxRevealRadius = Mathf.Max(minRevealRadius, maxRevealRadius);
        revealHardness = Mathf.Clamp(revealHardness, 0.05f, 0.95f);
        attackCooldownSeconds = Mathf.Max(0f, attackCooldownSeconds);

        if (Application.isPlaying)
        {
            currentInkAmount = Mathf.Clamp(currentInkAmount, 0f, maxInkAmount);
            inkRecoveryDelayTimer = Mathf.Max(0f, inkRecoveryDelayTimer);
        }
    }

    private void UpdateAim(Vector3 pointerPosition)
    {
        Vector3 aimOrigin = arrowSpawnPoint != null ? arrowSpawnPoint.position : GetOwnerPositionOrSelf();
        Vector2 aimOffset = pointerPosition - aimOrigin;
        if (aimOffset.sqrMagnitude > 0.0001f)
        {
            lastAimDirection = aimOffset.normalized;
        }

        if (!rotateTowardPointer)
        {
            return;
        }

        float angle = Mathf.Atan2(lastAimDirection.y, lastAimDirection.x) * Mathf.Rad2Deg + visualAngleOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void TryFireArrow()
    {
        if (arrowPrefab == null || IsInAttackCooldown)
        {
            return;
        }

        // float chargeNormalized = ChargeNormalized;
        // if (chargeNormalized < FullChargeNormalizedToFire)
        // {
        //     return;
        // }

        float maxTravelDistance = maxInkAmount;
        if (maxTravelDistance <= MinimumInkEpsilon)
        {
            return;
        }

        Vector2 fireDirection = lastAimDirection.sqrMagnitude > 0.0001f ? lastAimDirection.normalized : Vector2.right;
        Transform spawnReference = arrowSpawnPoint != null ? arrowSpawnPoint : transform;
        Vector3 spawnPosition = spawnReference.position;
        float attackPowerMultiplier = Owner != null ? Mathf.Max(0f, Owner.AttackPowerPercent) * 0.01f : 1f;
        // TriggerAttackAnimation();
        AudioManager.PlayBowShoot();
        BowArrowProjectile arrowInstance = Instantiate(arrowPrefab, spawnPosition, Quaternion.identity, EnsureProjectileRoot());
        arrowInstance.Launch(
            Owner,
            fireDirection,
            maxArrowSpeed,
            maxArrowDamage * attackPowerMultiplier,
            arrowLifetime,
            maxRevealRadius,
            revealHardness,
            mapRoot,
            mapRootName,
            revealMaskName,
            maskPixelsPerUnit,
            maxMaskTextureSize,
            maxTravelDistance,
            null);
        
        // 发射后瞬间清空所有颜料，并激活恢复计时
        currentInkAmount = 0f;
        inkRecoveryActive = true;
        inkRecoveryDelayTimer = inkRecoveryDelay;

        fireCooldownTimer = attackCooldownSeconds;
        currentChargeTime = 0f;
    }

    // private void ResolveAttackAnimator()
    // {
    //     if (attackAnimator == null)
    //     {
    //         Player owner = Owner;
    //         if (owner != null)
    //         {
    //             Animator ownerAnimator = owner.GetComponent<Animator>();
    //             if (AnimatorHasBoolParameter(ownerAnimator, IsAttackHash))
    //             {
    //                 attackAnimator = ownerAnimator;
    //             }
    //             else
    //             {
    //                 Animator[] animators = owner.GetComponentsInChildren<Animator>(true);
    //                 for (int index = 0; index < animators.Length; index++)
    //                 {
    //                     if (AnimatorHasBoolParameter(animators[index], IsAttackHash))
    //                     {
    //                         attackAnimator = animators[index];
    //                         break;
    //                     }
    //                 }
    //             }
    //         }
    //         else
    //         {
    //             Animator[] animators = GetComponentsInChildren<Animator>(true);
    //             for (int index = 0; index < animators.Length; index++)
    //             {
    //                 if (AnimatorHasBoolParameter(animators[index], IsAttackHash))
    //                 {
    //                     attackAnimator = animators[index];
    //                     break;
    //                 }
    //             }
    //         }
    //     }
    //
    //     attackAnimatorHasIsAttack = AnimatorHasBoolParameter(attackAnimator, IsAttackHash);
    // }

    private void ResolveAttackAnimator()
    {
        validAttackAnimators.Clear();
        Player owner = Owner;
        Animator[] animatorsToSearch;

        if (owner != null)
        {
            animatorsToSearch = owner.GetComponentsInChildren<Animator>(true);
        }
        else
        {
            animatorsToSearch = GetComponentsInChildren<Animator>(true);
        }

        for (int i = 0; i < animatorsToSearch.Length; i++)
        {
            if (AnimatorHasBoolParameter(animatorsToSearch[i], IsAttackHash))
            {
                validAttackAnimators.Add(animatorsToSearch[i]);
            }
        }
        
    }

    // private void TriggerAttackAnimation()
    // {
    //     if (!attackAnimatorHasIsAttack)
    //     {
    //         return;
    //     }
    //
    //     attackAnimatorTimer = Mathf.Max(attackAnimatorTrueDuration, 0.01f);
    //     ApplyAttackAnimation(true);
    // }
    //
    // private void UpdateAttackAnimation(float deltaTime)
    // {
    //     if (!attackAnimatorHasIsAttack || attackAnimatorTimer <= 0f)
    //     {
    //         return;
    //     }
    //
    //     attackAnimatorTimer = Mathf.Max(0f, attackAnimatorTimer - Mathf.Max(deltaTime, 0f));
    //     if (attackAnimatorTimer <= 0f)
    //     {
    //         ApplyAttackAnimation(false);
    //     }
    // }
    //
    // private void ApplyAttackAnimation(bool isAttacking)
    // {
    //     if (!attackAnimatorHasIsAttack)
    //     {
    //         return;
    //     }
    //
    //     attackAnimator.SetBool(IsAttackHash, isAttacking);
    // }

    private void ApplyAttackAnimation(bool isAttacking)
    {
        for (int i = 0; i < validAttackAnimators.Count; i++)
        {
            if (validAttackAnimators[i] != null)
            {
                validAttackAnimators[i].SetBool(IsAttackHash, isAttacking);
            }
        }
    }
    

    public void ApplyRuntimeRewardModifiers(float maxInkBonus, float inkRecoveryNormalizedBonus)
    {
        CacheInkBaseStatsIfNeeded();

        float previousMaxInkAmount = Mathf.Max(maxInkAmount, MinimumInkEpsilon);
        float inkRatio = Mathf.Clamp01(currentInkAmount / previousMaxInkAmount);

        maxInkAmount = Mathf.Max(0.01f, rewardBaseMaxInkAmount + maxInkBonus);
        inkRecoveryNormalizedPerSecond = Mathf.Max(0f, rewardBaseInkRecoveryNormalizedPerSecond + inkRecoveryNormalizedBonus);
        currentInkAmount = Mathf.Clamp(maxInkAmount * inkRatio, 0f, maxInkAmount);
    }

    private void CacheInkBaseStatsIfNeeded()
    {
        if (rewardBaseInkStatsCaptured)
        {
            return;
        }

        rewardBaseMaxInkAmount = maxInkAmount;
        rewardBaseInkRecoveryNormalizedPerSecond = inkRecoveryNormalizedPerSecond;
        rewardBaseInkStatsCaptured = true;
    }

    private void UpdateInkRecovery(float deltaTime)
    {
        if (currentInkAmount < maxInkAmount - MinimumInkEpsilon)
        {
            inkRecoveryActive = true;
        }

        if (!inkRecoveryActive)
        {
            inkRecoveryDelayTimer = inkRecoveryDelay;
            return;
        }

        if (currentInkAmount >= maxInkAmount - MinimumInkEpsilon)
        {
            currentInkAmount = maxInkAmount;
            inkRecoveryActive = false;
            inkRecoveryDelayTimer = inkRecoveryDelay;
            return;
        }

        if (inkRecoveryDelayTimer > 0f)
        {
            inkRecoveryDelayTimer = Mathf.Max(0f, inkRecoveryDelayTimer - deltaTime);
            return;
        }

        currentInkAmount = Mathf.Min(maxInkAmount, currentInkAmount + maxInkAmount * inkRecoveryNormalizedPerSecond * deltaTime);
        if (currentInkAmount >= maxInkAmount - MinimumInkEpsilon)
        {
            currentInkAmount = maxInkAmount;
            inkRecoveryActive = false;
            inkRecoveryDelayTimer = inkRecoveryDelay;
        }
    }

    private void ConsumeInkForDistance(float travelledDistance)
    {
        if (travelledDistance <= MinimumInkEpsilon || inkCostPerWorldUnit <= MinimumInkEpsilon)
        {
            return;
        }

        currentInkAmount = Mathf.Max(0f, currentInkAmount - travelledDistance * inkCostPerWorldUnit);
        inkRecoveryActive = true;
        inkRecoveryDelayTimer = inkRecoveryDelay;
    }

    private float GetAvailableTravelDistance()
    {
        if (inkCostPerWorldUnit <= MinimumInkEpsilon)
        {
            return float.PositiveInfinity;
        }

        return currentInkAmount / inkCostPerWorldUnit;
    }

    private static bool AnimatorHasBoolParameter(Animator animator, int parameterHash)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int index = 0; index < parameters.Length; index++)
        {
            AnimatorControllerParameter parameter = parameters[index];
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == parameterHash)
            {
                return true;
            }
        }

        return false;
    }

    private Transform EnsureProjectileRoot()
    {
        if (projectileRoot != null)
        {
            return projectileRoot;
        }

        string rootName = string.IsNullOrWhiteSpace(projectileRootName) ? "ProjectileRoot" : projectileRootName;
        GameObject rootObject = GameObject.Find(rootName);
        if (rootObject == null)
        {
            rootObject = new GameObject(rootName);
        }

        projectileRoot = rootObject.transform;
        return projectileRoot;
    }

    private void ResolveMapRoot()
    {
        if (mapRoot != null)
        {
            return;
        }

        string rootName = string.IsNullOrWhiteSpace(mapRootName) ? "MapRoot" : mapRootName;
        GameObject rootObject = GameObject.Find(rootName);
        if (rootObject != null)
        {
            mapRoot = rootObject.transform;
        }
    }

    private void LogMissingCameraOnce()
    {
        if (hasLoggedMissingCamera)
        {
            return;
        }

        hasLoggedMissingCamera = true;
        Debug.LogWarning("[BowWeapon] 未找到可用相机，无法计算鼠标世界坐标。", this);
    }
}