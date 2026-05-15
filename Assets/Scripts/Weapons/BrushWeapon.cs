using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BrushWeapon : WeaponBase
{
    
    //动画
    [Header("动画替换")]
    [SerializeField] private RuntimeAnimatorController brushAnimatorOverride; 
    
    
    private const float MinimumInkEpsilon = 0.0001f;

    [SerializeField] private Transform mapRoot;
    [SerializeField] private string mapRootName = "MapRoot";
    [SerializeField] private string revealMaskName = "WorldRevealMask";
    [SerializeField, Range(0f, 1f)] private float brushStrength = 1f;
    [SerializeField, Min(0.05f)] private float brushMinRadius = 0.42f;
    [SerializeField, Min(0.05f)] private float brushMaxRadius = 1.15f;
    [SerializeField, Min(0.05f)] private float brushStationaryMaxRadius = 2.15f;
    [SerializeField, Range(0.05f, 0.95f)] private float brushHardness = 0.56f;
    [SerializeField, Range(0.1f, 0.9f)] private float brushSpacingRatio = 0.28f;
    [SerializeField, Min(0f)] private float brushSpreadPerSecond = 1.05f;
    [SerializeField, Min(0f)] private float brushThinSpeedStart = 2f;
    [SerializeField, Min(0.01f)] private float brushThinSpeedEnd = 15f;
    [SerializeField, Min(0.01f)] private float brushStartExpandDuration = 0.16f;
    [SerializeField, Min(0f)] private float brushStartToSpeedBlendDuration = 0.1f;
    [SerializeField, Range(0.15f, 1f)] private float fastMoveRadiusFactor = 0.36f;
    [SerializeField, Min(0.1f)] private float cursorSpeedSmoothing = 14f;
    [SerializeField, Min(0.01f)] private float stationaryPaintInterval = 0.055f;
    [SerializeField, Min(0f)] private float maxDistanceFromOwner;
    [SerializeField] private bool showRangeWhilePainting = true;
    [SerializeField] private bool showRangeWhileIdle = true;
    [SerializeField] private Color rangeRingColor = new Color(1f, 0.86f, 0.12f, 0.95f);
    [SerializeField, Range(0.03f, 0.35f)] private float rangeRingThickness = 0.12f;
    [SerializeField, Min(32)] private int generatedSpriteResolution = 128;
    [SerializeField, Range(1f, 64f)] private float maskPixelsPerUnit = 16f;
    [SerializeField, Min(256)] private int maxMaskTextureSize = 2048;
    [SerializeField, Min(0.01f)] private float maxInkAmount = 12f;
    [SerializeField, Min(0f)] private float inkCostPerWorldUnit = 1f;
    [SerializeField, Min(0f)] private float inkRecoveryDelay = 0.8f;
    [FormerlySerializedAs("inkRecoveryPerSecond")]
    [SerializeField, Min(0f), InspectorName("每秒恢复最大颜料百分比"), Tooltip("按最大颜料值的百分比恢复。0.25 表示每秒恢复最大颜料的 25%。")]
    private float inkRecoveryNormalizedPerSecond = 0.33333334f;
    [SerializeField, Range(0f, 1f)] private float depletedInkResumeNormalized = 0.12f;
    [SerializeField] private bool enableSummonSpawning = true;
    [SerializeField] private SummonBase summonOnePrefab;
    [SerializeField] private Transform summonRoot;
    [SerializeField] private string summonRootName = "SummonRoot";
    [SerializeField, Min(0.01f)] private float summonSpawnInterval = 2.5f;
    [SerializeField, Min(0)] private int maxActiveSummons = 4;
    [SerializeField, Min(0f)] private float summonMinDistanceFromOwner = 1.5f;
    [SerializeField, Min(0f)] private float summonMaxDistanceFromOwner = 12f;
    [SerializeField, Min(0f)] private float summonMinSpacing = 0.75f;
    [SerializeField, Range(0.01f, 1f)] private float summonRevealAlphaThreshold = 0.12f;
    [SerializeField, Min(1)] private int summonSpawnSampleAttempts = 24;

    private SpriteRenderer rangeIndicator;
    private Sprite generatedRingSprite;
    private Texture2D generatedRingTexture;
    private WorldRevealMaskController revealMaskController;
    private readonly List<SummonBase> activeSummons = new List<SummonBase>();
    private bool strokeActive;
    private bool hasLoggedMissingCamera;
    private Vector3 lastCursorPosition;
    private Vector3 lastPaintPosition;
    private float lastPaintRadius;
    private float strokeElapsedTime;
    private float smoothedCursorSpeed;
    private float stationaryTime;
    private float stationaryPaintTimer;
    private float currentVisualRadius;
    private float summonSpawnTimer;
    private float currentInkAmount;
    private float inkRecoveryDelayTimer;
    private bool inkRecoveryActive;
    [SerializeField, HideInInspector] private bool inkRecoveryValueMigrated;
    private bool rewardBaseStatsCaptured;
    private float rewardBaseMaxInkAmount;
    private float rewardBaseInkRecoveryNormalizedPerSecond;

    public float CurrentInkAmount => currentInkAmount;
    public float MaxInkAmount => maxInkAmount;
    public float NormalizedInkAmount => maxInkAmount <= MinimumInkEpsilon ? 0f : Mathf.Clamp01(currentInkAmount / maxInkAmount);
    public bool InkRecoveryActive => inkRecoveryActive;

    protected override void Awake()
    {
        base.Awake();
        MigrateLegacyInkRecoveryIfNeeded();
        CacheRewardBaseStatsIfNeeded();
        currentInkAmount = maxInkAmount;
        inkRecoveryDelayTimer = inkRecoveryDelay;
        inkRecoveryActive = false;
        EnsureRangeIndicator();
        EnsureGeneratedRing();
        EnsureRevealMaskController();
        EnsureSummonRoot();
        UpdateBrushVisualScale(EvaluateEffectiveMaxRadius());
        UpdateRangeIndicatorVisibility(false, false);
    }
    
    
    protected override void OnOwnerChanged()
    {
        base.OnOwnerChanged();
        ApplyBrushAnimation();
    }

    private void ApplyBrushAnimation()
    {
        if (Owner == null || brushAnimatorOverride == null) return;

        // 获取玩家身上的 Animator
        Animator playerAnimator = Owner.GetComponentInChildren<Animator>();
        if (playerAnimator != null)
        {
            playerAnimator.runtimeAnimatorController = brushAnimatorOverride;
        }
    }
    

    protected override void Tick(float deltaTime)
    {
        UpdateSummonSpawning(deltaTime);
        EnsureRangeIndicator();
        EnsureGeneratedRing();

        bool useHeld = IsPrimaryUseHeld();
        UpdateInkRecovery(deltaTime, useHeld);

        if (!TryGetPointerWorldPosition(out Vector3 pointerPosition))
        {
            AudioManager.SetBrushAttackLoopActive(false);
            UpdateRangeIndicatorVisibility(false, false);
            LogMissingCameraOnce();
            return;
        }

        hasLoggedMissingCamera = false;
        pointerPosition = ClampPointerDistance(pointerPosition);
        transform.position = pointerPosition;

        float visualRadius = EvaluateEffectiveMaxRadius();

        if (!useHeld)
        {
            AudioManager.SetBrushAttackLoopActive(false);
            UpdateBrushVisualScale(visualRadius);
            UpdateRangeIndicatorVisibility(false, true);
            ResetStrokeState();
            FlushRevealMask();
            return;
        }

        if (!CanPaintWithInk())
        {
            AudioManager.SetBrushAttackLoopActive(false);
            BreakStrokeContinuity(pointerPosition, visualRadius);
            UpdateBrushVisualScale(visualRadius);
            UpdateRangeIndicatorVisibility(true, true);
            FlushRevealMask();
            return;
        }

        if (!strokeActive)
        {
            StartStroke(pointerPosition);
            UpdateBrushVisualScale(currentVisualRadius);
            UpdateRangeIndicatorVisibility(true, true);
            FlushRevealMask();
            return;
        }

        float safeDeltaTime = Mathf.Max(deltaTime, 0.0001f);
        strokeElapsedTime += safeDeltaTime;
        float cursorDistance = Vector2.Distance(pointerPosition, lastCursorPosition);
        float rawCursorSpeed = cursorDistance / safeDeltaTime;
        float speedLerp = 1f - Mathf.Exp(-cursorSpeedSmoothing * safeDeltaTime);
        smoothedCursorSpeed = Mathf.Lerp(smoothedCursorSpeed, rawCursorSpeed, speedLerp);

        float targetRadius = EvaluateStrokeRadius(smoothedCursorSpeed);
        float stationaryThreshold = Mathf.Max(targetRadius * brushSpacingRatio, 0.02f);

        if (cursorDistance <= stationaryThreshold)
        {
            stationaryTime += safeDeltaTime;
            stationaryPaintTimer += safeDeltaTime;
            visualRadius = targetRadius;

            if (stationaryPaintTimer >= stationaryPaintInterval)
            {
                float requestedPaintDistance = Vector2.Distance(lastPaintPosition, pointerPosition);
                float allowedPaintDistance = GetAllowedPaintDistance(requestedPaintDistance);

                if (requestedPaintDistance > MinimumInkEpsilon && allowedPaintDistance > MinimumInkEpsilon)
                {
                    float paintRatio = Mathf.Clamp01(allowedPaintDistance / requestedPaintDistance);
                    Vector2 paintedEndPosition = Vector2.Lerp(lastPaintPosition, pointerPosition, paintRatio);
                    float paintedEndRadius = Mathf.Lerp(lastPaintRadius, visualRadius, paintRatio);
                    MarkInkUseActive();
                    RevealStroke(lastPaintPosition, paintedEndPosition, lastPaintRadius, paintedEndRadius);
                    ConsumeInkForDistance(allowedPaintDistance);
                    lastPaintPosition = paintedEndPosition;
                    lastPaintRadius = paintedEndRadius;
                }
                else
                {
                    RevealStamp(pointerPosition, visualRadius);
                    lastPaintPosition = pointerPosition;
                    lastPaintRadius = visualRadius;
                }

                stationaryPaintTimer = 0f;
            }
        }
        else
        {
            stationaryTime = 0f;
            stationaryPaintTimer = 0f;
            float requestedPaintDistance = Vector2.Distance(lastPaintPosition, pointerPosition);
            float allowedPaintDistance = GetAllowedPaintDistance(requestedPaintDistance);

            if (allowedPaintDistance > MinimumInkEpsilon)
            {
                float paintRatio = requestedPaintDistance <= MinimumInkEpsilon ? 1f : Mathf.Clamp01(allowedPaintDistance / requestedPaintDistance);
                Vector2 paintedEndPosition = Vector2.Lerp(lastPaintPosition, pointerPosition, paintRatio);
                float paintedEndRadius = Mathf.Lerp(lastPaintRadius, targetRadius, paintRatio);
                MarkInkUseActive();
                RevealStroke(lastPaintPosition, paintedEndPosition, lastPaintRadius, paintedEndRadius);
                ConsumeInkForDistance(allowedPaintDistance);
            }

            lastPaintPosition = pointerPosition;
            lastPaintRadius = targetRadius;
            visualRadius = targetRadius;
        }

        currentVisualRadius = visualRadius;
        UpdateBrushVisualScale(visualRadius);
        UpdateRangeIndicatorVisibility(true, true);
        lastCursorPosition = pointerPosition;
        FlushRevealMask();
    }

    protected override void OnWeaponDisabled()
    {
        AudioManager.SetBrushAttackLoopActive(false);
        ResetStrokeState();
        summonSpawnTimer = 0f;
        UpdateRangeIndicatorVisibility(false, false);
        FlushRevealMask();
    }

    private void OnDestroy()
    {
        DestroyGeneratedRing();
    }

    private void OnValidate()
    {
        MigrateLegacyInkRecoveryIfNeeded();
        brushStrength = Mathf.Clamp01(brushStrength);
        brushMinRadius = Mathf.Max(0.05f, brushMinRadius);
        brushMaxRadius = Mathf.Max(brushMinRadius, brushMaxRadius);
        brushStationaryMaxRadius = Mathf.Max(brushMaxRadius, brushStationaryMaxRadius);
        brushHardness = Mathf.Clamp(brushHardness, 0.05f, 0.95f);
        brushSpacingRatio = Mathf.Clamp(brushSpacingRatio, 0.1f, 0.9f);
        brushSpreadPerSecond = Mathf.Max(0f, brushSpreadPerSecond);
        brushThinSpeedStart = Mathf.Max(0f, brushThinSpeedStart);
        brushThinSpeedEnd = Mathf.Max(brushThinSpeedStart + 0.01f, brushThinSpeedEnd);
        brushStartExpandDuration = Mathf.Max(0.01f, brushStartExpandDuration);
        brushStartToSpeedBlendDuration = Mathf.Max(0f, brushStartToSpeedBlendDuration);
        fastMoveRadiusFactor = Mathf.Clamp(fastMoveRadiusFactor, 0.15f, 1f);
        cursorSpeedSmoothing = Mathf.Max(0.1f, cursorSpeedSmoothing);
        stationaryPaintInterval = Mathf.Max(0.01f, stationaryPaintInterval);
        maxDistanceFromOwner = Mathf.Max(0f, maxDistanceFromOwner);
        rangeRingThickness = Mathf.Clamp(rangeRingThickness, 0.03f, 0.35f);
        generatedSpriteResolution = Mathf.Clamp(generatedSpriteResolution, 32, 512);
        maskPixelsPerUnit = Mathf.Clamp(maskPixelsPerUnit, 1f, 64f);
        maxMaskTextureSize = Mathf.Clamp(maxMaskTextureSize, 256, 4096);
        maxInkAmount = Mathf.Max(0.01f, maxInkAmount);
        inkCostPerWorldUnit = Mathf.Max(0f, inkCostPerWorldUnit);
        inkRecoveryDelay = Mathf.Max(0f, inkRecoveryDelay);
        inkRecoveryNormalizedPerSecond = Mathf.Max(0f, inkRecoveryNormalizedPerSecond);
        depletedInkResumeNormalized = Mathf.Clamp01(depletedInkResumeNormalized);
        summonSpawnInterval = Mathf.Max(0.01f, summonSpawnInterval);
        maxActiveSummons = Mathf.Max(0, maxActiveSummons);
        summonMinDistanceFromOwner = Mathf.Max(0f, summonMinDistanceFromOwner);
        summonMaxDistanceFromOwner = Mathf.Max(summonMinDistanceFromOwner, summonMaxDistanceFromOwner);
        summonMinSpacing = Mathf.Max(0f, summonMinSpacing);
        summonRevealAlphaThreshold = Mathf.Clamp01(summonRevealAlphaThreshold);
        summonSpawnSampleAttempts = Mathf.Max(1, summonSpawnSampleAttempts);

        if (Application.isPlaying)
        {
            currentInkAmount = Mathf.Clamp(currentInkAmount, 0f, maxInkAmount);
            inkRecoveryDelayTimer = Mathf.Max(0f, inkRecoveryDelayTimer);
            RebuildGeneratedRing();
            EnsureRevealMaskController();
            UpdateBrushVisualScale(currentVisualRadius > 0f ? currentVisualRadius : EvaluateEffectiveMaxRadius());
        }
    }

    private void MigrateLegacyInkRecoveryIfNeeded()
    {
        if (inkRecoveryValueMigrated)
        {
            return;
        }

        if (inkRecoveryNormalizedPerSecond > 1f && maxInkAmount > MinimumInkEpsilon)
        {
            inkRecoveryNormalizedPerSecond /= maxInkAmount;
        }

        inkRecoveryValueMigrated = true;
    }

    private void CacheRewardBaseStatsIfNeeded()
    {
        if (rewardBaseStatsCaptured)
        {
            return;
        }

        rewardBaseMaxInkAmount = maxInkAmount;
        rewardBaseInkRecoveryNormalizedPerSecond = inkRecoveryNormalizedPerSecond;
        rewardBaseStatsCaptured = true;
    }

    public void SetBrushStrength(float normalizedStrength)
    {
        brushStrength = Mathf.Clamp01(normalizedStrength);
        if (!strokeActive)
        {
            UpdateBrushVisualScale(EvaluateEffectiveMaxRadius());
        }
    }

    public void ApplyRuntimeRewardModifiers(float maxInkBonus, float inkRecoveryNormalizedBonus)
    {
        CacheRewardBaseStatsIfNeeded();

        float previousMaxInkAmount = Mathf.Max(maxInkAmount, MinimumInkEpsilon);
        float inkRatio = Mathf.Clamp01(currentInkAmount / previousMaxInkAmount);

        maxInkAmount = Mathf.Max(0.01f, rewardBaseMaxInkAmount + maxInkBonus);
        inkRecoveryNormalizedPerSecond = Mathf.Max(0f, rewardBaseInkRecoveryNormalizedPerSecond + inkRecoveryNormalizedBonus);
        currentInkAmount = Mathf.Clamp(maxInkAmount * inkRatio, 0f, maxInkAmount);
    }

    [ContextMenu("Clear Reveal Mask")]
    public void ClearRevealMask()
    {
        if (EnsureRevealMaskController())
        {
            revealMaskController.ClearMask();
        }
    }

    private void StartStroke(Vector3 pointerPosition)
    {
        AudioManager.SetBrushAttackLoopActive(true);
        strokeActive = true;
        strokeElapsedTime = 0f;
        smoothedCursorSpeed = 0f;
        lastCursorPosition = pointerPosition;
        lastPaintPosition = pointerPosition;
        lastPaintRadius = EvaluateEffectiveMinRadius();
        stationaryTime = 0f;
        stationaryPaintTimer = 0f;
        currentVisualRadius = lastPaintRadius;
        MarkInkUseActive();
        RevealStamp(pointerPosition, lastPaintRadius);
    }

    private void ResetStrokeState()
    {
        AudioManager.SetBrushAttackLoopActive(false);
        strokeActive = false;
        strokeElapsedTime = 0f;
        smoothedCursorSpeed = 0f;
        stationaryTime = 0f;
        stationaryPaintTimer = 0f;
        lastPaintRadius = EvaluateEffectiveMaxRadius();
        currentVisualRadius = lastPaintRadius;
    }

    private float EvaluateEffectiveMaxRadius()
    {
        return Mathf.Lerp(brushMinRadius, brushMaxRadius, brushStrength);
    }

    private float EvaluateEffectiveMinRadius()
    {
        float maxRadius = EvaluateEffectiveMaxRadius();
        float minRatio = Mathf.Approximately(brushMaxRadius, 0f) ? 1f : brushMinRadius / brushMaxRadius;
        return Mathf.Max(0.05f, maxRadius * minRatio);
    }

    private float EvaluateEffectiveStationaryMaxRadius()
    {
        float maxRadius = EvaluateEffectiveMaxRadius();
        float stationaryRatio = Mathf.Approximately(brushMaxRadius, 0f) ? 1f : brushStationaryMaxRadius / brushMaxRadius;
        return Mathf.Max(maxRadius, maxRadius * stationaryRatio);
    }

    private float EvaluateStrokeRadius(float cursorSpeed)
    {
        float maxRadius = EvaluateEffectiveMaxRadius();
        float minRadius = EvaluateEffectiveMinRadius();
        float thinAmount = Mathf.InverseLerp(brushThinSpeedStart, brushThinSpeedEnd, cursorSpeed);
        float speedControlledRadius = Mathf.Lerp(maxRadius, Mathf.Max(minRadius, maxRadius * fastMoveRadiusFactor), thinAmount);
        speedControlledRadius = Mathf.Clamp(speedControlledRadius, minRadius, maxRadius);

        float startExpandProgress = Mathf.Clamp01(strokeElapsedTime / brushStartExpandDuration);
        float easedExpandProgress = Mathf.SmoothStep(0f, 1f, startExpandProgress);
        float startExpandRadius = Mathf.Lerp(minRadius, maxRadius, easedExpandProgress);

        if (brushStartToSpeedBlendDuration <= 0f)
        {
            return strokeElapsedTime < brushStartExpandDuration ? startExpandRadius : speedControlledRadius;
        }

        float speedBlendProgress = Mathf.Clamp01((strokeElapsedTime - brushStartExpandDuration) / brushStartToSpeedBlendDuration);
        return Mathf.Lerp(startExpandRadius, speedControlledRadius, speedBlendProgress);
    }

    private bool CanPaintWithInk()
    {
        if (currentInkAmount <= MinimumInkEpsilon)
        {
            return false;
        }

        if (!inkRecoveryActive)
        {
            return true;
        }

        return currentInkAmount >= GetInkResumeAmount();
    }

    private float GetAllowedPaintDistance(float requestedDistance)
    {
        if (requestedDistance <= MinimumInkEpsilon)
        {
            return 0f;
        }

        if (inkCostPerWorldUnit <= MinimumInkEpsilon)
        {
            return requestedDistance;
        }

        return Mathf.Min(requestedDistance, currentInkAmount / inkCostPerWorldUnit);
    }

    private void ConsumeInkForDistance(float paintedDistance)
    {
        if (paintedDistance <= MinimumInkEpsilon || inkCostPerWorldUnit <= MinimumInkEpsilon)
        {
            return;
        }

        currentInkAmount = Mathf.Max(0f, currentInkAmount - paintedDistance * inkCostPerWorldUnit);
        inkRecoveryDelayTimer = inkRecoveryDelay;
        if (currentInkAmount <= MinimumInkEpsilon)
        {
            currentInkAmount = 0f;
            inkRecoveryActive = true;
        }
    }

    private void UpdateInkRecovery(float deltaTime, bool useHeld)
    {
        if (!useHeld && currentInkAmount < maxInkAmount - MinimumInkEpsilon)
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

    private float GetInkResumeAmount()
    {
        return Mathf.Clamp01(depletedInkResumeNormalized) * maxInkAmount;
    }

    private void MarkInkUseActive()
    {
        inkRecoveryActive = false;
        inkRecoveryDelayTimer = inkRecoveryDelay;
    }

    private void BreakStrokeContinuity(Vector3 pointerPosition, float visualRadius)
    {
        AudioManager.SetBrushAttackLoopActive(false);
        strokeActive = false;
        strokeElapsedTime = 0f;
        smoothedCursorSpeed = 0f;
        stationaryTime = 0f;
        stationaryPaintTimer = 0f;
        lastCursorPosition = pointerPosition;
        lastPaintPosition = pointerPosition;
        lastPaintRadius = visualRadius;
        currentVisualRadius = visualRadius;
    }

    private void RevealStroke(Vector2 from, Vector2 to, float fromRadius, float toRadius)
    {
        if (!EnsureRevealMaskController())
        {
            return;
        }

        revealMaskController.RevealStroke(from, to, fromRadius, toRadius, brushHardness, brushSpacingRatio);
    }

    private void RevealStamp(Vector2 center, float radius)
    {
        if (!EnsureRevealMaskController())
        {
            return;
        }

        revealMaskController.RevealStamp(center, radius, brushHardness);
    }

    private void FlushRevealMask()
    {
        if (revealMaskController != null)
        {
            revealMaskController.Flush();
        }
    }

    private void UpdateSummonSpawning(float deltaTime)
    {
        if (!enableSummonSpawning || summonOnePrefab == null)
        {
            return;
        }

        CleanupActiveSummons();
        if (maxActiveSummons > 0 && activeSummons.Count >= maxActiveSummons)
        {
            summonSpawnTimer = 0f;
            return;
        }

        summonSpawnTimer += deltaTime;
        if (summonSpawnTimer < summonSpawnInterval)
        {
            return;
        }

        if (!TryGetSummonSpawnPosition(out Vector3 spawnPosition))
        {
            summonSpawnTimer = summonSpawnInterval;
            return;
        }

        SpawnSummon(spawnPosition);
        summonSpawnTimer = 0f;
    }

    private bool TryGetSummonSpawnPosition(out Vector3 spawnPosition)
    {
        spawnPosition = default;
        if (!EnsureRevealMaskController())
        {
            return false;
        }

        Vector2 ownerPosition = GetOwnerPositionOrSelf();
        float minDistanceSqr = summonMinDistanceFromOwner * summonMinDistanceFromOwner;
        float maxDistanceSqr = summonMaxDistanceFromOwner <= 0f ? float.PositiveInfinity : summonMaxDistanceFromOwner * summonMaxDistanceFromOwner;
        int totalAttempts = Mathf.Max(1, summonSpawnSampleAttempts);

        for (int attempt = 0; attempt < totalAttempts; attempt++)
        {
            if (!revealMaskController.TryGetRandomRevealedPosition(out Vector3 candidate, summonRevealAlphaThreshold, totalAttempts))
            {
                return false;
            }

            float distanceSqr = ((Vector2)candidate - ownerPosition).sqrMagnitude;
            if (distanceSqr < minDistanceSqr || distanceSqr > maxDistanceSqr)
            {
                continue;
            }

            if (summonMinSpacing > 0f && !HasEnoughSpacing(candidate))
            {
                continue;
            }

            spawnPosition = candidate;
            spawnPosition.z = 0f;
            return true;
        }

        return false;
    }

    private bool HasEnoughSpacing(Vector3 candidate)
    {
        float minSpacingSqr = summonMinSpacing * summonMinSpacing;
        for (int index = 0; index < activeSummons.Count; index++)
        {
            SummonBase summon = activeSummons[index];
            if (summon == null)
            {
                continue;
            }

            if (((Vector2)(summon.transform.position - candidate)).sqrMagnitude < minSpacingSqr)
            {
                return false;
            }
        }

        return true;
    }

    private void SpawnSummon(Vector3 spawnPosition)
    {
        SummonBase summonInstance = Instantiate(summonOnePrefab, spawnPosition, Quaternion.identity, EnsureSummonRoot());
        summonInstance.SetOwner(Owner);
        activeSummons.Add(summonInstance);
    }

    private void CleanupActiveSummons()
    {
        for (int index = activeSummons.Count - 1; index >= 0; index--)
        {
            if (activeSummons[index] == null)
            {
                activeSummons.RemoveAt(index);
            }
        }
    }

    private bool EnsureRevealMaskController()
    {
        ResolveMapRoot();
        revealMaskController = WorldRevealMaskController.GetOrCreate(
            mapRoot,
            mapRootName,
            revealMaskName,
            maskPixelsPerUnit,
            maxMaskTextureSize);
        return revealMaskController != null;
    }

    private Transform EnsureSummonRoot()
    {
        if (summonRoot != null)
        {
            return summonRoot;
        }

        string rootName = string.IsNullOrWhiteSpace(summonRootName) ? "SummonRoot" : summonRootName;
        GameObject rootObject = GameObject.Find(rootName);
        if (rootObject == null)
        {
            rootObject = new GameObject(rootName);
        }

        summonRoot = rootObject.transform;
        return summonRoot;
    }

    private void EnsureRangeIndicator()
    {
        if (rangeIndicator == null)
        {
            Transform existing = transform.Find("BrushRangeIndicator");
            if (existing != null)
            {
                rangeIndicator = existing.GetComponent<SpriteRenderer>();
                if (rangeIndicator == null)
                {
                    rangeIndicator = existing.gameObject.AddComponent<SpriteRenderer>();
                }
            }
            else
            {
                GameObject ringObject = new GameObject("BrushRangeIndicator");
                ringObject.transform.SetParent(transform, false);
                rangeIndicator = ringObject.AddComponent<SpriteRenderer>();
            }
        }

        rangeIndicator.transform.SetParent(transform, false);
        rangeIndicator.transform.localPosition = Vector3.zero;
        rangeIndicator.transform.localRotation = Quaternion.identity;
        rangeIndicator.color = rangeRingColor;
        rangeIndicator.maskInteraction = SpriteMaskInteraction.None;
        rangeIndicator.sortingOrder = 1000;
    }

    private void EnsureGeneratedRing()
    {
        if (generatedRingSprite == null)
        {
            RebuildGeneratedRing();
        }

        if (rangeIndicator != null)
        {
            rangeIndicator.sprite = generatedRingSprite;
            rangeIndicator.color = rangeRingColor;
        }
    }

    private void RebuildGeneratedRing()
    {
        DestroyGeneratedRing();
        generatedRingTexture = BuildRingTexture(generatedSpriteResolution, rangeRingThickness);
        generatedRingSprite = Sprite.Create(
            generatedRingTexture,
            new Rect(0f, 0f, generatedRingTexture.width, generatedRingTexture.height),
            new Vector2(0.5f, 0.5f),
            generatedRingTexture.width);
        generatedRingSprite.name = "BrushRangeRingRuntime";
        generatedRingSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private void UpdateRangeIndicatorVisibility(bool useHeld, bool hasPointer)
    {
        if (rangeIndicator == null)
        {
            return;
        }

        rangeIndicator.enabled = hasPointer && (useHeld ? showRangeWhilePainting : showRangeWhileIdle);
    }

    private void UpdateBrushVisualScale(float radius)
    {
        currentVisualRadius = Mathf.Max(radius, 0.05f);
        if (rangeIndicator != null)
        {
            rangeIndicator.transform.localScale = CalculateBrushScale(currentVisualRadius);
        }
    }

    private Vector3 ClampPointerDistance(Vector3 pointerPosition)
    {
        if (maxDistanceFromOwner <= 0f)
        {
            return pointerPosition;
        }

        Vector3 ownerPosition = GetOwnerPositionOrSelf();
        Vector2 offset = pointerPosition - ownerPosition;
        if (offset.sqrMagnitude <= maxDistanceFromOwner * maxDistanceFromOwner)
        {
            return pointerPosition;
        }

        Vector2 clampedOffset = offset.normalized * maxDistanceFromOwner;
        return ownerPosition + (Vector3)clampedOffset;
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
        Debug.LogWarning("[BrushWeapon] 未找到可用相机，无法计算鼠标世界坐标。请确认正在游戏场景中运行，并且存在启用中的相机。", this);
    }

    private static Vector3 CalculateBrushScale(float radius)
    {
        return Vector3.one * Mathf.Max(radius * 2f, 0.01f);
    }

    private static Texture2D BuildRingTexture(int resolution, float thickness)
    {
        Texture2D texture = CreateRuntimeTexture(resolution, "BrushRingTexture");
        Color[] pixels = new Color[resolution * resolution];
        float radius = (resolution - 1) * 0.5f;
        float antiAlias = 1.5f / Mathf.Max(radius, 1f);
        float innerRadius = Mathf.Clamp01(1f - thickness);

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = (x - radius) / radius;
                float dy = (y - radius) / radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float outerAlpha = 1f - Mathf.SmoothStep(1f - antiAlias, 1f + antiAlias, distance);
                float innerAlpha = Mathf.SmoothStep(innerRadius - antiAlias, innerRadius + antiAlias, distance);
                float alpha = Mathf.Clamp01(outerAlpha * innerAlpha);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D CreateRuntimeTexture(int resolution, string textureName)
    {
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(new Color[resolution * resolution]);
        return texture;
    }

    private void DestroyGeneratedRing()
    {
        DestroyRuntimeObject(generatedRingSprite);
        DestroyRuntimeObject(generatedRingTexture);
        generatedRingSprite = null;
        generatedRingTexture = null;
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
            return;
        }

        DestroyImmediate(target);
    }
}