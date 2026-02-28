using UnityEngine;
using System.Collections;

public class GiantHumanWalker : MonoBehaviour
{
    [Header("--- Path Settings ---")]
    [Tooltip("Drag empty GameObjects here to form a path (e.g., Door -> Bed -> Desk -> Door).")]
    public Transform[] waypoints;
    public float walkSpeed = 5f;
    public float turnSpeed = 5f;
    
    [Tooltip("How long the human pauses at each waypoint before moving to the next.")]
    public float pauseAtWaypoint = 1.5f;

    [Header("--- Procedural Animation (No Animator Needed) ---")]
    [Tooltip("How high the human lifts up per step.")]
    public float stepHeight = 0.5f;
    [Tooltip("How fast the steps are.")]
    public float stepSpeed = 10f;
    [Tooltip("How much the human tilts left and right while walking.")]
    public float swayAngle = 5f;

    private int currentWaypointIndex = 0;
    private bool isWalkingRoutineActive = false;
    private float baseY; // Stores the original floor height

    public AK.Wwise.Event humanStep;

    void Start()
    {
        // Remember the starting height so we don't sink into the floor
        baseY = transform.position.y;
        
        // Hide the human initially until triggered
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Call this from a trigger to start the walking sequence.
    /// </summary>
    public void StartWalkingSequence()
    {
        if (isWalkingRoutineActive || waypoints.Length == 0) return;

        // Teleport to the FIRST waypoint (which should be hidden outside the door) BEFORE showing the model
        transform.position = new Vector3(waypoints[0].position.x, baseY, waypoints[0].position.z);
        currentWaypointIndex = 1; // Start moving towards the second point
        
        gameObject.SetActive(true); // Reveal the human
        StartCoroutine(WalkPathRoutine());
    }

    IEnumerator WalkPathRoutine()
    {
        isWalkingRoutineActive = true;

        while (currentWaypointIndex < waypoints.Length)
        {
            Transform targetWP = waypoints[currentWaypointIndex];

            // 1. Move horizontally towards waypoint
            while (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(targetWP.position.x, targetWP.position.z)) > 0.2f)
            {
                // Calculate direction (ignore Y axis)
                Vector3 moveDir = targetWP.position - transform.position;
                moveDir.y = 0;
                moveDir.Normalize();

                // Rotate smoothly towards target
                if (moveDir != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
                }

                // --- PROCEDURAL ANIMATION (The "Fake" Walk) ---
                
                // Absolute Sine wave creates a bouncing effect (0 to 1 to 0)
                float stepBounce = Mathf.Abs(Mathf.Sin(Time.time * stepSpeed)) * stepHeight;
                
                // Normal Sine wave creates a left-to-right sway (-1 to 1)
                float sway = Mathf.Sin(Time.time * stepSpeed / 2f) * swayAngle;

                // Move forward horizontally
                Vector3 newPos = Vector3.MoveTowards(transform.position, new Vector3(targetWP.position.x, transform.position.y, targetWP.position.z), walkSpeed * Time.deltaTime);
                
                // Apply the vertical bounce
                newPos.y = baseY + stepBounce;
                transform.position = newPos;

                // Apply the sway (tilt)
                // We keep the Y-axis rotation from the LookRotation, and add Z-axis sway
                Vector3 currentEuler = transform.eulerAngles;
                transform.rotation = Quaternion.Euler(currentEuler.x, currentEuler.y, sway);

                yield return null; // Wait for next frame
            }

            // 2. Reached waypoint, snap to ground and pause
            transform.position = new Vector3(transform.position.x, baseY, transform.position.z);
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0); // Reset sway

            // Wait a moment at the waypoint (unless it's the very last one outside the door)
            if (currentWaypointIndex < waypoints.Length - 1)
            {
                humanStep.Post(gameObject);
                yield return new WaitForSeconds(pauseAtWaypoint);
            }

            // Move to next waypoint
            currentWaypointIndex++;
        }

        // 3. Finished path (Should be back outside the door)
        gameObject.SetActive(false); // Hide smoothly
        isWalkingRoutineActive = false;
        currentWaypointIndex = 0; // Reset for next time
    }
}