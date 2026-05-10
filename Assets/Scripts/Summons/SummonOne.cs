using UnityEngine;

[DisallowMultipleComponent]
public sealed class SummonOne : SummonBase
{
    private enum CombatState
    {
        FollowOwner,
        ChaseTarget,
        PrepareCharge,
        Charge
    }

    [SerializeField, Min(0f)] private float followSpeed = 3.6f;
    [SerializeField, Min(0f)] private float chaseSpeed = 4.8f;
    [SerializeField, Min(0f)] private float followStopDistance = 1f;
    [SerializeField, Min(0f)] private float maxLockDistanceFromOwner = 12f;
    [SerializeField, Min(0f)] private float maxSearchDistanceFromSelf = 16f;
    [SerializeField, Min(0.01f)] private float targetRefreshInterval = 0.2f;
    [SerializeField, Min(0f)] private float prepareDistance = 1.15f;
    [SerializeField, Min(0.01f)] private float prepareDuration = 0.22f;
    [SerializeField, Min(0f)] private float chargeStartSpeed = 7.5f;
    [SerializeField, Min(0f)] private float chargeEndSpeed = 13f;
    [SerializeField, Min(0.01f)] private float chargeDuration = 0.35f;
    [SerializeField, Min(0f)] private float lockedTargetContactDistance = 0.28f;
    [SerializeField, Min(0f)] private float chargeDamage = 10f;

    private EnemyBase lockedTarget;
    private CombatState combatState;
    private Vector2 currentMoveDirection;
    private Vector2 lastChargeDirection = Vector2.right;
    private float stateTimer;
    private float targetRefreshTimer;

    protected override void TickSummon(float deltaTime)
    {
        targetRefreshTimer += deltaTime;

        switch (combatState)
        {
            case CombatState.PrepareCharge:
                UpdatePrepareCharge(deltaTime);
                return;
            case CombatState.Charge:
                UpdateCharge(deltaTime);
                return;
        }

        MaintainOrAcquireTarget();

        if (lockedTarget == null)
        {
            UpdateFollowOwner();
            return;
        }

        UpdateChaseTarget();
    }

    protected override Vector2 EvaluateMoveDirection(float deltaTime)
    {
        return currentMoveDirection;
    }

    protected override void OnSummonDisabled()
    {
        currentMoveDirection = Vector2.zero;
        combatState = CombatState.FollowOwner;
        lockedTarget = null;
        stateTimer = 0f;
    }

    private void UpdateFollowOwner()
    {
        SetMoveSpeed(followSpeed);
        Vector2 offset = GetOwnerPositionOrSelf() - transform.position;
        if (offset.sqrMagnitude <= followStopDistance * followStopDistance)
        {
            currentMoveDirection = Vector2.zero;
            combatState = CombatState.FollowOwner;
            return;
        }

        currentMoveDirection = offset.normalized;
        combatState = CombatState.FollowOwner;
    }

    private void UpdateChaseTarget()
    {
        if (!IsTargetValid(ignoreOwnerDistanceLimit: false))
        {
            lockedTarget = null;
            UpdateFollowOwner();
            return;
        }

        Vector2 offset = lockedTarget.transform.position - transform.position;
        if (offset.sqrMagnitude <= prepareDistance * prepareDistance)
        {
            EnterPrepareCharge();
            return;
        }

        SetMoveSpeed(chaseSpeed);
        currentMoveDirection = offset.normalized;
        combatState = CombatState.ChaseTarget;
    }

    private void UpdatePrepareCharge(float deltaTime)
    {
        if (!IsTargetValid(ignoreOwnerDistanceLimit: true))
        {
            ClearLockedTarget();
            UpdateFollowOwner();
            return;
        }

        stateTimer += deltaTime;
        SetMoveSpeed(0f);
        currentMoveDirection = Vector2.zero;

        if (stateTimer >= prepareDuration)
        {
            EnterCharge();
        }
    }

