using UnityEngine;

/// <summary>
/// FleeingAbsorbable.cs
/// 
/// A moving absorbable item that runs away from the player when they get close.
/// - Detects player proximity
/// - Moves away from player at a speed slower than the player
/// - Keeps minimum distance from walls/obstacles so the player can still absorb it
/// - Grants bonus size growth when absorbed
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
    
    [Header("Obstacle Avoidance")]
    [Tooltip("Minimum distance to keep from walls and other obstacles (prevents getting stuck)")]
    public float minDistanceFromObstacles = 1.5f;
    
    [Tooltip("How far ahead to check for obstacles")]
    public float obstacleCheckRadius = 2f;
    
    [Tooltip("How strongly to steer away from obstacles (higher = more avoidance)")]
    [Range(0.5f, 3f)]
    public float avoidanceStrength = 1.5f;
    
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
    private Collider myCollider;
    private DustBunnyController playerController;
    private float playerWalkSpeed;
    private bool isFleeing = false;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        myCollider = GetComponent<Collider>();
        
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
        
        // Steer away from nearby obstacles (walls, etc.) so we don't get stuck
        Vector3 avoidanceDir = GetObstacleAvoidanceDirection();
        if (avoidanceDir.sqrMagnitude > 0.01f)
        {
            directionAway = (directionAway + avoidanceDir * avoidanceStrength).normalized;
            directionAway.y = 0f;
            directionAway.Normalize();
        }
        
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
    
    /// <summary>
    /// Returns a direction that steers away from nearby obstacles (walls, static colliders).
    /// Keeps fleeing objects from getting stuck against walls so the player can absorb them.
    /// </summary>
    private Vector3 GetObstacleAvoidanceDirection()
    {
        Vector3 myPos = transform.position;
        Vector3 sumAvoidance = Vector3.zero;
        int count = 0;
        
        Collider[] hits = Physics.OverlapSphere(myPos, obstacleCheckRadius);
        foreach (Collider other in hits)
        {
            if (other == myCollider || other.attachedRigidbody == rb) continue;
            if (other.CompareTag("Player")) continue;
            
            Vector3 closestOnOther = other.ClosestPoint(myPos);
            float dist = Vector3.Distance(myPos, closestOnOther);
            
            if (dist < minDistanceFromObstacles && dist > 0.001f)
            {
                // Too close to this obstacle — push away from it (horizontal only)
                Vector3 away = (myPos - closestOnOther).normalized;
                away.y = 0f;
                away.Normalize();
                float strength = 1f - (dist / minDistanceFromObstacles); // stronger when closer
                sumAvoidance += away * strength;
                count++;
            }
        }
        
        if (count == 0) return Vector3.zero;
        return sumAvoidance.normalized;
    }
    
    void OnDrawGizmosSelected()
    {
        if (showGizmos)
        {
            // Draw flee distance sphere
            Gizmos.color = isFleeing ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, fleeDistance);
            
            // Draw obstacle avoidance radius (keep-out zone)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, minDistanceFromObstacles);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, obstacleCheckRadius);
            
            // Draw direction to player
            if (Application.isPlaying && playerTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, playerTransform.position);
            }
        }
    }
}
