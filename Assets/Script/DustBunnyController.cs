using UnityEngine;
using System.Collections;

// Ensures the object has the necessary components
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DustBunnyController : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    [Tooltip("How fast the bunny moves when walking.")]
    public float walkSpeed = 8f;
    
    [Tooltip("The immediate upward force applied when jumping.")]
    public float jumpForce = 15f;
    
    [Tooltip("How quickly the character turns to face movement direction.")]
    public float turnSmoothTime = 0.1f;

    [Header("--- Dash / Roll Settings ---")]
    [Tooltip("The explosive force applied when pressing Shift.")]
    public float dashForce = 30f;
    
    [Tooltip("How long the rolling state lasts (in seconds).")]
    public float dashDuration = 0.8f;
    
    [Tooltip("Time to wait before dashing again.")]
    public float dashCooldown = 1.0f;
    
    [Tooltip("Friction when rolling (lower = slide further).")]
    public float rollDrag = 0.5f;

    [Header("--- Debug & Status ---")]
    [Tooltip("Adjust this if the ground check is too sensitive or not sensitive enough.")]
    public float groundCheckOffset = 0.2f; 

    [Header("--- Advanced Gravity ---")]
    public float fallMultiplier = 2.5f; // Multiplier for when falling
    public float lowJumpMultiplier = 2f; // Multiplier for short hops
    
    public bool isRolling = false; // Accessible by other scripts (like Absorption)
    public bool isGrounded;        // Read-only status for debugging

    // Private variables
    private Rigidbody rb;
    private Collider playerCollider;
    private Transform camTransform;
    private float turnSmoothVelocity;
    private float lastDashTime = -10f; // Initialize to allow immediate dash
    private float defaultDrag;
    private float distToGround;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        camTransform = Camera.main.transform;

        // Calculate distance from center to the bottom of the collider
        distToGround = playerCollider.bounds.extents.y;

        // Set default drag for stable walking
        rb.linearDamping = 5f;
        defaultDrag = rb.linearDamping;

        // Lock rotation so the bunny stands upright (until we roll)
        rb.freezeRotation = true;
    }

    void Update()
    {
        // --- 1. Ground Check Logic ---
        // We recalculate bounds in case the player grew in size
        distToGround = playerCollider.bounds.extents.y;
        
        // Raycast from center downwards. Length = half height + small offset
        isGrounded = Physics.Raycast(transform.position, Vector3.down, distToGround + groundCheckOffset);

        // DEBUG: Draw a red line in the Scene view to show the ground check
        Debug.DrawRay(transform.position, Vector3.down * (distToGround + groundCheckOffset), isGrounded ? Color.green : Color.red);

        // --- 2. Handle Inputs ---
        
        // JUMP: Only if grounded and NOT rolling
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isRolling)
        {
            PerformJump();
        }

        // DASH: Check input and cooldown
        if (Input.GetKeyDown(KeyCode.LeftShift) && !isRolling)
        {
            if (Time.time >= lastDashTime + dashCooldown)
            {
                StartCoroutine(PerformDash());
            }
        }
    }

    void FixedUpdate()
    {
        if (!isRolling)
        {
            MoveCharacter();
        }

        // --- NEW: Gravity Multiplier Logic ---
        if (rb.linearVelocity.y < 0) 
        {
            // Falling: Apply extra gravity
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetKey(KeyCode.Space))
        {
            // Rising but NOT holding space: Fall faster (Variable Jump Height)
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    void MoveCharacter()
    {
        // Get Input (WASD or Arrow Keys)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        
        Vector3 direction = new Vector3(h, 0f, v).normalized;

        // If player is pressing keys
        if (direction.magnitude >= 0.1f)
        {
            // Calculate the angle the player should face based on Camera direction
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + camTransform.eulerAngles.y;
            
            // Smoothly rotate the character to that angle
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            // Convert rotation into a forward direction vector
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            
            // Apply velocity for movement
            // We preserve rb.velocity.y so gravity/jumping isn't cancelled out
            Vector3 targetVelocity = moveDir * walkSpeed;
            targetVelocity.y = rb.linearVelocity.y; 
            
            rb.linearVelocity = targetVelocity;
        }
    }

    void PerformJump()
    {
        // Reset vertical velocity to 0 before jumping to ensure consistent height
        // (This prevents "super jumps" if you hit space while bouncing up)
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        
        // Apply immediate upward force
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    // Coroutine to handle the dash sequence over time
    IEnumerator PerformDash()
    {
        isRolling = true;
        lastDashTime = Time.time; // Reset cooldown

        // --- STEP 1: Unlock Physics ---
        rb.freezeRotation = false; // Allow the ball to tumble
        rb.linearDamping = rollDrag;        // Reduce friction to slide
        
        // --- STEP 2: Calculate Dash Direction ---
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dashDir = Vector3.zero;

        // If giving input, dash in that direction
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
            // If standing still, dash forward relative to camera
            dashDir = camTransform.forward;
            dashDir.y = 0; 
            dashDir.Normalize();
        }

        // --- STEP 3: Apply Explosion Force ---
        rb.AddForce(dashDir * dashForce, ForceMode.Impulse);
        
        // Add rotational spin for visual flair
        rb.AddTorque(transform.right * dashForce, ForceMode.Impulse);

        // --- STEP 4: Wait ---
        yield return new WaitForSeconds(dashDuration);

        // --- STEP 5: Reset State ---
        isRolling = false;
        rb.freezeRotation = true;   // Lock upright again
        rb.linearDamping = defaultDrag;      // Restore walking friction
        
        // Reset rotation to be perfectly upright so we don't walk sideways
        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
    }
}