    private void UpdateCharge(float deltaTime)
    {
        stateTimer += deltaTime;
        if (lockedTarget != null)
        {
            Vector2 targetOffset = lockedTarget.transform.position - transform.position;
            if (targetOffset.sqrMagnitude > 0.0001f)
            {
                lastChargeDirection = targetOffset.normalized;
            }

            if (targetOffset.sqrMagnitude <= lockedTargetContactDistance * lockedTargetContactDistance)
            {
                HandleLockedTargetContact();
                return;
            }
        }

        float chargeProgress = Mathf.Clamp01(stateTimer / chargeDuration);
        SetMoveSpeed(Mathf.Lerp(chargeStartSpeed, chargeEndSpeed, chargeProgress));
        currentMoveDirection = lastChargeDirection;

        if (stateTimer >= chargeDuration)
        {
            FinishCharge();
        }
    }

    private void MaintainOrAcquireTarget()
    {
        if (IsTargetValid(ignoreOwnerDistanceLimit: false))
        {
            return;
        }

        lockedTarget = null;
        if (targetRefreshTimer < targetRefreshInterval)
        {
            return;
        }

        targetRefreshTimer = 0f;
        TryFindNearestEnemy(maxLockDistanceFromOwner, maxSearchDistanceFromSelf, out lockedTarget);
    }

    private bool IsTargetValid(bool ignoreOwnerDistanceLimit)
    {
        if (!IsEnemyAvailable(lockedTarget))
        {
            return false;
        }

        if (ignoreOwnerDistanceLimit || maxLockDistanceFromOwner <= 0f)
        {
            return true;
        }

        Vector2 ownerPosition = GetOwnerPositionOrSelf();
        Vector2 targetPosition = lockedTarget.transform.position;
        return (targetPosition - ownerPosition).sqrMagnitude <= maxLockDistanceFromOwner * maxLockDistanceFromOwner;
    }

    private void EnterPrepareCharge()
    {
        combatState = CombatState.PrepareCharge;
        stateTimer = 0f;
        SetMoveSpeed(0f);
        currentMoveDirection = Vector2.zero;
    }

    private void EnterCharge()
    {
        combatState = CombatState.Charge;
        stateTimer = 0f;
        if (lockedTarget != null)
        {
            Vector2 targetOffset = lockedTarget.transform.position - transform.position;
            if (targetOffset.sqrMagnitude > 0.0001f)
            {
                lastChargeDirection = targetOffset.normalized;
            }
        }

        SetMoveSpeed(chargeStartSpeed);
        currentMoveDirection = lastChargeDirection;
    }

    private void FinishCharge()
    {
        ClearLockedTarget();
        targetRefreshTimer = targetRefreshInterval;
        UpdateFollowOwner();
    }

    private void HandleLockedTargetContact()
    {
        if (lockedTarget != null)
        {
            lockedTarget.ApplyDamage(chargeDamage, DamageTeam.Player, this);
        }

        FinishCharge();
    }

    private void ClearLockedTarget()
    {
        lockedTarget = null;
        combatState = CombatState.FollowOwner;
        stateTimer = 0f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleTargetContact(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleTargetContact(collision.collider);
    }

    private void TryHandleTargetContact(Component other)
    {
        if (combatState != CombatState.Charge || lockedTarget == null || other == null)
        {
            return;
        }

        EnemyBase hitEnemy = other.GetComponentInParent<EnemyBase>();
        if (hitEnemy != lockedTarget)
        {
            return;
        }

        HandleLockedTargetContact();
    }

    private void OnValidate()
    {
        followSpeed = Mathf.Max(0f, followSpeed);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        followStopDistance = Mathf.Max(0f, followStopDistance);
        maxLockDistanceFromOwner = Mathf.Max(0f, maxLockDistanceFromOwner);
        maxSearchDistanceFromSelf = Mathf.Max(0f, maxSearchDistanceFromSelf);
        targetRefreshInterval = Mathf.Max(0.01f, targetRefreshInterval);
        prepareDistance = Mathf.Max(0f, prepareDistance);
        prepareDuration = Mathf.Max(0.01f, prepareDuration);
        chargeStartSpeed = Mathf.Max(0f, chargeStartSpeed);
        chargeEndSpeed = Mathf.Max(chargeStartSpeed, chargeEndSpeed);
        chargeDuration = Mathf.Max(0.01f, chargeDuration);
        lockedTargetContactDistance = Mathf.Max(0f, lockedTargetContactDistance);
        chargeDamage = Mathf.Max(0f, chargeDamage);
    }
}