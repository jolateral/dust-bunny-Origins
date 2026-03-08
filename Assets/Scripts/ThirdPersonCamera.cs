using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target; // Drag Player here
    
    [Header("--- Distance Settings ---")]
    public float baseDistance = 10.0f;
    public float height = 5.0f;
    
    [Header("--- Controls ---")]
    public float rotationSpeed = 2.0f;

    [Header("--- Collision Settings (Anti-Clipping) ---")]
    public LayerMask collisionLayers; 
    
    [Tooltip("The physical thickness of the camera.")]
    public float cameraRadius = 0.5f; 
    
    [Tooltip("Extra buffer distance to prevent the screen corners from clipping.")]
    public float collisionPadding = 0.2f; // NEW: Buffer zone
    
    public float recoverSpeed = 10f;

    // NEW: Camera Shake Settings
    [Header("--- Camera Shake Settings ---")]
    private float currentShakeDuration = 0f;
    private float currentShakeMagnitude = 0f;

    private float currentX = 0.0f;
    private float currentY = 0.0f;
    private float currentDistance; 

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        currentDistance = baseDistance;

        // Initialize camera to start behind the target, aligned with the bunny facing the level.
        if (target != null)
        {
            currentX = target.eulerAngles.y;
            currentY = 15f; // slight downward tilt so you see the bunny and what it's facing
        }
    }

    void Update()
    {
        Vector2 rightStick = Vector2.zero;
        if (Gamepad.current != null)
        {
            rightStick = Gamepad.current.rightStick.ReadValue();
        }

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        currentX += (rightStick.x + mouseX) * rotationSpeed;
        currentY -= (rightStick.y + mouseY) * rotationSpeed;
        currentY = Mathf.Clamp(currentY, -20f, 60f);
    }

    void LateUpdate()
    {
        if (!target) return;

        float currentScale = target.localScale.x;
        float targetBaseDistance = baseDistance * currentScale;
        float actualHeight = height * currentScale;

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 direction = new Vector3(0, 0, -targetBaseDistance);
        
        Vector3 lookAtPoint = target.position + Vector3.up * actualHeight;
        Vector3 desiredPosition = lookAtPoint + rotation * direction;

        float targetDistance = targetBaseDistance;
        Vector3 castDir = desiredPosition - lookAtPoint;

        // Anti-Clipping logic: if raycast hits a wall
        if (Physics.SphereCast(lookAtPoint, cameraRadius * currentScale, castDir.normalized, out RaycastHit hit, targetBaseDistance, collisionLayers))
        {
            // Back up slightly before the hit point to prevent clipping
            targetDistance = Mathf.Max(0.1f, hit.distance - (collisionPadding * currentScale));
        }

        if (targetDistance < currentDistance)
        {
            currentDistance = targetDistance; // Snap close instantly to avoid wall clip
        }
        else
        {
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * recoverSpeed); // Smooth recovery
        }

        Vector3 finalDirection = new Vector3(0, 0, -currentDistance);
        Vector3 finalPosition = lookAtPoint + rotation * finalDirection;

        // NEW: Apply Camera Shake safely to the final calculated position
        if (currentShakeDuration > 0)
        {
            // Add random spherical noise based on magnitude
            finalPosition += Random.insideUnitSphere * currentShakeMagnitude;
            
            // Reduce timer
            currentShakeDuration -= Time.deltaTime;
        }

        // Apply position and look at the original smooth target point
        transform.position = finalPosition;
        transform.LookAt(lookAtPoint);
    }

    // NEW: Public method to trigger shake
    /// <summary>
    /// Triggers a camera shake effect.
    /// </summary>
    /// <param name="duration">How long the shake lasts in seconds.</param>
    /// <param name="magnitude">How violent the shake is.</param>
    public void TriggerShake(float duration = 0.2f, float magnitude = 0.5f)
    {
        currentShakeDuration = duration;
        currentShakeMagnitude = magnitude;
    }
}