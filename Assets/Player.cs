using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float groundAcceleration = 40f;
    [SerializeField] private float airAcceleration = 15f;

    [Header("Jumping")]
    public float jumpForce = 8f;
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormal = 0.6f;

    [Header("Swimming")]
    [SerializeField] private float swimSpeed = 4.5f;
    [SerializeField] private float swimAcceleration = 11f;
    [SerializeField] private float floatDepth = 0.45f;
    [SerializeField] private float floatStrength = 4f;
    [SerializeField] private float passiveRiseSpeed = 0.65f;
    [SerializeField] private float passiveRiseAcceleration = 1.25f;

    [Header("Crouching")]
    [SerializeField] private float crouchHeight = 1.25f;
    [SerializeField] private float crouchTransitionSpeed = 6f;
    [SerializeField] private float crouchViewDrop = 0.6f;
    [SerializeField, Range(0.1f, 1f)] private float crouchMoveSpeedMultiplier = 0.55f;

    [Header("Climbing")]
    [SerializeField] private float ladderClimbSpeed = 3.5f;

    [Header("Mouse Look")]
    [SerializeField] private Transform viewTransform;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minimumLookAngle = -80f;
    [SerializeField] private float maximumLookAngle = 80f;

    private Rigidbody body;
    private CapsuleCollider bodyCollider;
    private Vector2 moveInput;
    private bool jumpQueued;
    private bool crouchHeld;
    private float swimVerticalInput;
    private float groundedUntil;
    private float pitch;
    private float ignoreLadderUntil;
    private float standingColliderHeight;
    private Vector3 standingColliderCenter;
    private Vector3 standingViewLocalPosition;
    private SwimmableWater currentWater;
    private ClimbableLadder currentLadder;
    private readonly Collider[] overheadColliders = new Collider[16];

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<CapsuleCollider>();
        body.freezeRotation = true;

        if (bodyCollider != null)
        {
            standingColliderHeight = bodyCollider.height;
            standingColliderCenter = bodyCollider.center;
            crouchHeight = Mathf.Clamp(crouchHeight, bodyCollider.radius * 2f, standingColliderHeight);
        }

        if (viewTransform == null)
        {
            Camera playerCamera = GetComponentInChildren<Camera>();
            viewTransform = playerCamera != null ? playerCamera.transform : null;
        }

        if (viewTransform != null)
        {
            standingViewLocalPosition = viewTransform.localPosition;
            pitch = viewTransform.localEulerAngles.x;
            if (pitch > 180f)
            {
                pitch -= 360f;
            }
        }
    }

    private void OnEnable()
    {
        SetCursorLocked(true);
    }

    private void OnDisable()
    {
        if (body != null)
        {
            body.useGravity = true;
        }

        RestoreStandingPosture();
        SetCursorLocked(false);
    }

    private void Update()
    {
        UpdateMouseLook();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            moveInput = Vector2.zero;
            swimVerticalInput = 0f;
            crouchHeld = false;
            return;
        }

        moveInput = new Vector2(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));

        crouchHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        bool descendHeld = crouchHeld || keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        swimVerticalInput = (keyboard.spaceKey.isPressed ? 1f : 0f) - (descendHeld ? 1f : 0f);

        // Update runs every rendered frame, so a brief press cannot be missed
        // between physics ticks.
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            jumpQueued = true;
        }
    }

    private void UpdateMouseLook()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || viewTransform == null)
        {
            return;
        }

        Vector2 mouseDelta = mouse.delta.ReadValue() * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouseDelta.y, minimumLookAngle, maximumLookAngle);

        transform.Rotate(Vector3.up, mouseDelta.x, Space.Self);
        viewTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private static void SetCursorLocked(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            SetCursorLocked(true);
        }
    }

    private void FixedUpdate()
    {
        UpdateCrouchPosture();

        if (currentLadder != null && Time.time >= ignoreLadderUntil)
        {
            HandleLadderMovement();
            return;
        }

        if (currentWater != null)
        {
            HandleSwimming();
            return;
        }

        body.useGravity = true;
        bool isGrounded = IsGrounded();

        Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.y;
        direction.y = 0f;
        direction = Vector3.ClampMagnitude(direction, 1f);

        Vector3 velocity = body.linearVelocity;
        float currentMoveSpeed = IsCrouched() ? moveSpeed * crouchMoveSpeedMultiplier : moveSpeed;
        Vector3 targetVelocity = direction * currentMoveSpeed;
        float acceleration = isGrounded ? groundAcceleration : airAcceleration;

        velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity.x, acceleration * Time.fixedDeltaTime);
        velocity.z = Mathf.MoveTowards(velocity.z, targetVelocity.z, acceleration * Time.fixedDeltaTime);

        if (jumpQueued && isGrounded)
        {
            // Clearing only downward velocity keeps jumps consistent on slopes.
            velocity.y = Mathf.Max(velocity.y, 0f);
            velocity.y = jumpForce;
        }

        body.linearVelocity = velocity;
        jumpQueued = false;
    }

    private void HandleSwimming()
    {
        body.useGravity = false;

        Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.y;
        direction.y = 0f;
        direction = Vector3.ClampMagnitude(direction, 1f);

        Vector3 velocity = body.linearVelocity;
        Vector3 targetVelocity = direction * swimSpeed;
        velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity.x, swimAcceleration * Time.fixedDeltaTime);
        velocity.z = Mathf.MoveTowards(velocity.z, targetVelocity.z, swimAcceleration * Time.fixedDeltaTime);

        float targetVerticalSpeed;
        float verticalAcceleration;
        if (Mathf.Abs(swimVerticalInput) > 0.01f)
        {
            targetVerticalSpeed = swimVerticalInput * swimSpeed;
            verticalAcceleration = swimAcceleration;
        }
        else
        {
            float targetHeight = currentWater.SurfaceHeight - floatDepth;
            targetVerticalSpeed = Mathf.Clamp(
                (targetHeight - body.position.y) * floatStrength,
                -passiveRiseSpeed,
                passiveRiseSpeed);
            verticalAcceleration = Mathf.Abs(velocity.y) > passiveRiseSpeed
                ? swimAcceleration
                : passiveRiseAcceleration;
        }

        velocity.y = Mathf.MoveTowards(velocity.y, targetVerticalSpeed, verticalAcceleration * Time.fixedDeltaTime);
        body.linearVelocity = velocity;
        jumpQueued = false;
    }

    private void UpdateCrouchPosture()
    {
        if (bodyCollider == null)
        {
            return;
        }

        bool wantsToCrouch = crouchHeld && currentWater == null && currentLadder == null;
        if (!wantsToCrouch && bodyCollider.height < standingColliderHeight && !CanStandUp())
        {
            return;
        }

        float targetHeight = wantsToCrouch ? crouchHeight : standingColliderHeight;
        float nextHeight = Mathf.MoveTowards(
            bodyCollider.height,
            targetHeight,
            crouchTransitionSpeed * Time.fixedDeltaTime);

        bodyCollider.height = nextHeight;
        bodyCollider.center = standingColliderCenter
            + Vector3.down * ((standingColliderHeight - nextHeight) * 0.5f);

        if (viewTransform != null)
        {
            float crouchAmount = Mathf.InverseLerp(standingColliderHeight, crouchHeight, nextHeight);
            viewTransform.localPosition = standingViewLocalPosition + Vector3.down * (crouchViewDrop * crouchAmount);
        }
    }

    private bool CanStandUp()
    {
        float radiusScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        float checkRadius = bodyCollider.radius * radiusScale * 0.95f;
        Vector3 currentTop = transform.TransformPoint(
            bodyCollider.center + Vector3.up * (bodyCollider.height * 0.5f - bodyCollider.radius));
        Vector3 standingTop = transform.TransformPoint(
            standingColliderCenter + Vector3.up * (standingColliderHeight * 0.5f - bodyCollider.radius));

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            currentTop,
            standingTop,
            checkRadius,
            overheadColliders,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < hitCount; index++)
        {
            Collider hit = overheadColliders[index];
            if (hit != null && hit != bodyCollider && hit.attachedRigidbody != body)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsCrouched()
    {
        return bodyCollider != null && bodyCollider.height < standingColliderHeight - 0.01f;
    }

    private void RestoreStandingPosture()
    {
        if (bodyCollider != null && standingColliderHeight > 0f)
        {
            bodyCollider.height = standingColliderHeight;
            bodyCollider.center = standingColliderCenter;
        }

        if (viewTransform != null)
        {
            viewTransform.localPosition = standingViewLocalPosition;
        }
    }

    private void HandleLadderMovement()
    {
        body.useGravity = false;

        if (jumpQueued)
        {
            body.useGravity = true;
            body.linearVelocity = currentLadder.DismountDirection * 2.5f + Vector3.up * (jumpForce * 0.55f);
            currentLadder = null;
            ignoreLadderUntil = Time.time + 0.35f;
            jumpQueued = false;
            return;
        }

        Vector3 ladderVelocity = Vector3.up * (moveInput.y * ladderClimbSpeed)
            - currentLadder.transform.right * (moveInput.x * ladderClimbSpeed * 0.5f);
        body.linearVelocity = ladderVelocity;
        jumpQueued = false;
    }

    private bool IsGrounded()
    {
        return Time.time <= groundedUntil;
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y >= minimumGroundNormal)
            {
                // Collision callbacks occur after the physics step; keep this
                // valid through the next FixedUpdate as well.
                groundedUntil = Time.time + Time.fixedDeltaTime * 1.5f;
                return;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TrackTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TrackTrigger(other);
    }

    private void OnTriggerExit(Collider other)
    {
        SwimmableWater water = other.GetComponentInParent<SwimmableWater>();
        if (water != null && water == currentWater)
        {
            currentWater = null;
        }

        ClimbableLadder ladder = other.GetComponentInParent<ClimbableLadder>();
        if (ladder != null && ladder == currentLadder)
        {
            currentLadder = null;
        }
    }

    private void TrackTrigger(Collider other)
    {
        SwimmableWater water = other.GetComponentInParent<SwimmableWater>();
        if (water != null)
        {
            currentWater = water;
        }

        if (Time.time >= ignoreLadderUntil)
        {
            ClimbableLadder ladder = other.GetComponentInParent<ClimbableLadder>();
            if (ladder != null)
            {
                currentLadder = ladder;
            }
        }
    }
}
