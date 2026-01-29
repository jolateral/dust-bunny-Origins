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
    public float jumpForce = 15f; // Might need to increase to 20-25 due to gravity multiplier

    [Tooltip("How quickly the character turns to face movement direction.")]
    public float turnSmoothTime = 0.1f;

    [Header("--- Jump Feel (Gravity) ---")]
    [Tooltip("Multiplier for gravity when falling. Higher = faster fall (snappier).")]
    public float fallMultiplier = 2.5f;

    [Tooltip("Multiplier for gravity when going up but Space is released (Variable Jump Height).")]
    public float lowJumpMultiplier = 2f;

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

    public bool isRolling = false; // Accessible by other scripts
    public bool isGrounded;

    // Private variables
    private Rigidbody rb;
    private Collider playerCollider;
    private Transform camTransform;
    private float turnSmoothVelocity;
    private float lastDashTime = -10f;
    private float defaultDrag;
    private float distToGround;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        camTransform = Camera.main.transform;

        distToGround = playerCollider.bounds.extents.y;

        // Unity 6: use linearDamping instead of drag
        rb.linearDamping = 5f;
        defaultDrag = rb.linearDamping;

        rb.freezeRotation = true;
    }

    void Update()
    {
        // --- 1. Ground Check Logic ---
        distToGround = playerCollider.bounds.extents.y;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, distToGround + groundCheckOffset);
        Debug.DrawRay(transform.position, Vector3.down * (distToGround + groundCheckOffset), isGrounded ? Color.green : Color.red);

        // --- 2. Handle Inputs ---

        // JUMP: Input.GetKeyDown(KeyCode.Space) OR Joystick Button 1 (Usually 'A' (xbox) or 'X' (ps) on controller)
        // Only if grounded and NOT rolling
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1)) && isGrounded && !isRolling)
        {
            PerformJump();
        }

        // DASH: Left Shift OR Joystick Button 2 (Usually 'X' (xbox) or 'Square' (ps))
        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.JoystickButton0)) && !isRolling)
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
            ApplyBetterGravity(); // Re-added the better jump feel logic
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
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(h, 0f, v).normalized;

        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + camTransform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            // Unity 6: use linearVelocity instead of velocity
            Vector3 targetVelocity = moveDir * walkSpeed;
            targetVelocity.y = rb.linearVelocity.y; // Preserve vertical gravity

            rb.linearVelocity = targetVelocity;
        }
    }

    // --- Added back the logic for Snappy Jumping ---
    void ApplyBetterGravity()
    {
        // If falling
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        // If jumping up but button released (Variable Jump Height)
        else if (rb.linearVelocity.y > 0 && !Input.GetKey(KeyCode.Space) && !Input.GetKey(KeyCode.JoystickButton0))
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }
    }

    void PerformJump()
    {
        // Unity 6: Reset vertical linearVelocity
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    IEnumerator PerformDash()
    {
        isRolling = true;
        lastDashTime = Time.time;

        // --- STEP 1: Unlock Physics ---
        rb.freezeRotation = false;
        // Unity 6: linearDamping
        rb.linearDamping = rollDrag;

        // --- STEP 2: Calculate Dash Direction ---
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dashDir = Vector3.zero;

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

        // --- STEP 3: Apply Explosion Force ---
        rb.AddForce(dashDir * dashForce, ForceMode.Impulse);
        rb.AddTorque(transform.right * dashForce, ForceMode.Impulse);

        // --- STEP 4: Wait ---
        yield return new WaitForSeconds(dashDuration);

        // --- STEP 5: Reset State ---
        isRolling = false;
        rb.freezeRotation = true;
        // Unity 6: linearDamping
        rb.linearDamping = defaultDrag;

        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
    }
}