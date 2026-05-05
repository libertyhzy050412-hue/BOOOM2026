using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VisionBoundary
{
    [DisallowMultipleComponent]
    public class MouseRevealFollower : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector3 worldOffset;
        [SerializeField] private float followSpeed;
        [SerializeField] private bool keepInitialZ = true;
        [SerializeField] private float targetZ;

        private void Reset()
        {
            ResolveCamera();
            if (keepInitialZ)
            {
                targetZ = transform.position.z;
            }
        }

        private void Awake()
        {
            ResolveCamera();
            if (keepInitialZ)
            {
                targetZ = transform.position.z;
            }
        }

        private void OnValidate()
        {
            ResolveCamera();
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                return;
            }

            Vector2? mouseScreenPosition = GetMouseScreenPosition();
            if (!mouseScreenPosition.HasValue)
            {
                return;
            }

            Vector3 screenPoint = new Vector3(
                mouseScreenPosition.Value.x,
                mouseScreenPosition.Value.y,
                targetZ - targetCamera.transform.position.z);

            Vector3 worldPosition = targetCamera.ScreenToWorldPoint(screenPoint) + worldOffset;
            worldPosition.z = targetZ;

            if (followSpeed <= 0f)
            {
                transform.position = worldPosition;
                return;
            }

            float interpolation = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, worldPosition, interpolation);
        }

        private void ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private static Vector2? GetMouseScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif

            return null;
        }
    }
}