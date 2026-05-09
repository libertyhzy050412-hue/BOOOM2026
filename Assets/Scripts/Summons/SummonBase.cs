using UnityEngine;

[DisallowMultipleComponent]
public abstract class SummonBase : MonoBehaviour
{
    [SerializeField] private Player owner;
    [SerializeField] private bool summonEnabled = true;
    [SerializeField] private bool useRigidbodyMovement = true;
    [SerializeField, Min(0f)] private float moveSpeed = 4f;

    private Rigidbody2D cachedRigidbody;
    private Vector2 pendingMoveDirection;

    public Player Owner => owner;
    public bool SummonEnabled => summonEnabled;
    public float MoveSpeed => moveSpeed;

    protected virtual void Reset()
    {
        owner = FindFirstObjectByType<Player>();
        cachedRigidbody = GetComponent<Rigidbody2D>();
    }

    protected virtual void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody2D>();
        ResolveOwner();
    }

    protected virtual void Update()
    {
        ResolveOwner();
        if (!summonEnabled)
        {
            pendingMoveDirection = Vector2.zero;
            return;
        }

        TickSummon(Time.deltaTime);
        Vector2 desiredDirection = Vector2.ClampMagnitude(EvaluateMoveDirection(Time.deltaTime), 1f);
        pendingMoveDirection = desiredDirection;

        if (cachedRigidbody != null && useRigidbodyMovement)
        {
            return;
        }

        transform.position += (Vector3)(desiredDirection * moveSpeed * Time.deltaTime);
    }

    protected virtual void FixedUpdate()
    {
        if (!summonEnabled || cachedRigidbody == null || !useRigidbodyMovement)
        {
            return;
        }

        cachedRigidbody.MovePosition(cachedRigidbody.position + pendingMoveDirection * moveSpeed * Time.fixedDeltaTime);
    }

    public void SetOwner(Player player)
    {
        owner = player;
        OnOwnerChanged();
    }

    public void SetSummonEnabled(bool enabled)
    {
        if (summonEnabled == enabled)
        {
            return;
        }

        summonEnabled = enabled;
        if (!summonEnabled)
        {
            pendingMoveDirection = Vector2.zero;
            OnSummonDisabled();
        }
    }

    protected abstract void TickSummon(float deltaTime);

    protected abstract Vector2 EvaluateMoveDirection(float deltaTime);

    protected virtual void OnOwnerChanged()
    {
    }

    protected virtual void OnSummonDisabled()
    {
    }

    protected void SetMoveSpeed(float value)
    {
        moveSpeed = Mathf.Max(0f, value);
    }

    protected Vector3 GetOwnerPositionOrSelf()
    {
        if (owner != null)
        {
            return owner.transform.position;
        }

        return transform.position;
    }

    protected bool IsEnemyAvailable(EnemyBase enemy)
    {
        return enemy != null && enemy.gameObject.activeInHierarchy && enemy.EnemyEnabled;
    }

    protected bool TryFindNearestEnemy(float maxDistanceFromOwner, float maxDistanceFromSelf, out EnemyBase nearestEnemy)
    {
        nearestEnemy = null;
        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0)
        {
            return false;
        }

        Vector2 selfPosition = transform.position;
        Vector2 ownerPosition = GetOwnerPositionOrSelf();
        float bestDistanceSqr = float.MaxValue;

        for (int index = 0; index < enemies.Length; index++)
        {
            EnemyBase candidate = enemies[index];
            if (!IsEnemyAvailable(candidate))
            {
                continue;
            }

            Vector2 candidatePosition = candidate.transform.position;
            if (maxDistanceFromOwner > 0f && (candidatePosition - ownerPosition).sqrMagnitude > maxDistanceFromOwner * maxDistanceFromOwner)
            {
                continue;
            }

            if (maxDistanceFromSelf > 0f && (candidatePosition - selfPosition).sqrMagnitude > maxDistanceFromSelf * maxDistanceFromSelf)
            {
                continue;
            }

            float distanceSqr = (candidatePosition - selfPosition).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            nearestEnemy = candidate;
        }

        return nearestEnemy != null;
    }

    private void ResolveOwner()
    {
        if (owner == null)
        {
            owner = FindFirstObjectByType<Player>();
        }
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
    }
}
