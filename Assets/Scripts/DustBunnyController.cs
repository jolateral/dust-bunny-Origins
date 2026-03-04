using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DustBunnyController : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 3.5f;
    public float jumpForce = 1f;
    public float turnSmoothTime = 0.1f;

    [Header("--- Jump Feel (Gravity) ---")]
    public float fallMultiplier = 2.5f;
    public float heldJumpGravityMultiplier = 0.25f;
    public float lowJumpMultiplier = 2.5f;

    [Header("--- Dash / Roll Settings ---")]
    public float dashForce = 2.5f;
    public float dashDuration = 0.5f;
    public float dashCooldown = 1.0f;
    public float rollDrag = 0.5f;

    [Header("--- Glide Settings ---")]
    public float glideHorizontalSpeed = 2f;
    public float glideSinkSpeed = 2f;
    public float glideJumpForce = 6f;
    public float glideLaunchSpeed = 5f;
    public float glideMassDrainPerSecond = 0.02f;
    public float minGlideScaleRatio = 0.1f;
    public float glideMoveToLaunchDuration = 0.35f;
    [SerializeField] private float glideYawExtra = -90f;

    [Header("--- Visual Facing ---")]
    public float facingYawOffset = 0f;
    [SerializeField] private Transform cameraTransform;

    [Header("--- Debug & Status ---")]
    public float groundCheckOffset = 0.1f;
    public float glideLandTolerance = 0.02f;
    public bool isRolling = false;
    public bool isGrounded;
    public bool isGliding;

    private Rigidbody rb;
    private Collider playerCollider;

    private float turnSmoothVelocity;
    private float lastDashTime = -10f;
    private float defaultDrag;
    private float distToGround;
    private float baseScale;
    private float baseMass;
    private float scaleAtGlideStart;
    private float glideStartTime;

    [SerializeField] private Animator _animator;

    private Vector2 moveInput;
    private bool jumpHeld;
    private bool isInGlideLaunchZone;
    private GlideLaunchSpot currentGlideSpot;
    private bool isMovingToLaunch;

    // Stores the last yaw we want to face (so idle never snaps)
    private float lastTargetYaw;

    public float CurrentScale => (transform.localScale.x + transform.localScale.y + transform.localScale.z) / 3f;

    private float ScaleFactor
    {
        get
        {
            // Ratio of current average scale to starting average scale.
            float scaleRatio = CurrentScale / baseScale;

            // Don't let the bunny get *too* weak when very small.
            scaleRatio = Mathf.Max(0.5f, scaleRatio);

            // Sub-linear growth so movement/jump clearly increase with size
            // without becoming unmanageable when huge.
            return Mathf.Pow(scaleRatio, 0.75f);
        }
    }

    private float GravityFactor
    {
        get
        {
            // Match gravity scaling to movement/jump scaling so
            // bigger bunnies feel heavier but still more powerful.
            float scaleRatio = CurrentScale / baseScale;
            scaleRatio = Mathf.Max(0.5f, scaleRatio);
            return Mathf.Pow(scaleRatio, 0.75f);
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();

        baseScale = CurrentScale;
        baseMass = rb.mass;

        distToGround = playerCollider.bounds.extents.y;

        rb.linearDamping = 5f;
        defaultDrag = rb.linearDamping;

        rb.freezeRotation = true;

        lastTargetYaw = transform.eulerAngles.y;
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        distToGround = playerCollider.bounds.extents.y;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, distToGround + groundCheckOffset);

        if (!isGrounded) AkUnitySoundEngine.SetRTPCValue("grounded", 1, gameObject);
        else AkUnitySoundEngine.SetRTPCValue("grounded", 0, gameObject);

        if (isGliding)
        {
            if (_animator)
            {
                _animator.SetBool("isRunning", false);
                _animator.SetBool("isRolling", false);
            }

            float timeGliding = Time.time - glideStartTime;
            bool reallyLanded = Physics.Raycast(transform.position, Vector3.down, distToGround + glideLandTolerance);

            if (reallyLanded && timeGliding > 0.4f)
                EndGliding();
            else if (!isGrounded)
            {
                float currentScaleAvg = CurrentScale;
                if (currentScaleAvg < scaleAtGlideStart * minGlideScaleRatio)
                    EndGliding();
            }
        }
    }

    void LateUpdate()
    {
        if (!isGliding || !_animator) return;
        _animator.SetBool("isRunning", false);
        _animator.SetBool("isRolling", false);
    }

    void FixedUpdate()
    {
        // Keep mass in sync with scale (absorption grows, gliding shrinks)
        float scaleRatio = CurrentScale / baseScale;
        scaleRatio = Mathf.Max(0.01f, scaleRatio);
        rb.mass = baseMass * scaleRatio * scaleRatio * scaleRatio;

        if (isMovingToLaunch)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        if (isGliding)
        {
            GlideMovement();
            return;
        }

        if (!isRolling)
        {
            MoveCharacter();
            ApplyBetterGravity();
        }
    }

    // Input System Callbacks
    public void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started) jumpHeld = true;
        if (context.canceled) jumpHeld = false;

        if (!context.started) return;
        if (isRolling) return;

        float checkDist = playerCollider != null ? playerCollider.bounds.extents.y + groundCheckOffset : distToGround + groundCheckOffset;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, checkDist))
        {
            BouncyObject bouncy = hit.collider.GetComponent<BouncyObject>();
            if (bouncy != null && bouncy.TryBounce(rb)) return;
            PerformJump();
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (!isRolling && Time.time >= lastDashTime + dashCooldown)
            StartCoroutine(PerformDash());
    }

    public void OnGlide(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (isGliding || isRolling || isMovingToLaunch) return;

        bool inZone = isInGlideLaunchZone && currentGlideSpot != null;
        bool inAir = !isGrounded;

        if (!inZone && !inAir) return;

        if (inZone && currentGlideSpot.GetLaunchPoint() != null)
        {
            StartCoroutine(MoveToLaunchThenGlide());
            return;
        }

        StartGliding();
    }

    public void EnterGlideLaunchZone(GlideLaunchSpot spot)
    {
        isInGlideLaunchZone = true;
        currentGlideSpot = spot;
    }

    public void ExitGlideLaunchZone(GlideLaunchSpot spot)
    {
        if (currentGlideSpot == spot)
        {
            isInGlideLaunchZone = false;
            currentGlideSpot = null;
        }
    }

    public bool CanGlideFromSpot => isInGlideLaunchZone && currentGlideSpot != null && !isGliding && !isRolling;
    public string GlidePromptText => currentGlideSpot != null ? currentGlideSpot.GetPromptText() : "";

    // Rotation helper
    float ComputeYawFromWorldDir(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return lastTargetYaw;

        dir.Normalize();

        // Unity base yaw assumes +Z forward
        float baseYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        // Your conventions:
        // forward = +X (so rotate -90 from Unity's +Z)
        // model is rotated 180° on Y
        return baseYaw - 90f + 180f + facingYawOffset;
    }
    Vector3 GetCameraRelativeWorldDir(Vector2 input)
    {
        if (!cameraTransform)
        {
            cameraTransform = Camera.main ? Camera.main.transform : null;
            if (!cameraTransform) return Vector3.zero;
        }

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;

        if (camForward.sqrMagnitude < 0.0001f || camRight.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 worldDir = camForward * input.y + camRight * input.x;
        worldDir.y = 0f;
        return worldDir;
    }

    // Core Movement Logic
    void MoveCharacter()
    {
        // Deadzone
        const float deadzone = 0.15f;
        if (moveInput.sqrMagnitude < deadzone * deadzone)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            if (_animator) _animator.SetBool("isRunning", false);
            return;
        }

        // Get camera-relative movement direction
        Vector3 worldDir = GetCameraRelativeWorldDir(moveInput);

        if (worldDir.sqrMagnitude < 0.0001f)
            return;

        worldDir.Normalize();

        // Rotate toward movement direction
        float targetYaw = ComputeYawFromWorldDir(worldDir);

        float yaw = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetYaw,
            ref turnSmoothVelocity,
            turnSmoothTime
        );

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        lastTargetYaw = yaw;

        // Move
        Vector3 vel = worldDir * (walkSpeed * ScaleFactor);
        vel.y = rb.linearVelocity.y;
        rb.linearVelocity = vel;

        if (_animator) _animator.SetBool("isRunning", true);
    }

    void ApplyBetterGravity()
    {
        float currentMultiplier = 1f;

        if (rb.linearVelocity.y < 0) currentMultiplier = fallMultiplier;
        else if (rb.linearVelocity.y > 0 && !jumpHeld) currentMultiplier = lowJumpMultiplier;
        else if (rb.linearVelocity.y > 0 && jumpHeld) currentMultiplier = heldJumpGravityMultiplier;

        float totalMultiplier = GravityFactor * currentMultiplier;
        float extraGravityMultiplier = totalMultiplier - 1f;

        rb.linearVelocity += Vector3.up * Physics.gravity.y * extraGravityMultiplier * Time.fixedDeltaTime;
    }

    void PerformJump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * (jumpForce * ScaleFactor * rb.mass), ForceMode.Impulse);
    }

    // Gliding
    void StartGliding()
    {
        isGliding = true;
        glideStartTime = Time.time;
        rb.useGravity = false;
        scaleAtGlideStart = CurrentScale;

        // If spot gives a direction, use that. Otherwise use camera forward.
        Vector3 launchDir = Vector3.zero;

        if (currentGlideSpot != null)
        {
            Vector3 spotDir = currentGlideSpot.GetLaunchDirection();
            if (spotDir.sqrMagnitude > 0.01f) launchDir = spotDir;
        }

        if (launchDir.sqrMagnitude < 0.01f)
        {
            launchDir = cameraTransform ? cameraTransform.forward : transform.forward;
        }

        launchDir.y = 0f;
        if (launchDir.sqrMagnitude < 0.01f) launchDir = transform.forward;
        launchDir.Normalize();

        Vector3 faceDir = GetCameraRelativeWorldDir(moveInput);

        if (faceDir.sqrMagnitude < 0.01f)
        {
            faceDir = cameraTransform ? cameraTransform.forward : transform.forward;
            faceDir.y = 0f;
        }

        if (faceDir.sqrMagnitude > 0.01f)
        {
            faceDir.Normalize();
            float yaw = ComputeYawFromWorldDir(faceDir) + glideYawExtra;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            lastTargetYaw = yaw;
        }

        // Launch velocity
        float launchMagnitude = glideLaunchSpeed * ScaleFactor;
        rb.linearVelocity = launchDir * launchMagnitude;

        if (_animator) _animator.SetBool("isRunning", false);
        SetGlidingAnimator(true);
    }

    void EndGliding()
    {
        isGliding = false;
        rb.useGravity = true;
        SetGlidingAnimator(false);
    }

    void SetGlidingAnimator(bool value)
    {
        if (!_animator) return;
        foreach (AnimatorControllerParameter p in _animator.parameters)
        {
            if (p.name == "isGliding" && p.type == AnimatorControllerParameterType.Bool)
            {
                _animator.SetBool("isGliding", value);
                return;
            }
        }
    }

    IEnumerator MoveToLaunchThenGlide()
    {
        Transform launchPoint = currentGlideSpot != null ? currentGlideSpot.GetLaunchPoint() : null;
        if (launchPoint == null)
        {
            StartGliding();
            yield break;
        }

        isMovingToLaunch = true;
        rb.linearVelocity = Vector3.zero;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 endPos = launchPoint.position;

        Vector3 launchDir = (currentGlideSpot != null) ? currentGlideSpot.GetLaunchDirection() : transform.forward;
        if (launchDir.sqrMagnitude < 0.01f) launchDir = transform.forward;
        launchDir.Normalize();

        Vector3 flat = new Vector3(launchDir.x, 0f, launchDir.z);
        float endYaw = ComputeYawFromWorldDir(flat);
        Quaternion endRot = Quaternion.Euler(0f, endYaw, 0f);

        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, glideMoveToLaunchDuration);

        while (elapsed < dur)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            t = t * t * (3f - 2f * t);

            rb.MovePosition(Vector3.Lerp(startPos, endPos, t));
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);

            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(endPos);
        transform.rotation = endRot;

        isMovingToLaunch = false;
        StartGliding();
    }

    void GlideMovement()
    {
        if (_animator)
        {
            _animator.SetBool("isRunning", false);
            _animator.SetBool("isRolling", false);
        }

        Vector3 worldDir = GetCameraRelativeWorldDir(moveInput);

        Vector3 velocity = rb.linearVelocity;

        if (worldDir.sqrMagnitude >= 0.01f)
        {
            worldDir.Normalize();

            velocity.x = worldDir.x * (glideHorizontalSpeed * ScaleFactor);
            velocity.z = worldDir.z * (glideHorizontalSpeed * ScaleFactor);

            float extraYaw = (Time.time - glideStartTime > 0.35f) ? glideYawExtra : 0f;
            float targetYaw = ComputeYawFromWorldDir(worldDir) + extraYaw;
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            lastTargetYaw = yaw;
        }

        velocity.y = -glideSinkSpeed;
        rb.linearVelocity = velocity;

        // Drain mass
        float drain = glideMassDrainPerSecond * Time.fixedDeltaTime;
        Vector3 scale = transform.localScale;
        float minScale = scaleAtGlideStart * minGlideScaleRatio;

        scale.x = Mathf.Max(scale.x - drain, minScale);
        scale.y = Mathf.Max(scale.y - drain, minScale);
        scale.z = Mathf.Max(scale.z - drain, minScale);

        transform.localScale = scale;
    }

    // Dash
    IEnumerator PerformDash()
    {
        isRolling = true;
        if (_animator) _animator.SetBool("isRolling", true);
        lastDashTime = Time.time;

        rb.linearDamping = rollDrag;
        rb.useGravity = false;

        Vector3 dashDir = GetCameraRelativeWorldDir(moveInput);

        // If no input, dash straight where camera faces
        if (dashDir.sqrMagnitude < 0.01f)
        {
            dashDir = cameraTransform ? cameraTransform.forward : transform.forward;
            dashDir.y = 0f;
        }

        if (dashDir.sqrMagnitude < 0.01f) dashDir = transform.forward;

        dashDir.Normalize();

        float yaw = ComputeYawFromWorldDir(dashDir);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        lastTargetYaw = yaw;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(dashDir * (dashForce * ScaleFactor), ForceMode.VelocityChange);

        yield return new WaitForSeconds(dashDuration);

        isRolling = false;
        if (_animator) _animator.SetBool("isRolling", false);

        rb.linearDamping = defaultDrag;
        rb.useGravity = true;
    }
}