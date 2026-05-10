using UnityEngine;

[DisallowMultipleComponent]
public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [SerializeField] private Player targetPlayer;
    [SerializeField] private bool enemyEnabled = true;
    [SerializeField] private bool useRigidbodyMovement = true;
    [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
    [SerializeField, Min(0f)] private float maxHealth = 10f;
    [SerializeField, Min(0f)] private float currentHealth = 10f;
    [SerializeField] private bool restoreOuterWorldAlongPath = true;
    [SerializeField, Min(0f)] private float restoreRadius = 0.72f;
    [SerializeField, Range(0.05f, 0.95f)] private float restoreHardness = 0.72f;
    [SerializeField, Range(0.1f, 0.9f)] private float restoreSpacingRatio = 0.35f;
    [SerializeField, Range(0f, 1f)] private float restoreStrength = 1f;
    [SerializeField, Min(0.01f)] private float stationaryRestoreInterval = 0.12f;

    private Rigidbody2D cachedRigidbody;
    private UnitDamageFlash damageFlash;
    private WorldRevealMaskController revealMaskController;
    private Vector2 pendingMoveDirection;
    private Vector2 lastRestorePosition;
    private bool hasRestorePosition;
    private float stationaryRestoreTimer;

    public Player TargetPlayer => targetPlayer;
    public bool EnemyEnabled => enemyEnabled;
    public float MoveSpeed => moveSpeed;
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public DamageTeam Team => DamageTeam.Enemy;
    public bool IsAlive => currentHealth > 0f;

    protected virtual void Reset()
    {
        targetPlayer = FindFirstObjectByType<Player>();
        cachedRigidbody = GetComponent<Rigidbody2D>();
    }

    protected virtual void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody2D>();
        damageFlash = GetComponent<UnitDamageFlash>();
        if (damageFlash == null)
        {
            damageFlash = gameObject.AddComponent<UnitDamageFlash>();
        }
        ResolveTargetPlayer();
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    protected virtual void Update()
    {
        ResolveTargetPlayer();
        if (!enemyEnabled)
        {
            pendingMoveDirection = Vector2.zero;
            return;
        }

        Vector2 desiredDirection = Vector2.ClampMagnitude(EvaluateMoveDirection(Time.deltaTime), 1f);
        pendingMoveDirection = desiredDirection;

        if (cachedRigidbody != null && useRigidbodyMovement)
        {
            return;
        }

        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + (Vector3)(desiredDirection * moveSpeed * Time.deltaTime);
        transform.position = endPosition;
        HandleWorldRestore(startPosition, endPosition, Time.deltaTime);
    }

    protected virtual void FixedUpdate()
    {
        if (!enemyEnabled || cachedRigidbody == null || !useRigidbodyMovement)
        {
            return;
        }

        Vector2 startPosition = cachedRigidbody.position;
        Vector2 endPosition = startPosition + pendingMoveDirection * moveSpeed * Time.fixedDeltaTime;
        cachedRigidbody.MovePosition(endPosition);
        HandleWorldRestore(startPosition, endPosition, Time.fixedDeltaTime);
    }

    public void SetTargetPlayer(Player player)
    {
        targetPlayer = player;
        OnTargetPlayerChanged();
    }

    public void SetEnemyEnabled(bool enabled)
    {
        if (enemyEnabled == enabled)
        {
            return;
        }

        enemyEnabled = enabled;
        if (!enemyEnabled)
        {
            pendingMoveDirection = Vector2.zero;
            ResetRestoreState();
            OnEnemyDisabled();
        }
    }

    public void SetMoveSpeed(float value)
    {
        moveSpeed = Mathf.Max(0f, value);
    }

    public void SetCurrentHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
    }

    public void SetMaxHealth(float value)
    {
        maxHealth = Mathf.Max(0f, value);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        ApplyDamage(damage, DamageTeam.Neutral, this);
    }

    public bool ApplyDamage(float amount, DamageTeam sourceTeam, Object source = null)
    {
        if (amount <= 0f || !IsAlive)
        {
            return false;
        }

        if (sourceTeam == Team)
        {
            return false;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        damageFlash?.PlayFlash();
        if (currentHealth <= 0f)
        {
            OnKilled();
            Destroy(gameObject);
        }

        return true;
    }

    protected bool TryGetTargetPosition(out Vector3 position)
    {
        ResolveTargetPlayer();
        if (targetPlayer != null)
        {
            position = targetPlayer.transform.position;
            return true;
        }

        position = transform.position;
        return false;
    }

    protected abstract Vector2 EvaluateMoveDirection(float deltaTime);

    protected virtual void OnTargetPlayerChanged()
    {
    }

    protected virtual void OnEnemyDisabled()
    {
    }

    protected virtual void OnKilled()
    {
    }

    private void HandleWorldRestore(Vector2 startPosition, Vector2 endPosition, float deltaTime)
    {
        if (!restoreOuterWorldAlongPath || restoreRadius <= 0f)
        {
            return;
        }

        WorldRevealMaskController controller = EnsureRevealMaskController();
        if (controller == null)
        {
            return;
        }

        float movedDistance = Vector2.Distance(startPosition, endPosition);
        float stationaryThreshold = Mathf.Max(restoreRadius * restoreSpacingRatio, 0.02f);

        if (!hasRestorePosition)
        {
            controller.ConcealStamp(endPosition, restoreRadius, restoreHardness, restoreStrength);
            controller.Flush();
            lastRestorePosition = endPosition;
            hasRestorePosition = true;
            stationaryRestoreTimer = 0f;
            return;
        }

        if (movedDistance <= stationaryThreshold)
        {
            stationaryRestoreTimer += Mathf.Max(deltaTime, 0.0001f);
            if (stationaryRestoreTimer >= stationaryRestoreInterval)
            {
                controller.ConcealStamp(endPosition, restoreRadius, restoreHardness, restoreStrength);
                controller.Flush();
                stationaryRestoreTimer = 0f;
            }
        }
        else
        {
            stationaryRestoreTimer = 0f;
            controller.ConcealStroke(lastRestorePosition, endPosition, restoreRadius, restoreRadius, restoreHardness, restoreSpacingRatio, restoreStrength);
            controller.Flush();
        }

        lastRestorePosition = endPosition;
    }

    private WorldRevealMaskController EnsureRevealMaskController()
    {
        if (revealMaskController == null)
        {
            revealMaskController = WorldRevealMaskController.GetOrCreate();
        }

        return revealMaskController;
    }

    private void ResolveTargetPlayer()
    {
        if (targetPlayer == null)
        {
            targetPlayer = FindFirstObjectByType<Player>();
        }
    }

    private void ResetRestoreState()
    {
        hasRestorePosition = false;
        stationaryRestoreTimer = 0f;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        maxHealth = Mathf.Max(0f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        restoreRadius = Mathf.Max(0f, restoreRadius);
        restoreHardness = Mathf.Clamp(restoreHardness, 0.05f, 0.95f);
        restoreSpacingRatio = Mathf.Clamp(restoreSpacingRatio, 0.1f, 0.9f);
        restoreStrength = Mathf.Clamp01(restoreStrength);
        stationaryRestoreInterval = Mathf.Max(0.01f, stationaryRestoreInterval);
    }
}