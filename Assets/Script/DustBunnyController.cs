using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DustBunnyController : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 8f;
    public float jumpForce = 15f; 
    public float turnSmoothTime = 0.1f;

    [Header("--- Jump Feel (Gravity) ---")]
    [Tooltip("Multiplier for gravity when falling. Higher = faster fall.")]
    public float fallMultiplier = 2.5f;
    [Tooltip("Multiplier for gravity when space is released early.")]
    public float lowJumpMultiplier = 2f;

    [Header("--- Dash / Roll Settings ---")]
    public float dashForce = 30f; // Adjusted default, 3f might be too weak for an impulse
    public float dashDuration = 0.8f;
    public float dashCooldown = 1.0f;
    public float rollDrag = 0.5f;

    [Header("--- Debug & Status ---")]
    public float groundCheckOffset = 0.2f;
    public bool isRolling = false; 
    public bool isGrounded;

    private Rigidbody rb;
    private Collider playerCollider;
    private Transform camTransform;
    private float turnSmoothVelocity;
    private float lastDashTime = -10f;
    private float defaultDrag;
    private float distToGround;

    [SerializeField] private Animator _animator;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        camTransform = Camera.main.transform;

        distToGround = playerCollider.bounds.extents.y;

        // Unity 6: use linearDamping
        rb.linearDamping = 5f;
        defaultDrag = rb.linearDamping;

        // Ensure rotation is locked so the bunny stays upright
        rb.freezeRotation = true;
    }

    void Update()
    {
        // 1. Ground Check
        distToGround = playerCollider.bounds.extents.y;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, distToGround + groundCheckOffset);
        // Debug.DrawRay(transform.position, Vector3.down * (distToGround + groundCheckOffset), isGrounded ? Color.green : Color.red);

        // 2. Handle Inputs
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1)) && isGrounded && !isRolling)
        {
            PerformJump();
        }

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
        // If we are rolling (dashing), we let physics handle the slide.
        // If we are NOT rolling, we control movement manually.
        if (!isRolling)
        {
            MoveCharacter();
            ApplyBetterGravity(); 
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

            Vector3 targetVelocity = moveDir * walkSpeed;
            targetVelocity.y = rb.linearVelocity.y; 

            rb.linearVelocity = targetVelocity;
        }

        // Animation handling
        if (h != 0 || v != 0)
        { 
            if(_animator) _animator.SetBool("isRunning", true);
        }
        else
        {
            if(_animator) _animator.SetBool("isRunning", false);
        }
    }

    void ApplyBetterGravity()
    {
        // Falling
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        // Rising but jump button released early
        else if (rb.linearVelocity.y > 0 && !Input.GetKey(KeyCode.Space) && !Input.GetKey(KeyCode.JoystickButton1))
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    void PerformJump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    IEnumerator PerformDash()
    {
        isRolling = true;
        if(_animator) _animator.SetBool("isRolling", true);
        lastDashTime = Time.time;

        // --- Change: No longer unlocking rotation ---
        // rb.freezeRotation = false; // REMOVED: We want the bunny upright
        rb.linearDamping = rollDrag;

        // Calculate Direction
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

        // --- New: Immediately face the dash direction ---
        // This prevents sliding sideways while looking forward
        if (dashDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dashDir);
        }

        // Apply Force
        rb.AddForce(dashDir * dashForce, ForceMode.Impulse);
        
        // --- Change: No longer adding Torque (spinning) ---
        // rb.AddTorque(...); // REMOVED

        yield return new WaitForSeconds(dashDuration);

        // Reset
        isRolling = false;
        if(_animator) _animator.SetBool("isRolling", false);

        // Restore drag
        rb.linearDamping = defaultDrag;
        
        // Rotation is already locked, so we don't need to manually reset logic here
    }
}