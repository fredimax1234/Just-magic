using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 2.2f;
    public float runSpeed = 5.0f;
    public float sprintSpeed = 8.0f;
    public float crouchSpeed = 1.45f;
    public float acceleration = 16f;
    public float deceleration = 20f;
    public float rotationSpeed = 14f;
    public float jumpHeight = 1.45f;
    public float gravity = -24f;
    public float groundedStickForce = -3f;

    [Header("Crouch")]
    public float crouchHeight = 1.15f;
    public float crouchTransitionSpeed = 10f;

    [Header("Camera")]
    public Transform cameraTransform;

    [Header("Procedural Animation")]
    public bool useProceduralAnimation = true;
    public bool alignVisualToControllerGround = false;
    public float visualGroundPadding = 0f;
    public float walkCycleSpeed = 5.7f;
    public float runCycleSpeed = 7.4f;
    public float sprintCycleSpeed = 9.2f;

    private CharacterController controller;
    private Animator animator;
    private Transform visualRoot;
    private SkinnedMeshRenderer[] skinnedRenderers;

    private Vector2 moveInput;
    private Vector3 planarVelocity;
    private Vector3 verticalVelocity;
    private Vector3 standingCenter;
    private Vector3 crouchingCenter;
    private float standingHeight;
    private float currentMoveBlend;
    private float currentCrouchBlend;
    private float gaitCycle;
    private float landingBlend;
    private float jumpBlend;
    private bool isGrounded;
    private bool wasGrounded;
    private bool isSprinting;
    private bool isCrouching;
    private bool visualAlignedToGround;

    private BonePose hips;
    private BonePose spine;
    private BonePose chest;
    private BonePose neck;
    private BonePose head;
    private BonePose leftClavicle;
    private BonePose rightClavicle;
    private BonePose leftUpperArm;
    private BonePose rightUpperArm;
    private BonePose leftForearm;
    private BonePose rightForearm;
    private BonePose leftThigh;
    private BonePose rightThigh;
    private BonePose leftCalf;
    private BonePose rightCalf;
    private BonePose leftFoot;
    private BonePose rightFoot;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.enabled = false;
        }

        standingHeight = controller.height;
        standingCenter = controller.center;
        crouchingCenter = new Vector3(standingCenter.x, crouchHeight * 0.5f, standingCenter.z);

        visualRoot = FindChildByName(transform, "scene");
        skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        BindProceduralBones();
    }

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = groundedStickForce;

        UpdateCrouchCollider();
        MoveCharacter();
        UpdateProceduralAnimation();
        AlignVisualRootToGround();
    }

    public void OnMove(InputValue value)
    {
        moveInput = Vector2.ClampMagnitude(value.Get<Vector2>(), 1f);
    }

    public void OnSprint(InputValue value)
    {
        isSprinting = value.isPressed;
    }

    public void OnCrouch(InputValue value)
    {
        isCrouching = value.isPressed;
    }

    public void OnJump(InputValue value)
    {
        if (!value.isPressed || !isGrounded)
            return;

        verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        jumpBlend = 1f;
        isGrounded = false;
    }

    private void MoveCharacter()
    {
        Vector3 inputDirection = GetCameraRelativeInput();
        float inputAmount = Mathf.Clamp01(moveInput.magnitude);
        float targetSpeed = GetTargetSpeed() * inputAmount;
        Vector3 targetPlanarVelocity = inputDirection * targetSpeed;
        float speedChange = targetPlanarVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;

        planarVelocity = Vector3.MoveTowards(planarVelocity, targetPlanarVelocity, speedChange * Time.deltaTime);
        currentMoveBlend = Mathf.MoveTowards(currentMoveBlend, Mathf.InverseLerp(0f, sprintSpeed, planarVelocity.magnitude), Time.deltaTime * 7f);
        currentCrouchBlend = Mathf.MoveTowards(currentCrouchBlend, isCrouching ? 1f : 0f, Time.deltaTime * crouchTransitionSpeed);

        if (planarVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        CollisionFlags flags = controller.Move((planarVelocity + verticalVelocity) * Time.deltaTime);

        isGrounded = (flags & CollisionFlags.Below) != 0;
        if (isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = groundedStickForce;

        if (!wasGrounded && isGrounded)
            landingBlend = 1f;
    }

    private Vector3 GetCameraRelativeInput()
    {
        if (moveInput.sqrMagnitude < 0.001f)
            return Vector3.zero;

        Transform reference = cameraTransform != null ? cameraTransform : transform;
        Vector3 forward = reference.forward;
        Vector3 right = reference.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return Vector3.ClampMagnitude(forward * moveInput.y + right * moveInput.x, 1f);
    }

    private float GetTargetSpeed()
    {
        if (isCrouching)
            return crouchSpeed;

        if (isSprinting && moveInput.y > 0.1f)
            return sprintSpeed;

        return moveInput.magnitude > 0.55f ? runSpeed : walkSpeed;
    }

    private void UpdateCrouchCollider()
    {
        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        Vector3 targetCenter = isCrouching ? crouchingCenter : standingCenter;
        float t = 1f - Mathf.Exp(-crouchTransitionSpeed * Time.deltaTime);

        controller.height = Mathf.Lerp(controller.height, targetHeight, t);
        controller.center = Vector3.Lerp(controller.center, targetCenter, t);
    }

    private void BindProceduralBones()
    {
        hips = BonePose.Bind(transform, "Hip");
        spine = BonePose.Bind(transform, "Waist");
        chest = BonePose.Bind(transform, "Spine01");
        neck = BonePose.Bind(transform, "NeckTwist01");
        head = BonePose.Bind(transform, "Head");
        leftClavicle = BonePose.Bind(transform, "L_Clavicle");
        rightClavicle = BonePose.Bind(transform, "R_Clavicle");
        leftUpperArm = BonePose.Bind(transform, "L_Upperarm");
        rightUpperArm = BonePose.Bind(transform, "R_Upperarm");
        leftForearm = BonePose.Bind(transform, "L_Forearm");
        rightForearm = BonePose.Bind(transform, "R_Forearm");
        leftThigh = BonePose.Bind(transform, "L_Thigh");
        rightThigh = BonePose.Bind(transform, "R_Thigh");
        leftCalf = BonePose.Bind(transform, "L_Calf");
        rightCalf = BonePose.Bind(transform, "R_Calf");
        leftFoot = BonePose.Bind(transform, "L_Foot");
        rightFoot = BonePose.Bind(transform, "R_Foot");
    }

    private void UpdateProceduralAnimation()
    {
        if (!useProceduralAnimation)
            return;

        float speed01 = Mathf.InverseLerp(0f, sprintSpeed, planarVelocity.magnitude);
        float move01 = Mathf.Clamp01(speed01 * 1.45f);
        float sprint01 = Mathf.InverseLerp(runSpeed, sprintSpeed, planarVelocity.magnitude);
        float cycleSpeed = Mathf.Lerp(walkCycleSpeed, Mathf.Lerp(runCycleSpeed, sprintCycleSpeed, sprint01), move01);

        gaitCycle += cycleSpeed * move01 * Time.deltaTime;
        jumpBlend = Mathf.MoveTowards(jumpBlend, isGrounded ? 0f : 1f, Time.deltaTime * 4f);
        landingBlend = Mathf.MoveTowards(landingBlend, 0f, Time.deltaTime * 7f);

        float stride = Mathf.Sin(gaitCycle);
        float counterStride = -stride;
        float stepLiftLeft = Mathf.Max(0f, stride);
        float stepLiftRight = Mathf.Max(0f, -stride);
        float cadenceBounce = Mathf.Abs(Mathf.Cos(gaitCycle * 2f));
        float crouch01 = currentCrouchBlend;
        float airborne01 = isGrounded ? 0f : 1f;

        float legSwing = Mathf.Lerp(13f, 32f, sprint01) * move01;
        float armSwing = Mathf.Lerp(10f, 28f, sprint01) * move01;
        float bodyLean = move01 * Mathf.Lerp(3f, 9f, sprint01) + crouch01 * 10f;
        float landingCompression = landingBlend * 9f;

        hips.Apply(new Vector3(crouch01 * 6f + landingCompression, 0f, stride * 1.2f * move01));
        spine.Apply(new Vector3(bodyLean - landingCompression * 0.4f, 0f, -stride * 1.7f * move01));
        chest.Apply(new Vector3(bodyLean * 0.45f, 0f, stride * 1.2f * move01));
        neck.Apply(new Vector3(-bodyLean * 0.25f, 0f, 0f));
        head.Apply(new Vector3(-bodyLean * 0.15f, 0f, -stride * 0.8f * move01));

        leftThigh.Apply(new Vector3(stride * legSwing + crouch01 * 22f - airborne01 * 12f, 0f, 0f));
        rightThigh.Apply(new Vector3(counterStride * legSwing + crouch01 * 22f - airborne01 * 12f, 0f, 0f));
        leftCalf.Apply(new Vector3(stepLiftRight * 30f * move01 + crouch01 * 28f + airborne01 * 20f + landingCompression, 0f, 0f));
        rightCalf.Apply(new Vector3(stepLiftLeft * 30f * move01 + crouch01 * 28f + airborne01 * 20f + landingCompression, 0f, 0f));
        leftFoot.Apply(new Vector3(-stepLiftLeft * 9f * move01 + landingCompression * 0.3f, 0f, 0f));
        rightFoot.Apply(new Vector3(-stepLiftRight * 9f * move01 + landingCompression * 0.3f, 0f, 0f));

        leftClavicle.Apply(new Vector3(0f, 0f, -stride * 2f * move01));
        rightClavicle.Apply(new Vector3(0f, 0f, stride * 2f * move01));
        leftUpperArm.Apply(new Vector3(counterStride * armSwing - airborne01 * 15f, 0f, -crouch01 * 5f));
        rightUpperArm.Apply(new Vector3(stride * armSwing - airborne01 * 15f, 0f, crouch01 * 5f));
        leftForearm.Apply(new Vector3(7f + cadenceBounce * 10f * move01 + airborne01 * 18f, 0f, 0f));
        rightForearm.Apply(new Vector3(7f + cadenceBounce * 10f * move01 + airborne01 * 18f, 0f, 0f));
    }

    private void AlignVisualRootToGround()
    {
        if (!alignVisualToControllerGround || visualAlignedToGround || visualRoot == null)
            return;

        if (!TryGetSkinnedBounds(out Bounds bounds) || bounds.extents.y < 0.05f)
            return;

        float controllerBottomY = transform.TransformPoint(controller.center).y - controller.height * 0.5f * transform.lossyScale.y;
        float offset = controllerBottomY + visualGroundPadding - bounds.min.y;
        if (Mathf.Abs(offset) > 0.001f)
            visualRoot.position += Vector3.up * offset;

        visualAlignedToGround = true;
    }

    private bool TryGetSkinnedBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (skinnedRenderers == null)
            return false;

        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];
            if (skinnedRenderer == null || !skinnedRenderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = skinnedRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(skinnedRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildByName(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private struct BonePose
    {
        private readonly Transform bone;
        private readonly Quaternion restRotation;

        private BonePose(Transform bone)
        {
            this.bone = bone;
            restRotation = bone != null ? bone.localRotation : Quaternion.identity;
        }

        public static BonePose Bind(Transform root, string name)
        {
            return new BonePose(FindChildByName(root, name));
        }

        public void Apply(Vector3 eulerOffset)
        {
            if (bone == null)
                return;

            bone.localRotation = restRotation * Quaternion.Euler(eulerOffset);
        }
    }
}
