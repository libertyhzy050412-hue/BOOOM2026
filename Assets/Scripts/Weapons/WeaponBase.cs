#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;

[DisallowMultipleComponent]
public abstract class WeaponBase : MonoBehaviour
{
    [SerializeField] private Player owner;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private bool weaponEnabled = true;

    public Player Owner => owner;
    public bool WeaponEnabled => weaponEnabled;

    protected virtual void Reset()
    {
        owner = GetComponentInParent<Player>();
        inputCamera = Camera.main;
    }

    protected virtual void Awake()
    {
        ResolveOwner();
        ResolveInputCamera();
    }

    protected virtual void Update()
    {
        ResolveInputCamera();
        if (!weaponEnabled)
        {
            return;
        }

        Tick(Time.deltaTime);
    }

    public void SetOwner(Player player)
    {
        owner = player;
        OnOwnerChanged();
    }

    public void SetWeaponEnabled(bool enabled)
    {
        if (weaponEnabled == enabled)
        {
            return;
        }

        weaponEnabled = enabled;
        if (!weaponEnabled)
        {
            OnWeaponDisabled();
        }
    }

    protected abstract void Tick(float deltaTime);

    protected virtual void OnOwnerChanged()
    {
    }

    protected virtual void OnWeaponDisabled()
    {
    }

    protected bool TryGetPointerWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = transform.position;
        if (inputCamera == null)
        {
            return false;
        }

        Vector3 screenPosition = default;
        bool hasPointer = false;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            hasPointer = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!hasPointer)
        {
            screenPosition = Input.mousePosition;
            hasPointer = true;
        }
#endif

        if (!hasPointer)
        {
            return false;
        }

        worldPosition = inputCamera.ScreenToWorldPoint(screenPosition);
        worldPosition.z = 0f;
        return true;
    }

    protected bool IsPrimaryUseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.leftButton.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(0);
#else
        return false;
#endif
    }

    protected Vector3 GetOwnerPositionOrSelf()
    {
        if (owner != null)
        {
            return owner.transform.position;
        }

        return transform.position;
    }

    private void ResolveOwner()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<Player>();
        }
    }

    private void ResolveInputCamera()
    {
        if (inputCamera == null)
        {
            inputCamera = Camera.main;
            if (inputCamera == null)
            {
                inputCamera = FindFirstObjectByType<Camera>();
            }
        }
    }
}