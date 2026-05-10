#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Player))]
public sealed class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Rigidbody2D controlledBody;
    [SerializeField, Min(0.001f)] private float collisionSkin = 0.02f;

    private Player player;
    private Vector2 moveInput;
    private readonly RaycastHit2D[] castResults = new RaycastHit2D[8];
    private ContactFilter2D movementFilter;
    
    
    private void Reset()
    {
        controlledBody = GetComponent<Rigidbody2D>();
    }

    private void Awake()
    {
        player = GetComponent<Player>();
        if (controlledBody == null)
        {
            controlledBody = GetComponent<Rigidbody2D>();
        }

        if (controlledBody != null)
        {
            controlledBody.bodyType = RigidbodyType2D.Kinematic;
            controlledBody.useFullKinematicContacts = true;
            controlledBody.linearVelocity = Vector2.zero;
            controlledBody.angularVelocity = 0f;
        }

        movementFilter = new ContactFilter2D();
        movementFilter.useLayerMask = false;
        movementFilter.useTriggers = false;
        
    }

    private void Update()
    {
        moveInput = ReadMoveInput();
        MoveTransform(Time.deltaTime);
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                input.y += 1f;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (input.sqrMagnitude <= 0f)
        {
            input.x = Input.GetAxisRaw("Horizontal");
            input.y = Input.GetAxisRaw("Vertical");
        }
#endif

        return Vector2.ClampMagnitude(input, 1f);
    }

    private void MoveTransform(float deltaTime)
    {
        if (player == null)
        {
            return;
        }

        if (controlledBody != null)
        {
            controlledBody.position = transform.position;
        }

        Vector2 movement = moveInput * player.MoveSpeed * deltaTime;
        MoveAlongAxis(new Vector2(movement.x, 0f));
        MoveAlongAxis(new Vector2(0f, movement.y));
    }

    private void MoveAlongAxis(Vector2 axisMovement)
    {
        float requestedDistance = axisMovement.magnitude;
        if (requestedDistance <= 0f)
        {
            return;
        }

        Vector2 direction = axisMovement / requestedDistance;
        float allowedDistance = requestedDistance;

        if (controlledBody != null)
        {
            int hitCount = controlledBody.Cast(direction, movementFilter, castResults, requestedDistance + collisionSkin);
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit2D hit = castResults[index];
                if (hit.collider == null || hit.collider.isTrigger)
                {
                    continue;
                }

                float distanceToHit = Mathf.Max(0f, hit.distance - collisionSkin);
                if (distanceToHit < allowedDistance)
                {
                    allowedDistance = distanceToHit;
                }
            }
        }

        if (allowedDistance <= 0f)
        {
            return;
        }

        Vector3 nextPosition = transform.position + (Vector3)(direction * allowedDistance);
        transform.position = nextPosition;

        if (controlledBody != null)
        {
            controlledBody.position = nextPosition;
        }
    }
}