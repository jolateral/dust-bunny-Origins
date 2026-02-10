using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DustBunnyController : MonoBehaviour
{
    [Header("--- Movement Settings ---")]
    public float walkSpeed = 3f;
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
    public float dashForce = 30f;
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
        camTransform = Camera.main ? Camera.main.transform : null;

        distToGround = playerCollider.bounds.extents.y;

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
        if (!isRolling)
        {
            MoveCharacter();
            ApplyBetterGravity();
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        // Track held state for low-jump logic
        if (context.started)
        {
            jumpHeld = true;
        }
        if (context.canceled)
        {
            jumpHeld = false;
        }

        // Jump on press (started)
        if (context.performed && isGrounded && !isRolling)
        {
            PerformJump();
            PlaySfx(bunnyJump);
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        if (!isRolling && Time.time >= lastDashTime + dashCooldown)
        {
            StartCoroutine(PerformDash());
            PlaySfx(bunnyRoll);
        }
    }

    // Movement

    void MoveCharacter()
    {
        if (camTransform == null)
        {
            return;
        }

        float h = moveInput.x;
        float v = moveInput.y;

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
        if (_animator)
        {
            bool running = (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f);
            _animator.SetBool("isRunning", running);
        }
    }

    void ApplyBetterGravity()
    {
        // Falling
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        // Rising and NOT holding jump short hop
        else if (rb.linearVelocity.y > 0 && !jumpHeld)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y *
                (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }

        // Rising and holding jump lighter gravity higher jump
        else if (rb.linearVelocity.y > 0 && jumpHeld)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y *
                (heldJumpGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    void PerformJump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    IEnumerator PerformDash()
    {
        if (camTransform == null)
        {
            yield break;
        }

        isRolling = true;
        if (_animator) _animator.SetBool("isRolling", true);
        lastDashTime = Time.time;

        rb.linearDamping = rollDrag;

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

        if (dashDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dashDir);
        }

        rb.AddForce(dashDir * dashForce, ForceMode.Impulse);

        yield return new WaitForSeconds(dashDuration);

        isRolling = false;
        if (_animator)
        {
            _animator.SetBool("isRolling", false);
        }
        rb.linearDamping = defaultDrag;
    }
    private void PlaySfx(AudioResource clip)
    {
        if (!bunnySfxSource || !clip)
        {
            return;
        }

        bunnySfxSource.resource = clip;
        bunnySfxSource.Play();
    }
}
