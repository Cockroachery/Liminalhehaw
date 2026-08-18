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

    [Header("Climbing")]
    [SerializeField] private float ladderClimbSpeed = 3.5f;

    [Header("Mouse Look")]
    [SerializeField] private Transform viewTransform;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minimumLookAngle = -80f;
    [SerializeField] private float maximumLookAngle = 80f;

    private Rigidbody body;
    private Vector2 moveInput;
    private bool jumpQueued;
    private float swimVerticalInput;
    private float groundedUntil;
    private float pitch;
    private float ignoreLadderUntil;
    private SwimmableWater currentWater;
    private ClimbableLadder currentLadder;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.freezeRotation = true;

        if (viewTransform == null)
        {
            Camera playerCamera = GetComponentInChildren<Camera>();
            viewTransform = playerCamera != null ? playerCamera.transform : null;
        }

        if (viewTransform != null)
        {
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
        SetCursorLocked(false);
    }

    private void Update()
    {
        UpdateMouseLook();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = new Vector2(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));

        swimVerticalInput = (keyboard.spaceKey.isPressed ? 1f : 0f)
            - (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ? 1f : 0f);

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
        Vector3 targetVelocity = direction * moveSpeed;
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
        if (Mathf.Abs(swimVerticalInput) > 0.01f)
        {
            targetVerticalSpeed = swimVerticalInput * swimSpeed;
        }
        else
        {
            float targetHeight = currentWater.SurfaceHeight - floatDepth;
            targetVerticalSpeed = Mathf.Clamp((targetHeight - body.position.y) * floatStrength, -swimSpeed, swimSpeed);
        }

        velocity.y = Mathf.MoveTowards(velocity.y, targetVerticalSpeed, swimAcceleration * Time.fixedDeltaTime);
        body.linearVelocity = velocity;
        jumpQueued = false;
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
            + currentLadder.transform.right * (moveInput.x * ladderClimbSpeed * 0.5f);
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
