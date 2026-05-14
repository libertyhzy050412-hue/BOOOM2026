using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BowArrowProjectile : MonoBehaviour
{
    [SerializeField] private Rigidbody2D cachedRigidbody;
    [SerializeField] private Collider2D cachedCollider;
    [SerializeField] private SpriteRenderer[] projectileRenderers;
    [SerializeField] private bool destroyOnBlockingHit = true;
    [Header("Reveal Trail")]
    [SerializeField] private bool revealTrailWhileFlying = true;
    [SerializeField, Min(0.02f)] private float trailRevealRadius = 0.24f;
    [SerializeField, Range(0.05f, 0.95f)] private float trailRevealHardness = 0.52f;
    [SerializeField, Range(0.1f, 0.9f)] private float trailRevealSpacingRatio = 0.3f;
    [SerializeField] private bool rotateAlongVelocity = true;
    [SerializeField] private bool forceVisibleSorting = true;
    [SerializeField] private bool forceNoMaskInteraction = true;
    [SerializeField] private bool matchFirstRendererSortingLayer = true;
    [SerializeField] private string overrideSortingLayerName;
    [SerializeField] private int projectileSortingOrder = 4;
    [SerializeField] private float visualAngleOffset;

    private Player owner;
    private WorldRevealMaskController revealMaskController;
    private Transform mapRoot;
    private string mapRootName = "MapRoot";
    private string revealMaskName = "WorldRevealMask";
    private float maskPixelsPerUnit = 16f;
    private int maxMaskTextureSize = 2048;
    private float damageAmount;
    private float remainingLifetime;
    private float maxTravelDistance = float.PositiveInfinity;
    private float travelledDistance;
    private float revealRadius;
    private float revealHardness = 0.6f;
    private float moveSpeed;
    private Vector2 moveDirection = Vector2.right;
    private bool launched;
    private bool impactResolved;
    private Action<float> travelDistanceCallback;
    private ContactFilter2D sweepFilter;
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[8];
    private int cachedSortingLayerId;
    private bool hasCachedSortingLayer;

    private void Reset()
    {
        cachedRigidbody = GetComponent<Rigidbody2D>();
        cachedCollider = GetComponent<Collider2D>();
        ResolveProjectileRenderers();
    }

    private void Awake()
    {
        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponent<Rigidbody2D>();
        }

        if (cachedCollider == null)
        {
            cachedCollider = GetComponent<Collider2D>();
        }

        ResolveProjectileRenderers();
        CacheSortingLayer();
        ApplyVisualRendererSettings();

        sweepFilter = new ContactFilter2D();
        sweepFilter.useLayerMask = false;
        sweepFilter.useDepth = false;
        sweepFilter.useNormalAngle = false;
        sweepFilter.useTriggers = true;
    }

    private void Update()
    {
        if (!launched || impactResolved)
        {
            return;
        }

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            FinishImpact();
            return;
        }

        if (cachedRigidbody == null)
        {
            AdvanceWithoutRigidbody(Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (!launched || impactResolved || cachedRigidbody == null)
        {
            return;
        }

        AdvanceWithRigidbody(Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ResolveImpact(other, other != null ? other.ClosestPoint(transform.position) : (Vector2)transform.position);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Collider2D other = collision.collider;
        Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (other != null ? other.ClosestPoint(transform.position) : (Vector2)transform.position);
        ResolveImpact(other, impactPoint);
    }

    public void Launch(
        Player projectileOwner,
        Vector2 direction,
        float speed,
        float damage,
        float lifetime,
        float impactRevealRadius,
        float impactRevealHardness,
        Transform preferredMapRoot,
        string preferredMapRootName,
        string preferredRevealMaskName,
        float preferredMaskPixelsPerUnit,
        int preferredMaxMaskTextureSize,
        float maximumTravelDistance,
        Action<float> onTravelDistance)
    {
        owner = projectileOwner;
        moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        moveSpeed = Mathf.Max(0f, speed);
        damageAmount = Mathf.Max(0f, damage);
        remainingLifetime = Mathf.Max(0.01f, lifetime);
        maxTravelDistance = maximumTravelDistance > 0f && !float.IsInfinity(maximumTravelDistance)
            ? maximumTravelDistance
            : float.PositiveInfinity;
        travelledDistance = 0f;
        travelDistanceCallback = onTravelDistance;
        revealRadius = Mathf.Max(0.05f, impactRevealRadius);
        revealHardness = Mathf.Clamp(impactRevealHardness, 0.05f, 0.95f);
        mapRoot = preferredMapRoot;
        mapRootName = string.IsNullOrWhiteSpace(preferredMapRootName) ? "MapRoot" : preferredMapRootName;
        revealMaskName = string.IsNullOrWhiteSpace(preferredRevealMaskName) ? "WorldRevealMask" : preferredRevealMaskName;
        maskPixelsPerUnit = Mathf.Clamp(preferredMaskPixelsPerUnit, 1f, 64f);
        maxMaskTextureSize = Mathf.Clamp(preferredMaxMaskTextureSize, 256, 4096);
        launched = true;
        impactResolved = false;

        ApplyVisualRendererSettings();

        if (cachedRigidbody != null)
        {
            cachedRigidbody.bodyType = RigidbodyType2D.Kinematic;
            cachedRigidbody.useFullKinematicContacts = true;
            cachedRigidbody.linearVelocity = Vector2.zero;
            cachedRigidbody.angularVelocity = 0f;
            cachedRigidbody.position = transform.position;
        }

        UpdateVisualRotation(moveDirection);
    }

    private void ResolveImpact(Component other, Vector2 impactPoint)
    {
        if (impactResolved || !launched || other == null)
        {
            return;
        }

        if (BelongsToOwner(other))
        {
            return;
        }

        if (TryResolveDamageable(other, out IDamageable damageable, out Component damageableComponent) &&
            damageableComponent != null &&
            damageable.Team == DamageTeam.Enemy)
        {
            if (damageAmount > 0f)
            {
                damageable.ApplyDamage(damageAmount, DamageTeam.Player, this);
            }

            AudioManager.PlayBowImpact();
            RevealImpactArea(impactPoint);
            FinishImpact();
            return;
        }

        if (other is Collider2D otherCollider && otherCollider.isTrigger)
        {
            return;
        }

        if (destroyOnBlockingHit)
        {
            AudioManager.PlayBowImpact();
            FinishImpact();
        }
    }

    private bool TryResolveSweepImpact(Vector2 startPosition, Vector2 endPosition)
    {
        if (impactResolved)
        {
            return true;
        }

        Vector2 sweepOffset = endPosition - startPosition;
        float sweepDistance = sweepOffset.magnitude;
        if (sweepDistance <= 0.0001f)
        {
            return false;
        }

        Vector2 sweepDirection = sweepOffset / sweepDistance;
        int hitCount = 0;

        if (cachedCollider != null)
        {
            hitCount = cachedCollider.Cast(sweepDirection, sweepFilter, sweepHits, sweepDistance);
        }
        else
        {
            RaycastHit2D hit = Physics2D.Raycast(startPosition, sweepDirection, sweepDistance);
            if (hit.collider != null)
            {
                sweepHits[0] = hit;
                hitCount = 1;
            }
        }

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit2D hit = sweepHits[index];
            if (hit.collider == null)
            {
                continue;
            }

            if (hit.collider == cachedCollider || BelongsToOwner(hit.collider))
            {
                continue;
            }

            Vector2 impactPoint = hit.point;
            if (impactPoint == Vector2.zero)
            {
                impactPoint = hit.centroid;
            }

            RevealTrailSegment(startPosition, impactPoint);
            RegisterTravelDistance(Vector2.Distance(startPosition, impactPoint));
            ResolveImpact(hit.collider, impactPoint);
            return impactResolved;
        }

        return false;
    }

    private void ResolveProjectileRenderers()
    {
        if (projectileRenderers == null || projectileRenderers.Length == 0)
        {
            projectileRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    private void CacheSortingLayer()
    {
        if (!matchFirstRendererSortingLayer)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(overrideSortingLayerName))
        {
            cachedSortingLayerId = SortingLayer.NameToID(overrideSortingLayerName);
            hasCachedSortingLayer = cachedSortingLayerId != 0 || overrideSortingLayerName == "Default";
            return;
        }

        ResolveProjectileRenderers();
        for (int index = 0; index < projectileRenderers.Length; index++)
        {
            SpriteRenderer spriteRenderer = projectileRenderers[index];
            if (spriteRenderer == null)
            {
                continue;
            }

            cachedSortingLayerId = spriteRenderer.sortingLayerID;
            hasCachedSortingLayer = true;
            return;
        }
    }

    private void ApplyVisualRendererSettings()
    {
        ResolveProjectileRenderers();
        for (int index = 0; index < projectileRenderers.Length; index++)
        {
            SpriteRenderer spriteRenderer = projectileRenderers[index];
            if (spriteRenderer == null)
            {
                continue;
            }

            if (hasCachedSortingLayer)
            {
                spriteRenderer.sortingLayerID = cachedSortingLayerId;
            }

            if (forceVisibleSorting)
            {
                spriteRenderer.sortingOrder = projectileSortingOrder;
            }

            if (forceNoMaskInteraction)
            {
                spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
            }
        }
    }

    private void RevealImpactArea(Vector2 impactPoint)
    {
        if (!EnsureRevealMaskController())
        {
            return;
        }

        revealMaskController.RevealStamp(impactPoint, revealRadius, revealHardness);
        revealMaskController.Flush();
    }

    private void RevealTrailSegment(Vector2 from, Vector2 to)
    {
        if (!revealTrailWhileFlying || Vector2.Distance(from, to) <= 0.0001f)
        {
            return;
        }

        if (!EnsureRevealMaskController())
        {
            return;
        }

        if (revealMaskController.RevealStroke(from, to, trailRevealRadius, trailRevealRadius, trailRevealHardness, trailRevealSpacingRatio))
        {
            revealMaskController.Flush();
        }
    }

    private bool EnsureRevealMaskController()
    {
        revealMaskController = WorldRevealMaskController.GetOrCreate(
            mapRoot,
            mapRootName,
            revealMaskName,
            maskPixelsPerUnit,
            maxMaskTextureSize);

        return revealMaskController != null;
    }

    private void FinishImpact()
    {
        impactResolved = true;
        launched = false;
        if (cachedRigidbody != null)
        {
            cachedRigidbody.linearVelocity = Vector2.zero;
            cachedRigidbody.angularVelocity = 0f;
        }

        Destroy(gameObject);
    }

    private void AdvanceWithoutRigidbody(float deltaTime)
    {
        Vector2 startPosition = transform.position;
        float stepDistance = GetStepDistance(deltaTime);
        if (stepDistance <= 0.0001f)
        {
            FinishImpact();
            return;
        }

        Vector2 endPosition = startPosition + moveDirection * stepDistance;
        if (TryResolveSweepImpact(startPosition, endPosition))
        {
            return;
        }

        RevealTrailSegment(startPosition, endPosition);
        RegisterTravelDistance(stepDistance);
        transform.position = endPosition;
        UpdateVisualRotation(moveDirection);

        if (HasReachedTravelLimit())
        {
            FinishImpact();
        }
    }

    private void AdvanceWithRigidbody(float deltaTime)
    {
        Vector2 startPosition = cachedRigidbody.position;
        float stepDistance = GetStepDistance(deltaTime);
        if (stepDistance <= 0.0001f)
        {
            FinishImpact();
            return;
        }

        Vector2 nextPosition = startPosition + moveDirection * stepDistance;
        if (TryResolveSweepImpact(startPosition, nextPosition))
        {
            return;
        }

        RevealTrailSegment(startPosition, nextPosition);
        RegisterTravelDistance(stepDistance);
        cachedRigidbody.MovePosition(nextPosition);
        UpdateVisualRotation(moveDirection);

        if (HasReachedTravelLimit())
        {
            FinishImpact();
        }
    }

    private float GetStepDistance(float deltaTime)
    {
        float requestedDistance = Mathf.Max(0f, moveSpeed * Mathf.Max(deltaTime, 0f));
        if (float.IsInfinity(maxTravelDistance))
        {
            return requestedDistance;
        }

        return Mathf.Min(requestedDistance, Mathf.Max(0f, maxTravelDistance - travelledDistance));
    }

    private void RegisterTravelDistance(float segmentDistance)
    {
        if (segmentDistance <= 0.0001f)
        {
            return;
        }

        travelledDistance += segmentDistance;
        travelDistanceCallback?.Invoke(segmentDistance);
    }

    private bool HasReachedTravelLimit()
    {
        return !float.IsInfinity(maxTravelDistance) && travelledDistance >= maxTravelDistance - 0.0001f;
    }

    private bool BelongsToOwner(Component other)
    {
        if (owner == null)
        {
            return false;
        }

        return other.GetComponentInParent<Player>() == owner;
    }

    private void UpdateVisualRotation(Vector2 direction)
    {
        if (!rotateAlongVelocity || direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + visualAngleOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private static bool TryResolveDamageable(Component source, out IDamageable damageable, out Component damageableComponent)
    {
        MonoBehaviour[] behaviours = source.GetComponentsInParent<MonoBehaviour>(true);
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];
            if (behaviour is IDamageable resolved)
            {
                damageable = resolved;
                damageableComponent = behaviour;
                return true;
            }
        }

        damageable = null;
        damageableComponent = null;
        return false;
    }

    private void OnValidate()
    {
        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponent<Rigidbody2D>();
        }

        if (cachedCollider == null)
        {
            cachedCollider = GetComponent<Collider2D>();
        }

        trailRevealRadius = Mathf.Max(0.02f, trailRevealRadius);
        trailRevealHardness = Mathf.Clamp(trailRevealHardness, 0.05f, 0.95f);
        trailRevealSpacingRatio = Mathf.Clamp(trailRevealSpacingRatio, 0.1f, 0.9f);
        ResolveProjectileRenderers();
    }
}