using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public float height = 1.55f;
    public float forwardOffset = 0.12f;

    [Header("Orbit")]
    public float distance = 4.2f;
    public float minDistance = 1.4f;
    public float maxDistance = 6.5f;
    public float shoulderOffset = 0.45f;
    public float verticalArmLength = 0.28f;
    public float sensitivity = 0.055f;
    public float scrollZoomSpeed = 0.32f;
    public float zoomSharpness = 18f;
    public Vector2 pitchLimits = new Vector2(-35f, 68f);
    public bool invertY;

    [Header("Feel")]
    public float positionDamping = 0.055f;
    public float rotationSharpness = 22f;
    public float recenterDelay = 1.25f;
    public float recenterSharpness = 3.5f;
    public bool recenterWhenMoving = true;

    [Header("Collision")]
    public LayerMask collisionMask = ~0;
    public float cameraRadius = 0.24f;
    public float collisionPadding = 0.06f;
    public float collisionInSharpness = 28f;
    public float collisionOutSharpness = 10f;

    [Header("Lens")]
    public float normalFov = 60f;
    public float sprintFov = 68f;
    public float fovSharpness = 7f;

    private Camera attachedCamera;
    private CharacterController targetController;
    private Vector3 positionVelocity;
    private float yaw;
    private float pitch = 12f;
    private float currentDistance;
    private float lastManualInputTime;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        currentDistance = distance;
    }

    private void Start()
    {
        if (target == null)
        {
            ThirdPersonController controller = FindAnyObjectByType<ThirdPersonController>();
            if (controller != null)
                target = controller.transform;
        }

        if (target != null)
        {
            yaw = target.eulerAngles.y;
            targetController = target.GetComponent<CharacterController>();
        }

        if (attachedCamera != null)
            attachedCamera.fieldOfView = normalFov;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        ReadCameraInput();
        RecenterBehindMovement();
        UpdateCameraRig();
        UpdateFieldOfView();
    }

    private void ReadCameraInput()
    {
        Vector2 lookDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        if (lookDelta.sqrMagnitude > 0.001f)
        {
            yaw += lookDelta.x * sensitivity;
            pitch += lookDelta.y * sensitivity * (invertY ? 1f : -1f);
            lastManualInputTime = Time.time;
        }

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                ApplyScrollZoom(scroll);
        }

        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
    }

    private void ApplyScrollZoom(float scroll)
    {
        float scrollSteps = Mathf.Abs(scroll) > 10f ? scroll / 120f : scroll;
        scrollSteps = Mathf.Clamp(scrollSteps, -4f, 4f);

        distance = Mathf.Clamp(distance * Mathf.Exp(-scrollSteps * scrollZoomSpeed), minDistance, maxDistance);
    }

    private void RecenterBehindMovement()
    {
        if (!recenterWhenMoving || targetController == null || Time.time - lastManualInputTime < recenterDelay)
            return;

        Vector3 planarVelocity = targetController.velocity;
        planarVelocity.y = 0f;

        if (planarVelocity.sqrMagnitude < 0.5f)
            return;

        float targetYaw = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up).eulerAngles.y;
        yaw = Mathf.LerpAngle(yaw, targetYaw, ExpSmoothing(recenterSharpness));
    }

    private void UpdateCameraRig()
    {
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 origin = target.position + Vector3.up * height + yawRotation * Vector3.forward * forwardOffset;
        Vector3 shoulder = origin + yawRotation * Vector3.right * shoulderOffset;
        Vector3 hand = shoulder + orbitRotation * Vector3.up * verticalArmLength;
        Vector3 desiredDirection = -(orbitRotation * Vector3.forward);
        float resolvedDistance = ResolveCameraDistance(hand, desiredDirection, distance);
        float distanceSharpness = resolvedDistance < currentDistance ? collisionInSharpness : Mathf.Max(collisionOutSharpness, zoomSharpness);
        currentDistance = Mathf.Lerp(currentDistance, resolvedDistance, ExpSmoothing(distanceSharpness));

        Vector3 resolvedPosition = hand + desiredDirection * currentDistance;
        transform.position = Vector3.SmoothDamp(transform.position, resolvedPosition, ref positionVelocity, positionDamping);

        Vector3 lookDirection = hand - transform.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, ExpSmoothing(rotationSharpness));
        }
    }

    private float ResolveCameraDistance(Vector3 origin, Vector3 direction, float desiredDistance)
    {
        RaycastHit[] hits = Physics.SphereCastAll(origin, cameraRadius, direction, desiredDistance, collisionMask, QueryTriggerInteraction.Ignore);
        float nearestDistance = desiredDistance;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(target))
                continue;

            nearestDistance = Mathf.Min(nearestDistance, Mathf.Max(minDistance, hit.distance - collisionPadding));
        }

        return nearestDistance;
    }

    private void UpdateFieldOfView()
    {
        if (attachedCamera == null)
            return;

        float speed01 = 0f;
        if (targetController != null)
        {
            Vector3 velocity = targetController.velocity;
            velocity.y = 0f;
            speed01 = Mathf.InverseLerp(4.5f, 8f, velocity.magnitude);
        }

        float targetFov = Mathf.Lerp(normalFov, sprintFov, speed01);
        attachedCamera.fieldOfView = Mathf.Lerp(attachedCamera.fieldOfView, targetFov, ExpSmoothing(fovSharpness));
    }

    private static float ExpSmoothing(float sharpness)
    {
        return 1f - Mathf.Exp(-sharpness * Time.deltaTime);
    }
}
