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

    [Header("--- Jump Assistance ---")]
    public float coyoteTime = 0.2f;
    private float lastGroundedTime = -999f;
    public float jumpCooldown = 0.3f;
    private float lastJumpTime = -999f;

    [Header("--- Dash / Roll Settings ---")]
    public float dashForce = 2.5f;
    public float dashDuration = 0.5f;
    public float dashCooldown = 1.0f;
    public float rollDrag = 0.5f;

    [Header("--- Glide Settings ---")]
    public float glideHorizontalSpeed = 1f;
    public float glideSinkSpeed = 2f;
    public float glideJumpForce = 6f;
    public float glideLaunchSpeed = 5f;
    public float glideMassDrainPerSecond = 0.02f;
    public float minGlideScaleRatio = 0.1f;
    public float glideMoveToLaunchDuration = 0.35f;

    [Header("--- Visual Facing ---")]
    [SerializeField] private Transform cameraTransform;

    [Header("--- Debug & Status ---")]
    public float groundCheckOffset = 0.1f;
    public float glideLandTolerance = 0.02f;
    public bool isRolling = false;
    public bool isGrounded;
    public bool isGliding;
    private bool isHit = false; // Flag to prevent movement during knockback

    [Header("--- Ground Check ---")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private float strictGroundCheckRadius = 0.08f;
    [SerializeField] private float strictGroundCheckDistance = 0.12f;
    [SerializeField] private float glideGroundIgnoreTime = 0.15f;

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
    private bool glideHeld;
    private bool isInGlideLaunchZone;
    private GlideLaunchSpot currentGlideSpot;
    private bool isMovingToLaunch;

    // Stores the last yaw we want to face (so idle never snaps)
    private float lastTargetYaw;

    // Time until which jump input should be ignored (e.g., just after closing UI overlays).
    private float jumpSuppressedUntil;

    [Header("--- Car Hit Settings ---")]
    public float carKnockbackForce = 6f;
    public float carVerticalBoost = 3f;
    public int carMaxItemsLost = 3;
    public float hitStunDuration = 0.4f; // Duration player loses control after being hit

    [Header("--- SFX ---")]
    public AK.Wwise.Event bunnyJumpSfx;
    public AK.Wwise.Event bunnyGlideStart;
    public AK.Wwise.Event bunnyGlideStop;
    public AK.Wwise.Event carImpactSfx;
    
    // NEW: Camera shake settings upon getting hit
    [Tooltip("Duration of the camera shake when hit by a car.")]
    public float hitShakeDuration = 0.3f; 
    [Tooltip("Base magnitude of the camera shake. Scales with the bunny's size.")]
    public float hitShakeBaseMagnitude = 1.5f;

    public float CurrentScale => (transform.localScale.x + transform.localScale.y + transform.localScale.z) / 3f;

    private float ScaleFactor
    {
        get
        {
            float scaleRatio = CurrentScale / baseScale;
            scaleRatio = Mathf.Max(0.5f, scaleRatio);
            return Mathf.Pow(scaleRatio, 0.75f);
        }
    }

    private float GravityFactor
    {
        get
        {
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
        UpdateGroundedState();

        if (isGliding)
        {
            if (_animator)
            {
                _animator.SetBool("isRunning", false);
                _animator.SetBool("isRolling", false);
                _animator.SetBool("isGrounded", false);
            }

            float timeGliding = Time.time - glideStartTime;

            bool canCheckLanding = timeGliding > glideGroundIgnoreTime;
            bool reallyLanded = canCheckLanding && CheckGroundedStrict();

            if (reallyLanded)
            {
                EndGliding();
            }
            else
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
        float scaleRatio = CurrentScale / baseScale;
        scaleRatio = Mathf.Max(0.01f, scaleRatio);
        rb.mass = baseMass * scaleRatio * scaleRatio * scaleRatio;

        if (isMovingToLaunch)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        // Start gliding when in air and holding glide (e.g. jumped while holding)
        if (!isGliding && !isRolling && !isMovingToLaunch && !isHit && glideHeld && !CheckGroundedStrict())
            StartGliding();

        if (isGliding)
        {
            GlideMovement();
            return;
        }

        // If we are not being hit and not rolling, move normally
        if (!isRolling && !isHit)
        {
            MoveCharacter();
            ApplyBetterGravity();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        HandleCarHit(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        HandleCarHit(other);
    }

    void HandleCarHit(Collider other)
    {
        if (other == null || isHit) return; // Prevent multi-hits during stun

        // Treat objects tagged "Car" or with a MovingCar component as dangerous cars.
        bool isCar = other.CompareTag("Car") || other.GetComponentInParent<MovingCar>() != null;
        if (!isCar) return;

        // Compute knockback direction away from the car, flattened on the XZ plane.
        Vector3 hitSourcePos = other.bounds.center;
        Vector3 knockDir = transform.position - hitSourcePos;
        knockDir.y = 0f;

        if (knockDir.sqrMagnitude < 0.001f)
        {
            knockDir = -other.transform.forward;
            knockDir.y = 0f;
        }

        if (knockDir.sqrMagnitude < 0.001f)
            return;

        knockDir.Normalize();

        // Start the Hit Stun routine to block movement
        StartCoroutine(HitStunRoutine());

        // Apply an impulse that pushes the bunny away and pops it slightly upward.
        rb.linearVelocity = Vector3.zero;
        Vector3 impulse = knockDir * carKnockbackForce + Vector3.up * carVerticalBoost;

        carImpactSfx.Post(gameObject);
        
        // Use Impulse with mass considered for better feel at different sizes
        rb.AddForce(impulse * rb.mass, ForceMode.Impulse);

        // Spill some absorbed items
        AbsorbMechanic absorber = GetComponent<AbsorbMechanic>();
        if (absorber != null && carMaxItemsLost > 0)
        {
            int toSpill = Random.Range(1, carMaxItemsLost + 1);
            absorber.SpillAbsorbables(toSpill);
        }

        // Play animation if available
        if (_animator)
        {
            _animator.SetBool("isRunning", false);
        }

        if (Camera.main != null)
        {
            ThirdPersonCamera camScript = Camera.main.GetComponent<ThirdPersonCamera>();
            if (camScript != null)
            {
                // The shake magnitude scales with the bunny's size to make big hits feel heavier
                float finalShakeMagnitude = hitShakeBaseMagnitude * ScaleFactor;
                camScript.TriggerShake(hitShakeDuration, finalShakeMagnitude);
            }
            else
            {
                Debug.LogWarning("ThirdPersonCamera script not found on Main Camera. Cannot shake.");
            }
        }
    }

    // Coroutine to handle loss of control during hit feedback
    IEnumerator HitStunRoutine()
    {
        isHit = true;
        // End glide if being hit mid-air
        if (isGliding) EndGliding();
        
        yield return new WaitForSeconds(hitStunDuration);
        isHit = false;
    }

    public void SuppressJumpForSeconds(float seconds)
    {
        float until = Time.time + Mathf.Max(0f, seconds);
        if (until > jumpSuppressedUntil)
            jumpSuppressedUntil = until;
    }

    // Input System Callbacks
    public void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started) jumpHeld = true;
        if (context.canceled) jumpHeld = false;

        if (!context.started) return;
        if (isHit) return;
        if (Time.time < jumpSuppressedUntil) return;
        if (PaperUIManager.Instance != null && PaperUIManager.Instance.IsPaperShowing()) return;
        if (DiaryUIManager.Instance != null && DiaryUIManager.Instance.IsDiaryShowing()) return;
        if (isRolling) return;
        if (Time.time < lastJumpTime + jumpCooldown) return;

        bool groundedNow = CheckGroundedStrict();
        bool canCoyoteJump = Time.time <= lastGroundedTime + coyoteTime;

        if (groundedNow)
        {
            if (TryGetGroundHit(out RaycastHit hit))
            {
                BouncyObject bouncy = hit.collider.GetComponent<BouncyObject>();
                if (bouncy == null)
                    bouncy = hit.collider.GetComponentInParent<BouncyObject>();

                if (bouncy != null && bouncy.TryBounce(rb))
                {
                    lastJumpTime = Time.time;
                    lastGroundedTime = -999f;
                    return;
                }
            }

            PerformJump();
            lastJumpTime = Time.time;
            lastGroundedTime = -999f;
        }
        else if (canCoyoteJump)
        {
            PerformJump();
            lastJumpTime = Time.time;
            lastGroundedTime = -999f;
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed || isHit) return; // Prevent dashing during knockback
        if (!isRolling && Time.time >= lastDashTime + dashCooldown)
            StartCoroutine(PerformDash());
    }

    public void OnGlide(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            glideHeld = true;
            if (isHit) return;
            if (isGliding || isRolling || isMovingToLaunch) return;

            bool inZone = isInGlideLaunchZone && currentGlideSpot != null;
            bool inAir = !CheckGroundedStrict();

            if (!inZone && !inAir) return;

            if (inZone && currentGlideSpot.GetLaunchPoint() != null)
            {
                StartCoroutine(MoveToLaunchThenGlide());
                return;
            }

            StartGliding();
        }
        else if (context.canceled)
        {
            glideHeld = false;
            if (isGliding)
                EndGliding();
        }
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

    float ComputeYawFromWorldDir(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return lastTargetYaw;

        dir.Normalize();
        float baseYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        return baseYaw - 90f;
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

    void MoveCharacter()
    {
        const float deadzone = 0.15f;
        if (moveInput.sqrMagnitude < deadzone * deadzone)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            if (_animator)
                _animator.SetBool("isRunning", false);
            return;
        }

        Vector3 worldDir = GetCameraRelativeWorldDir(moveInput);
        if (worldDir.sqrMagnitude < 0.0001f) return;

        worldDir.Normalize();
        float targetYaw = ComputeYawFromWorldDir(worldDir);

        float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref turnSmoothVelocity, turnSmoothTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        lastTargetYaw = yaw;

        Vector3 vel = worldDir * (walkSpeed * ScaleFactor);
        vel.y = rb.linearVelocity.y;
        rb.linearVelocity = vel;

        if (_animator)
        {
            bool shouldRun =
                isGrounded &&
                !isRolling &&
                !isGliding &&
                !isHit;

            _animator.SetBool("isRunning", shouldRun);
        }
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
        bunnyJumpSfx.Post(gameObject);

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * (jumpForce * ScaleFactor * rb.mass), ForceMode.Impulse);

        isGrounded = false;
        if (_animator)
        {
            _animator.SetBool("isGrounded", false);
            _animator.SetBool("isRunning", false);
        }
    }

    void StartGliding()
    {
        isGliding = true;
        glideStartTime = Time.time;
        rb.useGravity = false;
        scaleAtGlideStart = CurrentScale;

        Vector3 launchDir = Vector3.zero;
        if (currentGlideSpot != null)
        {
            Vector3 spotDir = currentGlideSpot.GetLaunchDirection();
            if (spotDir.sqrMagnitude > 0.01f)
                launchDir = spotDir;
        }

        // If no launch spot direction, glide forward in the direction the body is actually facing
        if (launchDir.sqrMagnitude < 0.01f)
            launchDir = transform.right;

        launchDir.y = 0f;
        if (launchDir.sqrMagnitude < 0.01f)
            launchDir = transform.right;

        launchDir.Normalize();

        Vector3 faceDir = GetCameraRelativeWorldDir(moveInput);

        // If there is no input, keep facing the body's actual forward direction
        if (faceDir.sqrMagnitude < 0.01f)
        {
            faceDir = transform.right;
            faceDir.y = 0f;
        }

        if (faceDir.sqrMagnitude > 0.01f)
        {
            faceDir.Normalize();
            float yaw = ComputeYawFromWorldDir(faceDir);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            lastTargetYaw = yaw;
        }

        float launchMagnitude = glideLaunchSpeed * ScaleFactor;
        rb.linearVelocity = launchDir * launchMagnitude;

        if (_animator)
        {
            _animator.SetBool("isRunning", false);
            _animator.SetBool("isGrounded", false);
        }
        SetGlidingAnimator(true);

        bunnyGlideStart.Post(gameObject);
    }

    void EndGliding()
    {
        bunnyGlideStop.Post(gameObject);

        isGliding = false;
        rb.useGravity = true;
        SetGlidingAnimator(false);
        UpdateGroundedState();
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

        Vector3 launchDir = (currentGlideSpot != null) ? currentGlideSpot.GetLaunchDirection() : transform.right;
        if (launchDir.sqrMagnitude < 0.01f) launchDir = transform.right;
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
        if (glideHeld)
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

            float targetYaw = ComputeYawFromWorldDir(worldDir);
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            lastTargetYaw = yaw;
        }

        velocity.y = -glideSinkSpeed;
        rb.linearVelocity = velocity;

        float drain = glideMassDrainPerSecond * Time.fixedDeltaTime;
        Vector3 scale = transform.localScale;
        float minScale = scaleAtGlideStart * minGlideScaleRatio;

        scale.x = Mathf.Max(scale.x - drain, minScale);
        scale.y = Mathf.Max(scale.y - drain, minScale);
        scale.z = Mathf.Max(scale.z - drain, minScale);
        transform.localScale = scale;
    }

    IEnumerator PerformDash()
    {
        isRolling = true;
        if (_animator)
        {
            _animator.SetBool("isRolling", true);
        }
        lastDashTime = Time.time;

        rb.linearDamping = rollDrag;
        rb.useGravity = false;

        Vector3 dashDir = GetCameraRelativeWorldDir(moveInput);

        // If no movement input, dash in the direction the body is actually facing
        if (dashDir.sqrMagnitude < 0.01f)
        {
            dashDir = transform.right; // using right because of model orientation
        }

        dashDir.y = 0f;
        dashDir.Normalize();

        float yaw = ComputeYawFromWorldDir(dashDir);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        lastTargetYaw = yaw;

        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        rb.AddForce(dashDir * (dashForce * ScaleFactor), ForceMode.VelocityChange);

        yield return new WaitForSeconds(dashDuration);

        isRolling = false;
        if (_animator) _animator.SetBool("isRolling", false);
        rb.linearDamping = defaultDrag;
        rb.useGravity = true;
    }

    bool TryGetGroundHit(out RaycastHit hit)
    {
        hit = default;

        if (playerCollider == null) return false;

        Bounds bounds = playerCollider.bounds;

        Vector3 origin = new Vector3(
            bounds.center.x,
            bounds.min.y + 0.2f,
            bounds.center.z
        );

        float rayDistance = 0.35f;

        return Physics.Raycast(
            origin,
            Vector3.down,
            out hit,
            rayDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    bool CheckGroundedStrict()
    {
        if (playerCollider == null) return false;

        Bounds bounds = playerCollider.bounds;

        Vector3 origin = new Vector3(
            bounds.center.x,
            bounds.min.y + 0.02f,
            bounds.center.z
        );

        return Physics.SphereCast(
            origin,
            strictGroundCheckRadius,
            Vector3.down,
            out _,
            strictGroundCheckDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    void UpdateGroundedState()
    {
        isGrounded = CheckGroundedStrict();

        if (isGrounded)
            lastGroundedTime = Time.time;

        if (_animator)
            _animator.SetBool("isGrounded", isGrounded);
    }
}