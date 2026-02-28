using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DustBunnyController : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 3.5f;
    public float jumpForce = 16f;
    public float turnSmoothTime = 0.1f;

    [Header("--- Jump Feel (Gravity) ---")]
    [Tooltip("Multiplier for gravity when falling. Higher = faster fall.")]
    public float fallMultiplier = 2.5f;
    [Tooltip("Gravity multiplier while holding jump (lower = higher jump)")]
    public float heldJumpGravityMultiplier = 0.25f;
    [Tooltip("Multiplier for gravity when jump is released early.")]
    public float lowJumpMultiplier = 2.5f;

    [Header("--- Dash / Roll Settings ---")]
    public float dashForce = 2.5f; 
    public float dashDuration = 0.5f; 
    public float dashCooldown = 1.0f;
    public float rollDrag = 0.5f; 

    [Header("--- Impact Feel ---")]
    public float dashFovKick = 10f; // How much the camera zooms out on dash
    public float fovSmoothTime = 0.8f; // How fast camera returns to normal

    [Header("--- Glide Settings ---")]
    [Tooltip("Horizontal speed while gliding (same control as walking: left stick / WASD).")]
    public float glideHorizontalSpeed = 2f;
    [Tooltip("Downward speed (sink rate) while gliding. Lower = floatier.")]
    public float glideSinkSpeed = 2f;
    [Tooltip("Upward boost when pressing F to glide (jump into the glide). Prevents getting stuck on the platform.")]
    public float glideJumpForce = 6f;
    [Tooltip("Initial forward speed when launching off the ledge.")]
    public float glideLaunchSpeed = 5f;
    [Tooltip("Mass (scale) lost per second while gliding. Flying consumes mass.")]
    public float glideMassDrainPerSecond = 0.02f;
    [Tooltip("Gliding stops when scale drops below this (relative to starting scale).")]
    public float minGlideScaleRatio = 0.1f;
    [Tooltip("When pressing F in a launch zone with a Launch Point, time to auto-move to that position before gliding.")]
    public float glideMoveToLaunchDuration = 0.35f;

    [Header("--- Debug & Status ---")]
    [Tooltip("Tolerance for normal ground check (walk, jump).")]
    public float groundCheckOffset = 0.1f;
    [Tooltip("Tighter tolerance for ending glide — only land when really touching ground so bunny doesn't hop early.")]
    public float glideLandTolerance = 0.02f;
    public bool isRolling = false;
    public bool isGrounded;
    public bool isGliding;

    private Rigidbody rb;
    private Collider playerCollider;
    private Transform camTransform;
    private Camera mainCam; // Reference to Camera component for FOV effects
    private float defaultFov; // Store original FOV
    private float turnSmoothVelocity;
    private float lastDashTime = -10f;
    private float defaultDrag;
    private float distToGround;
    private float baseScale; // Scale at Start — speed scales with size relative to this
    private float scaleAtGlideStart;   // Scale when we started gliding (for min check)
    private float glideStartTime;      // When we started gliding (grace period so we don't end immediately on ground)

    [SerializeField] private Animator _animator;

    private Vector2 moveInput;         
    private bool jumpHeld;
    private bool isInGlideLaunchZone;
    private GlideLaunchSpot currentGlideSpot;
    private bool isMovingToLaunch;

    /// <summary> Average of localScale x,y,z (for minimum mass checks). </summary>
    public float CurrentScale => (transform.localScale.x + transform.localScale.y + transform.localScale.z) / 3f;              

    // --- Scaling Logic ---

    /// <summary> Speed scales sub-linearly with size (sqrt) so getting bigger doesn't make you feel much faster. </summary>
    private float ScaleFactor
    {
        get
        {
            float scaleRatio = (transform.localScale.x + transform.localScale.y + transform.localScale.z) / (3f * baseScale);
            scaleRatio = Mathf.Max(0.5f, scaleRatio);
            return Mathf.Pow(scaleRatio, 0.5f); // sqrt: speed grows slower than size
        }
    }

    /// <summary> 
    /// Gravity must scale linearly with size to maintain visual "snappiness".
    /// If you are 10x bigger, you need much more gravity to not look like you're floating.
    /// </summary>
    private float GravityFactor
    {
        get
        {
            float scaleRatio = (transform.localScale.x + transform.localScale.y + transform.localScale.z) / (3f * baseScale);
            return Mathf.Max(1f, scaleRatio); 
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        baseScale = (transform.localScale.x + transform.localScale.y + transform.localScale.z) / 3f;
        
        if (Camera.main != null)
        {
            camTransform = Camera.main.transform;
            mainCam = Camera.main;
            defaultFov = mainCam.fieldOfView;
        }

        distToGround = playerCollider.bounds.extents.y;

        // Unity 6: linearDamping replaces drag
        rb.linearDamping = 5f;
        defaultDrag = rb.linearDamping;

        // Ensure rotation is locked so the bunny stays upright
        rb.freezeRotation = true;
    }

    void Update()
    {
        // Ground Check
        distToGround = playerCollider.bounds.extents.y;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, distToGround + groundCheckOffset);

        if (!isGrounded)
        {
            AkUnitySoundEngine.SetRTPCValue("grounded", 1, gameObject);
        }
        else
        {
            AkUnitySoundEngine.SetRTPCValue("grounded", 0, gameObject);
        }

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
                float currentScaleAvg = (transform.localScale.x + transform.localScale.y + transform.localScale.z) / 3f;
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
        // Only allow movement control if NOT rolling
        if (!isRolling)
        {
            MoveCharacter();
            ApplyBetterGravity();
        }
    }

    // Input System Callbacks

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started) jumpHeld = true;
        if (context.canceled) jumpHeld = false;

        if (context.performed && isGrounded && !isRolling)
            PerformJump();
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (!isRolling && Time.time >= lastDashTime + dashCooldown)
        {
            StartCoroutine(PerformDash());
        }
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

    /// <summary> True when standing in a GlideLaunchSpot (can press Glide to launch). </summary>
    public bool CanGlideFromSpot => isInGlideLaunchZone && currentGlideSpot != null && !isGliding && !isRolling;

    /// <summary> Prompt text from the current glide spot (e.g. "Press G (or R1) to glide"). </summary>
    public string GlidePromptText => currentGlideSpot != null ? currentGlideSpot.GetPromptText() : "";

    // --- Core Movement Logic ---

    void MoveCharacter()
    {
        if (camTransform == null) return;

        float h = moveInput.x;
        float v = moveInput.y;

        Vector3 direction = new Vector3(h, 0f, v).normalized;

        if (direction.magnitude >= 0.1f)
        {
            // Calculate target angle based on camera
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + camTransform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            // Apply movement velocity (scale with size so bigger bunny isn't slower)
            Vector3 targetVelocity = moveDir * (walkSpeed * ScaleFactor);
            targetVelocity.y = rb.linearVelocity.y; // Preserve gravity

            rb.linearVelocity = targetVelocity;
        }

        // Animation
        if (_animator)
        {
            bool running = (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f);
            _animator.SetBool("isRunning", running);
        }
    }

    void ApplyBetterGravity()
    {
        float currentMultiplier = 1f; // Default gravity state

        // Determine which gravity multiplier state we are in
        if (rb.linearVelocity.y < 0)
        {
            currentMultiplier = fallMultiplier; // Falling heavily
        }
        else if (rb.linearVelocity.y > 0 && !jumpHeld)
        {
            currentMultiplier = lowJumpMultiplier; // Rising but let go of jump (short hop)
        }
        else if (rb.linearVelocity.y > 0 && jumpHeld)
        {
            currentMultiplier = heldJumpGravityMultiplier; // Rising and holding jump
        }

        // Calculate the absolute total multiplier we want, factoring in the bunny's massive size
        float totalMultiplier = GravityFactor * currentMultiplier;

        // Because Unity's Rigidbody already automatically applies 1x Physics.gravity.y every frame,
        // we only need to add the difference. 
        float extraGravityMultiplier = totalMultiplier - 1f;

        // Apply the downward force safely.
        rb.linearVelocity += Vector3.up * Physics.gravity.y * extraGravityMultiplier * Time.fixedDeltaTime;
    }

    void PerformJump()
    {
        // Reset vertical velocity for consistent jump height (scale with size so jump height feels consistent)
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * (jumpForce * ScaleFactor), ForceMode.Impulse);
    }

    // --- Gliding ---

    void StartGliding()
    {
        isGliding = true;
        glideStartTime = Time.time;
        rb.useGravity = false;
        scaleAtGlideStart = (transform.localScale.x + transform.localScale.y + transform.localScale.z) / 3f;

        Vector3 launchDir;
        if (currentGlideSpot != null)
        {
            Vector3 spotDir = currentGlideSpot.GetLaunchDirection();
            if (spotDir.sqrMagnitude > 0.01f)
                launchDir = spotDir.normalized;
            else if (camTransform != null)
            {
                launchDir = camTransform.forward;
                launchDir.y = 0;
                launchDir.Normalize();
            }
            else
                launchDir = transform.forward;
        }
        else if (camTransform != null)
        {
            launchDir = camTransform.forward;
            launchDir.y = 0;
            launchDir.Normalize();
        }
        else
            launchDir = transform.forward;

        // Face the launch direction — use horizontal (XZ) only so bunny stays upright and faces target
        if (launchDir.sqrMagnitude > 0.01f)
        {
            Vector3 flat = new Vector3(launchDir.x, 0f, launchDir.z);
            if (flat.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(flat.normalized);
            else
                transform.rotation = Quaternion.LookRotation(launchDir, Vector3.back);
        }

        // Launch velocity exactly along launch direction (no extra world-up boost so direction matches)
        float launchMagnitude = glideLaunchSpeed * ScaleFactor;
        if (launchDir.y > 0.01f)
            launchMagnitude += glideJumpForce * ScaleFactor * launchDir.y; // extra oomph when launching upward
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
            if (p.name == "isGliding" && p.type == AnimatorControllerParameterType.Bool)
            {
                _animator.SetBool("isGliding", value);
                return;
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
        // Face launch direction during the move (not launch point rotation) so we never show "blue"
        Vector3 launchDir = currentGlideSpot != null ? currentGlideSpot.GetLaunchDirection() : Vector3.forward;
        if (launchDir.sqrMagnitude < 0.01f) launchDir = Vector3.forward;
        launchDir = launchDir.normalized;
        Vector3 flat = new Vector3(launchDir.x, 0f, launchDir.z);
        Quaternion endRot = flat.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(flat.normalized)
            : Quaternion.LookRotation(launchDir, Vector3.back);
        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, glideMoveToLaunchDuration);
        while (elapsed < dur)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            t = t * t * (3f - 2f * t); // smoothstep
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

        // Horizontal movement: camera-relative drift (WASD). Facing is locked — no rotation from input.
        float h = moveInput.x;
        float v = moveInput.y;
        Vector3 direction = new Vector3(h, 0f, v).normalized;

        Vector3 velocity = rb.linearVelocity;
        if (direction.magnitude >= 0.1f && camTransform != null)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + camTransform.eulerAngles.y;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            velocity.x = moveDir.x * (glideHorizontalSpeed * ScaleFactor);
            velocity.z = moveDir.z * (glideHorizontalSpeed * ScaleFactor);
        }
        velocity.y = -glideSinkSpeed;
        rb.linearVelocity = velocity;

        // Drain mass over time (flying consumes mass)
        float drain = glideMassDrainPerSecond * Time.fixedDeltaTime;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Max(scale.x - drain, scaleAtGlideStart * minGlideScaleRatio);
        scale.y = Mathf.Max(scale.y - drain, scaleAtGlideStart * minGlideScaleRatio);
        scale.z = Mathf.Max(scale.z - drain, scaleAtGlideStart * minGlideScaleRatio);
        transform.localScale = scale;
    }

    // --- The Improved Dash Coroutine ---
    IEnumerator PerformDash()
    {
        if (camTransform == null) yield break;

        isRolling = true;
        if (_animator) _animator.SetBool("isRolling", true);
        lastDashTime = Time.time;

        // 1. Physics Setup for Impact
        rb.linearDamping = rollDrag; 
        rb.useGravity = false; // Disable gravity to dash straight (like a bullet)

        // Calculate Dash Direction
        float h = moveInput.x;
        float v = moveInput.y;
        Vector3 dashDir;

        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
        {
            Vector3 camForward = camTransform.forward;
            Vector3 camRight = camTransform.right;
            camForward.y = 0;
            camRight.y = 0;
            dashDir = (camForward.normalized * v + camRight.normalized * h).normalized;
        }
        else
        {
            dashDir = camTransform.forward;
            dashDir.y = 0;
            dashDir.Normalize();
        }

        // 2. Face Direction Instantly
        if (dashDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dashDir);
        }

        // 3. APPLY IMPACT
        // Reset velocity first so we don't fight existing momentum
        rb.linearVelocity = Vector3.zero; 
        
        // Use VelocityChange instead of Impulse for instant, mass-independent speed (scale with size)
        rb.AddForce(dashDir * (dashForce * ScaleFactor), ForceMode.VelocityChange);

        // 4. Camera Juice (FOV Kick)
        if (mainCam != null)
        {
            StartCoroutine(FovKick());
        }

        yield return new WaitForSeconds(dashDuration);

        // 5. Reset State
        isRolling = false;
        if (_animator) _animator.SetBool("isRolling", false);
        
        rb.linearDamping = defaultDrag;
        rb.useGravity = true; // Re-enable gravity
    }

    // Helper coroutine to create a visual "Zoom" effect during dash
    IEnumerator FovKick()
    {
        float targetFov = defaultFov + dashFovKick;
        float elapsed = 0f;

        // Zoom Out
        while(elapsed < 0.1f)
        {
            if(!mainCam) yield break;
            mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFov, elapsed / 0.1f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Return to Normal
        elapsed = 0f;
        while (elapsed < fovSmoothTime)
        {
            if (!mainCam) yield break;
            mainCam.fieldOfView = Mathf.Lerp(targetFov, defaultFov, elapsed / fovSmoothTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (mainCam) mainCam.fieldOfView = defaultFov;
    }
}