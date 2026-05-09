using UnityEngine;

[DisallowMultipleComponent]
public sealed class NormalEnemy : EnemyBase
{
    [SerializeField, Min(0f)] private float stopDistance = 0.1f;

    protected override Vector2 EvaluateMoveDirection(float deltaTime)
    {
        if (!TryGetTargetPosition(out Vector3 targetPosition))
        {
            return Vector2.zero;
        }

        Vector2 offset = targetPosition - transform.position;
        if (offset.sqrMagnitude <= stopDistance * stopDistance)
        {
            return Vector2.zero;
        }

        return offset.normalized;
    }

    private void OnValidate()
    {
        stopDistance = Mathf.Max(0f, stopDistance);
    }
}