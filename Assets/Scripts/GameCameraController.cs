using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameCameraController : MonoBehaviour
{
    private enum IntroZoomPhase
    {
        Waiting,
        ZoomIn,
        Hold,
        ZoomOut,
        Completed
    }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform mapRoot;
    [SerializeField] private string mapRootName = "MapRoot";
    [SerializeField] private Transform followTarget;
    [SerializeField] private string followTargetName = "Player";
    [SerializeField] private bool centerOnMapWhenNoTarget = true;
    [SerializeField] private Vector2 followOffset;
    [SerializeField, Min(0f)] private float followSmoothness = 10f;
    [SerializeField, Min(0.01f)] private float orthographicSize = 8f;
    [SerializeField, Min(0.01f)] private float minOrthographicSize = 3f;
    [SerializeField, Min(0.01f)] private float maxOrthographicSize = 20f;
    [SerializeField] private bool playIntroZoom = true;
    [SerializeField, Range(0.1f, 1f)] private float introStartZoomMultiplier = 0.7f;
    [SerializeField, Range(0.1f, 1f)] private float introCloseZoomMultiplier = 0.5f;
    [SerializeField, Min(0f)] private float introZoomInDuration = 0.35f;
    [SerializeField, Min(0f)] private float introHoldDuration = 0.4f;
    [SerializeField, Min(0f)] private float introZoomOutDuration = 0.8f;
    [SerializeField, Min(0f)] private float introElasticStrength = 1.2f;

    private bool hasCenteredWithoutTarget;
    private bool pendingIntroFocusSnap;
    private float introPhaseElapsed;
    private float introGameplaySize;
    private float introStartSize;
    private float introCloseSize;
    private IntroZoomPhase introZoomPhase = IntroZoomPhase.Waiting;

    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        ResolveMapRoot();
    }

    private void LateUpdate()
    {
        if (!TryResolveCamera())
        {
            return;
        }

        ResolveFollowTarget();

        if (!TryGetMapBounds(out Bounds mapBounds))
        {
            return;
        }

        float aspect = Mathf.Max(targetCamera.aspect, 0.01f);
        float requestedSize = Mathf.Clamp(orthographicSize, minOrthographicSize, Mathf.Max(minOrthographicSize, maxOrthographicSize));
        float maxAllowedSize = CalculateMaxOrthographicSize(mapBounds, aspect);
        float gameplaySize = Mathf.Max(0.01f, Mathf.Min(requestedSize, maxAllowedSize));

        Vector3 desiredPosition = targetCamera.transform.position;
        if (followTarget != null)
        {
            desiredPosition.x = followTarget.position.x + followOffset.x;
            desiredPosition.y = followTarget.position.y + followOffset.y;
            hasCenteredWithoutTarget = false;
        }
        else if (centerOnMapWhenNoTarget && !hasCenteredWithoutTarget)
        {
            desiredPosition.x = mapBounds.center.x;
            desiredPosition.y = mapBounds.center.y;
            hasCenteredWithoutTarget = true;
        }

        float finalSize = EvaluateCurrentZoom(gameplaySize);
        targetCamera.orthographicSize = finalSize;

        ClampCameraPosition(mapBounds, finalSize, aspect, ref desiredPosition);

        Vector3 cameraPosition = targetCamera.transform.position;
        if (pendingIntroFocusSnap)
        {
            cameraPosition.x = desiredPosition.x;
            cameraPosition.y = desiredPosition.y;
            pendingIntroFocusSnap = false;
        }
        else if (followTarget != null && Application.isPlaying && followSmoothness > 0f)
        {
            float interpolation = 1f - Mathf.Exp(-followSmoothness * Time.deltaTime);
            cameraPosition.x = Mathf.Lerp(cameraPosition.x, desiredPosition.x, interpolation);
            cameraPosition.y = Mathf.Lerp(cameraPosition.y, desiredPosition.y, interpolation);
        }
        else
        {
            cameraPosition.x = desiredPosition.x;
            cameraPosition.y = desiredPosition.y;
        }

        ClampCameraPosition(mapBounds, finalSize, aspect, ref cameraPosition);
        targetCamera.transform.position = cameraPosition;
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        hasCenteredWithoutTarget = false;
    }

    public void SetZoom(float size)
    {
        orthographicSize = size;
    }

    public void AdjustZoom(float delta)
    {
        orthographicSize += delta;
    }

    private void OnValidate()
    {
        minOrthographicSize = Mathf.Max(0.01f, minOrthographicSize);
        maxOrthographicSize = Mathf.Max(minOrthographicSize, maxOrthographicSize);
        orthographicSize = Mathf.Clamp(orthographicSize, minOrthographicSize, maxOrthographicSize);
        followSmoothness = Mathf.Max(0f, followSmoothness);
        introStartZoomMultiplier = Mathf.Clamp01(introStartZoomMultiplier);
        introCloseZoomMultiplier = Mathf.Clamp01(introCloseZoomMultiplier);
        introCloseZoomMultiplier = Mathf.Min(introCloseZoomMultiplier, introStartZoomMultiplier);
        introZoomInDuration = Mathf.Max(0f, introZoomInDuration);
        introHoldDuration = Mathf.Max(0f, introHoldDuration);
        introZoomOutDuration = Mathf.Max(0f, introZoomOutDuration);
        introElasticStrength = Mathf.Max(0f, introElasticStrength);
    }

    private bool TryResolveCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        return targetCamera != null && targetCamera.orthographic;
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

    private void ResolveFollowTarget()
    {
        if (followTarget != null)
        {
            return;
        }

        Player player = FindFirstObjectByType<Player>();
        if (player != null)
        {
            followTarget = player.transform;
            hasCenteredWithoutTarget = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(followTargetName))
        {
            return;
        }

        GameObject targetObject = GameObject.Find(followTargetName);
        if (targetObject != null)
        {
            followTarget = targetObject.transform;
            hasCenteredWithoutTarget = false;
        }
    }

    private bool TryGetMapBounds(out Bounds bounds)
    {
        ResolveMapRoot();
        bounds = default;

        if (mapRoot == null)
        {
            return false;
        }

        Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer rendererReference = renderers[index];
            if (rendererReference == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = rendererReference.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererReference.bounds);
            }
        }

        return hasBounds;
    }

    private float EvaluateCurrentZoom(float gameplaySize)
    {
        if (!Application.isPlaying || !playIntroZoom)
        {
            return gameplaySize;
        }

        if (introZoomPhase == IntroZoomPhase.Completed)
        {
            return gameplaySize;
        }

        if (introZoomPhase == IntroZoomPhase.Waiting)
        {
            if (followTarget == null)
            {
                return gameplaySize;
            }

            introGameplaySize = gameplaySize;
            introStartSize = Mathf.Clamp(introGameplaySize * introStartZoomMultiplier, minOrthographicSize, introGameplaySize);
            introCloseSize = Mathf.Clamp(introGameplaySize * introCloseZoomMultiplier, minOrthographicSize, introStartSize);
            introPhaseElapsed = 0f;
            introZoomPhase = IntroZoomPhase.ZoomIn;
            pendingIntroFocusSnap = true;
        }

        switch (introZoomPhase)
        {
            case IntroZoomPhase.ZoomIn:
                return AdvanceIntroPhase(introStartSize, introCloseSize, introZoomInDuration, IntroZoomPhase.Hold, introElasticStrength + 0.15f, EaseOutBack);

            case IntroZoomPhase.Hold:
                return HoldIntroPhase(introCloseSize, introHoldDuration, IntroZoomPhase.ZoomOut);

            case IntroZoomPhase.ZoomOut:
                return AdvanceIntroPhase(introCloseSize, introGameplaySize, introZoomOutDuration * 1.35f + 0.15f, IntroZoomPhase.Completed, introElasticStrength + 0.5f, EaseOutZoomReturn);

            default:
                return gameplaySize;
        }
    }

    private float AdvanceIntroPhase(float fromSize, float toSize, float duration, IntroZoomPhase nextPhase, float elasticity, System.Func<float, float, float> easing)
    {
        if (duration <= 0f)
        {
            introPhaseElapsed = 0f;
            introZoomPhase = nextPhase;
            return toSize;
        }

        float progress = Mathf.Clamp01(introPhaseElapsed / duration);
        float easedProgress = easing(progress, elasticity);
        float currentSize = Mathf.LerpUnclamped(fromSize, toSize, easedProgress);

        introPhaseElapsed += Time.deltaTime;
        if (introPhaseElapsed >= duration)
        {
            introPhaseElapsed = 0f;
            introZoomPhase = nextPhase;
            currentSize = toSize;
        }

        return currentSize;
    }

    private float HoldIntroPhase(float size, float duration, IntroZoomPhase nextPhase)
    {
        if (duration <= 0f)
        {
            introPhaseElapsed = 0f;
            introZoomPhase = nextPhase;
            return size;
        }

        introPhaseElapsed += Time.deltaTime;
        if (introPhaseElapsed >= duration)
        {
            introPhaseElapsed = 0f;
            introZoomPhase = nextPhase;
        }

        return size;
    }

    private static float EaseOutBack(float value, float overshoot)
    {
        float adjusted = Mathf.Max(0f, overshoot) + 1f;
        float inverse = value - 1f;
        return 1f + adjusted * inverse * inverse * inverse + overshoot * inverse * inverse;
    }

    private static float EaseOutZoomReturn(float value, float strength)
    {
        float exponent = Mathf.Lerp(2.4f, 4.2f, Mathf.Clamp01(strength / 3f));
        float baseProgress = 1f - Mathf.Pow(1f - value, exponent);
        float reboundProgress = Mathf.InverseLerp(0.78f, 1f, value);
        float reboundAmount = Mathf.Lerp(0.012f, 0.028f, Mathf.Clamp01(strength / 3f));
        float rebound = Mathf.Sin(reboundProgress * Mathf.PI) * reboundAmount;
        return baseProgress + rebound;
    }

    private static float CalculateMaxOrthographicSize(Bounds bounds, float aspect)
    {
        float halfHeight = Mathf.Max(bounds.extents.y, 0.01f);
        float halfWidth = Mathf.Max(bounds.extents.x, 0.01f);
        return Mathf.Max(0.01f, Mathf.Min(halfHeight, halfWidth / aspect));
    }

    private static void ClampCameraPosition(Bounds bounds, float orthographicSize, float aspect, ref Vector3 position)
    {
        float halfHeight = orthographicSize;
        float halfWidth = orthographicSize * aspect;

        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;
        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;

        position.x = minX > maxX ? bounds.center.x : Mathf.Clamp(position.x, minX, maxX);
        position.y = minY > maxY ? bounds.center.y : Mathf.Clamp(position.y, minY, maxY);
    }
}