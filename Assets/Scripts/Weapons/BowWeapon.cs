using UnityEngine.Serialization;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BowWeapon : WeaponBase
{
    [SerializeField] private BowArrowProjectile arrowPrefab;
    [SerializeField] private Transform arrowSpawnPoint;
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private string projectileRootName = "ProjectileRoot";
    [SerializeField] private Transform mapRoot;
    [SerializeField] private string mapRootName = "MapRoot";
    [SerializeField] private string revealMaskName = "WorldRevealMask";
    [SerializeField, Range(1f, 64f)] private float maskPixelsPerUnit = 16f;
    [SerializeField, Min(256)] private int maxMaskTextureSize = 2048;
    [SerializeField, Min(0.05f)] private float maxChargeDuration = 1.1f;
    [SerializeField, Range(0f, 1f)] private float minimumChargeNormalizedToFire = 0.08f;
    [SerializeField, Min(0f)] private float minArrowDamage = 10f;
    [SerializeField, Min(0f)] private float maxArrowDamage = 24f;
    [SerializeField, Min(0.1f)] private float minArrowSpeed = 12f;
    [SerializeField, Min(0.1f)] private float maxArrowSpeed = 28f;
    [SerializeField, Min(0.1f)] private float arrowLifetime = 4f;
    [SerializeField, Min(0.05f)] private float minRevealRadius = 1.1f;
    [SerializeField, Min(0.05f)] private float maxRevealRadius = 2.4f;
    [SerializeField, Range(0.05f, 0.95f)] private float revealHardness = 0.62f;
    [FormerlySerializedAs("postFireCooldown")]
    [SerializeField, Min(0f)] private float attackCooldownSeconds = 0.35f;
    [SerializeField] private bool rotateTowardPointer = true;
    [SerializeField] private float visualAngleOffset;

    private float currentChargeTime;
    private float fireCooldownTimer;
    private bool wasPrimaryUseHeld;
    private bool hasLoggedMissingCamera;
    private Vector2 lastAimDirection = Vector2.right;

    public float ChargeNormalized => Mathf.Clamp01(currentChargeTime / Mathf.Max(maxChargeDuration, 0.0001f));
    public float MinimumChargeNormalizedToFire => minimumChargeNormalizedToFire;
    public float AttackCooldownRemaining => fireCooldownTimer;
    public bool IsInAttackCooldown => fireCooldownTimer > 0.0001f;
    public bool IsCharging => !IsInAttackCooldown && wasPrimaryUseHeld && currentChargeTime > 0f;

    protected override void Awake()
    {
        base.Awake();
        ResolveMapRoot();
    }

    protected override void Tick(float deltaTime)
    {
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
        if (IsInAttackCooldown)
        {
            currentChargeTime = 0f;
            wasPrimaryUseHeld = false;
            return;
        }

        if (primaryUseHeld)
        {
            currentChargeTime = Mathf.Min(maxChargeDuration, currentChargeTime + Mathf.Max(deltaTime, 0f));
        }
        else if (wasPrimaryUseHeld)
        {
            TryFireArrow();
            currentChargeTime = 0f;
        }
        else
        {
            currentChargeTime = 0f;
        }

        wasPrimaryUseHeld = primaryUseHeld;
    }

    protected override void OnWeaponDisabled()
    {
        currentChargeTime = 0f;
        fireCooldownTimer = 0f;
        wasPrimaryUseHeld = false;
    }

    private void Reset()
    {
        arrowSpawnPoint = transform;
        projectileRoot = null;
    }

    private void OnValidate()
    {
        maskPixelsPerUnit = Mathf.Clamp(maskPixelsPerUnit, 1f, 64f);
        maxMaskTextureSize = Mathf.Clamp(maxMaskTextureSize, 256, 4096);
        maxChargeDuration = Mathf.Max(0.05f, maxChargeDuration);
        minimumChargeNormalizedToFire = Mathf.Clamp01(minimumChargeNormalizedToFire);
        minArrowDamage = Mathf.Max(0f, minArrowDamage);
        maxArrowDamage = Mathf.Max(minArrowDamage, maxArrowDamage);
        minArrowSpeed = Mathf.Max(0.1f, minArrowSpeed);
        maxArrowSpeed = Mathf.Max(minArrowSpeed, maxArrowSpeed);
        arrowLifetime = Mathf.Max(0.1f, arrowLifetime);
        minRevealRadius = Mathf.Max(0.05f, minRevealRadius);
        maxRevealRadius = Mathf.Max(minRevealRadius, maxRevealRadius);
        revealHardness = Mathf.Clamp(revealHardness, 0.05f, 0.95f);
        attackCooldownSeconds = Mathf.Max(0f, attackCooldownSeconds);
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

        float chargeNormalized = ChargeNormalized;
        if (chargeNormalized < minimumChargeNormalizedToFire)
        {
            return;
        }

        Vector2 fireDirection = lastAimDirection.sqrMagnitude > 0.0001f ? lastAimDirection.normalized : Vector2.right;
        Transform spawnReference = arrowSpawnPoint != null ? arrowSpawnPoint : transform;
        Vector3 spawnPosition = spawnReference.position;
        BowArrowProjectile arrowInstance = Instantiate(arrowPrefab, spawnPosition, Quaternion.identity, EnsureProjectileRoot());
        arrowInstance.Launch(
            Owner,
            fireDirection,
            Mathf.Lerp(minArrowSpeed, maxArrowSpeed, chargeNormalized),
            Mathf.Lerp(minArrowDamage, maxArrowDamage, chargeNormalized),
            arrowLifetime,
            Mathf.Lerp(minRevealRadius, maxRevealRadius, chargeNormalized),
            revealHardness,
            mapRoot,
            mapRootName,
            revealMaskName,
            maskPixelsPerUnit,
            maxMaskTextureSize);
        fireCooldownTimer = attackCooldownSeconds;
        currentChargeTime = 0f;
        wasPrimaryUseHeld = false;
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