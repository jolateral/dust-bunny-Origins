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
    public float dashFovKick = 10f; 
    public float fovSmoothTime = 0.8f; 

    [Header("--- Debug & Status ---")]
    public float groundCheckOffset = 0.1f;
    public bool isRolling = false;
    public bool isGrounded;

    private Rigidbody rb;
    private Collider playerCollider;
    private Transform camTransform;
    private Camera mainCam; 
    private float defaultFov; 
    private float turnSmoothVelocity;
    private float lastDashTime = -10f;
    private float defaultDrag;
    private float distToGround;
    private float baseScale; 

    [SerializeField] private Animator _animator;

    private Vector2 moveInput;         
    private bool jumpHeld;              

    // --- Scaling Logic ---

    /// <summary> Speed scales sub-linearly with size (sqrt) so getting bigger doesn't make you feel much faster. </summary>
    private float ScaleFactor
    {
        get
        {
            float scaleRatio = (transform.localScale.x + transform.localScale.y + transform.localScale.z) / (3f * baseScale);
            scaleRatio = Mathf.Max(0.5f, scaleRatio);
            return Mathf.Pow(scaleRatio, 0.5f);
        }
    }

    /// <summary> 
    /// NEW: Gravity must scale linearly with size to maintain visual "snappiness".
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

        rb.linearDamping = 5f;
        defaultDrag = rb.linearDamping;
        rb.freezeRotation = true;
    }

    void Update()
    {
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

    // Input Callbacks
    public void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started) jumpHeld = true;
        if (context.canceled) jumpHeld = false;
        if (context.performed && isGrounded && !isRolling) PerformJump();
    }
    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.performed && !isRolling && Time.time >= lastDashTime + dashCooldown) StartCoroutine(PerformDash());
    }

    void MoveCharacter()
    {
        if (camTransform == null) return;

        float h = moveInput.x;
        float v = moveInput.y;
        Vector3 direction = new Vector3(h, 0f, v).normalized;

        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + camTransform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            Vector3 targetVelocity = moveDir * (walkSpeed * ScaleFactor);
            targetVelocity.y = rb.linearVelocity.y; 

            rb.linearVelocity = targetVelocity;
        }

        if (_animator) _animator.SetBool("isRunning", (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f));
    }

    void ApplyBetterGravity()
    {
        // Multiply gravity by GravityFactor to maintain visual speed as you grow
        float customGravity = Physics.gravity.y * GravityFactor;

        // Falling
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * customGravity * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        // Rising and NOT holding jump
        else if (rb.linearVelocity.y > 0 && !jumpHeld)
        {
            rb.linearVelocity += Vector3.up * customGravity * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }
        // Rising and holding jump
        else if (rb.linearVelocity.y > 0 && jumpHeld)
        {
            rb.linearVelocity += Vector3.up * customGravity * (heldJumpGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    void PerformJump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        // Jump force also scales with ScaleFactor to maintain feel
        rb.AddForce(Vector3.up * (jumpForce * ScaleFactor), ForceMode.Impulse);
    }

    IEnumerator PerformDash()
    {
        if (camTransform == null) yield break;
        isRolling = true;
        if (_animator) _animator.SetBool("isRolling", true);
        lastDashTime = Time.time;

        rb.linearDamping = rollDrag; 
        rb.useGravity = false; 

        float h = moveInput.x;
        float v = moveInput.y;
        Vector3 dashDir = (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f) ? 
            (Quaternion.Euler(0, camTransform.eulerAngles.y, 0) * new Vector3(h, 0, v)).normalized : 
            Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;

        if (dashDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dashDir);

        rb.linearVelocity = Vector3.zero; 
        rb.AddForce(dashDir * (dashForce * ScaleFactor), ForceMode.VelocityChange);

        if (mainCam != null) StartCoroutine(FovKick());
        yield return new WaitForSeconds(dashDuration);

        isRolling = false;
        if (_animator) _animator.SetBool("isRolling", false);
        rb.linearDamping = defaultDrag;
        rb.useGravity = true; 
    }

    IEnumerator FovKick()
    {
        float targetFov = defaultFov + dashFovKick;
        float elapsed = 0f;
        while(elapsed < 0.1f)
        {
            if(!mainCam) yield break;
            mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFov, elapsed / 0.1f);
            elapsed += Time.deltaTime;
            yield return null;
        }
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