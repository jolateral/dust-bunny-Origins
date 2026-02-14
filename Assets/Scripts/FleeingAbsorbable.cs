using UnityEngine;

/// <summary>
/// FleeingAbsorbable.cs
/// 
/// A moving absorbable item that runs away from the player when they get close.
/// - Detects player proximity
/// - Moves away from player at a speed slower than the player
/// - Grants bonus size growth when absorbed
/// - Uses physics-based rolling movement for sphere-shaped objects
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class FleeingAbsorbable : MonoBehaviour
{
    [Header("Flee Settings")]
    [Tooltip("Distance at which the item starts fleeing from the player")]
    public float fleeDistance = 5f;
    
    [Tooltip("Speed multiplier relative to player's walk speed (0.5 = half player speed)")]
    [Range(0.1f, 0.9f)]
    public float speedMultiplier = 0.8f;
    
    [Header("Bonus Growth")]
    [Tooltip("Multiplier for size growth when this item is absorbed (2.0 = double growth)")]
    public float growthMultiplier = 2.0f;
    
    [Header("Movement Smoothing")]
    [Tooltip("How quickly the item changes direction when fleeing")]
    public float turnSpeed = 5f;
    
    [Header("Debug")]
    public bool showGizmos = true;
    
    private Transform playerTransform;
    private Rigidbody rb;
    private DustBunnyController playerController;
    private float playerWalkSpeed;
    private bool isFleeing = false;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerController = player.GetComponent<DustBunnyController>();
            if (playerController != null)
            {
                playerWalkSpeed = playerController.walkSpeed;
            }
            else
            {
                Debug.LogWarning("FleeingAbsorbable: Player doesn't have DustBunnyController component!");
                playerWalkSpeed = 8f; // Default fallback
            }
        }
        else
        {
            Debug.LogWarning("FleeingAbsorbable: No player found with 'Player' tag!");
        }
        
        // Ensure this object has the StickyObject tag (required for absorption)
        if (!CompareTag("StickyObject"))
        {
            // Try to set the tag automatically
            try
            {
                gameObject.tag = "StickyObject";
                Debug.Log($"FleeingAbsorbable '{name}': Automatically set tag to 'StickyObject'");
            }
            catch
            {
                Debug.LogWarning($"FleeingAbsorbable '{name}': Could not set tag to 'StickyObject'. Please set it manually in the Inspector!");
            }
        }
        
        // Configure Rigidbody for rolling
        rb.freezeRotation = false; // Allow rotation for rolling
        rb.linearDamping = 2f; // Add some damping for smoother movement
        rb.angularDamping = 0.5f;
        
        // Ensure the collider is not a trigger (triggers don't generate OnCollisionEnter events)
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            if (col.isTrigger)
            {
                col.isTrigger = false;
                Debug.LogWarning($"FleeingAbsorbable '{name}': Changed collider from trigger to solid for collision detection!");
            }
        }
    }
    
    void OnDisable()
    {
        // Stop movement when component is disabled (e.g., when absorbed)
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    
    void FixedUpdate()
    {
        if (playerTransform == null || rb == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        
        if (distanceToPlayer <= fleeDistance)
        {
            isFleeing = true;
            FleeFromPlayer();
        }
        else
        {
            isFleeing = false;
            // Gradually slow down when not fleeing
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 2f);
        }
    }
    
    void FleeFromPlayer()
    {
        // Calculate direction away from player
        Vector3 directionAway = (transform.position - playerTransform.position).normalized;
        
        // Remove vertical component to keep movement on ground plane
        directionAway.y = 0f;
        directionAway.Normalize();
        
        // Calculate target velocity (slower than player)
        float fleeSpeed = playerWalkSpeed * speedMultiplier;
        Vector3 targetVelocity = directionAway * fleeSpeed;
        
        // Preserve vertical velocity (for gravity/falling)
        targetVelocity.y = rb.linearVelocity.y;
        
        // Smoothly apply velocity
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * turnSpeed);
        
        // Rotate the sphere to face the direction it's moving (for visual rolling effect)
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            Vector3 moveDirection = rb.linearVelocity;
            moveDirection.y = 0f;
            if (moveDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * turnSpeed);
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (showGizmos)
        {
            // Draw flee distance sphere
            Gizmos.color = isFleeing ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, fleeDistance);
            
            // Draw direction to player
            if (Application.isPlaying && playerTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, playerTransform.position);
            }
        }
    }
}
