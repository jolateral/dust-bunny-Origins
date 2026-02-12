using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DustBunnyController : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 8f; // Increased slightly for better feel
    public float jumpForce = 15f;
    public float turnSmoothTime = 0.1f;

    [Header("--- Jump Feel (Gravity) ---")]
    [Tooltip("Multiplier for gravity when falling. Higher = faster fall.")]
    public float fallMultiplier = 2.5f;
    [Tooltip("Gravity multiplier while holding jump (lower = higher jump)")]
    public float heldJumpGravityMultiplier = 0.6f;
    [Tooltip("Multiplier for gravity when jump is released early.")]
    public float lowJumpMultiplier = 1f;

    [Header("--- Dash / Roll Settings ---")]
    public float dashForce = 40f; // Increased force for more impact
    public float dashDuration = 0.5f; // Shortened duration for a "burst" feel
    public float dashCooldown = 1.0f;
    public float rollDrag = 2f; // Increased drag so you stop firmly after the burst (less slippery)

    [Header("--- Impact Feel ---")]
    public float dashFovKick = 5f; // How much the camera zooms out on dash
    public float fovSmoothTime = 0.2f; // How fast camera returns to normal

    [Header("--- Debug & Status ---")]
    public float groundCheckOffset = 0.2f;
    public bool isRolling = false;
    public bool isGrounded;

    private Rigidbody rb;
    private Collider playerCollider;
    private Transform camTransform;
    private Camera mainCam; // Reference to Camera component for FOV effects
    private float defaultFov; // Store original FOV
    private float turnSmoothVelocity;
    private float lastDashTime = -10f;
    private float defaultDrag;
    private float distToGround;

    // Audio
    public AudioSource bunnySfxSource;
    public AudioResource bunnyMove;
    public AudioResource bunnyJump;
    public AudioResource bunnyRoll;

    [SerializeField] private Animator _animator;

    private Vector2 moveInput;         
    private bool jumpHeld;              

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        
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
    }

    void FixedUpdate()
    {
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
        {
            PerformJump();
            PlaySfx(bunnyJump);
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (!isRolling && Time.time >= lastDashTime + dashCooldown)
        {
            StartCoroutine(PerformDash());
            PlaySfx(bunnyRoll);
        }
    }

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

            // Apply movement velocity
            Vector3 targetVelocity = moveDir * walkSpeed;
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
        // Use Unity Physics.gravity.y (-9.81) for cleaner calculations
        
        // Falling (Heavy fall)
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        // Rising and NOT holding jump (Short hop)
        else if (rb.linearVelocity.y > 0 && !jumpHeld)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }
        // Rising and holding jump (High jump)
        else if (rb.linearVelocity.y > 0 && jumpHeld)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (heldJumpGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    void PerformJump()
    {
        // Reset vertical velocity for consistent jump height
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
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
        
        // Use VelocityChange instead of Impulse for instant, mass-independent speed
        rb.AddForce(dashDir * dashForce, ForceMode.VelocityChange);

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

    private void PlaySfx(AudioResource clip)
    {
        if (!bunnySfxSource || !clip) return;
        bunnySfxSource.resource = clip;
        bunnySfxSource.Play();
    }
}