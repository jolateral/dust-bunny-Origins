using UnityEngine;
// Important: This namespace is required for Gamepad support
using UnityEngine.InputSystem; 

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target; // Drag Player here
    
    [Header("Distance Settings")]
    public float baseDistance = 10.0f;
    public float height = 5.0f;
    
    [Header("Katamari-Style POV")]
    [Tooltip("Wider FOV to see more of the environment (Katamari-style)")]
    public float fieldOfView = 75f;
    [Tooltip("Look-at point above player center - puts player in bottom third of screen")]
    public float lookAtHeightOffset = 3f;
    
    [Header("Controls")]
    public float rotationSpeed = 1.0f; // Adjusted sensitivity
    
    [Header("Initial Look Target")]
    [Tooltip("Optional: Camera will face this object at start. Leave empty to use default rotation.")]
    public Transform initialLookTarget;

    private float currentX = 0.0f;
    private float currentY = 0.0f;
    private Camera cam;

    void Start()
    {
        // Hide mouse cursor
        Cursor.lockState = CursorLockMode.Locked;
        
        cam = GetComponent<Camera>();
        if (cam != null)
            cam.fieldOfView = fieldOfView;
        
        // Initialize camera to face dust particle if target exists
        InitializeCameraRotation();
    }
    
    void InitializeCameraRotation()
    {
        if (!target) return;
        
        Transform lookTarget = initialLookTarget;
        
        // If no initialLookTarget is assigned, try to find "Dust Particle" by name
        if (lookTarget == null)
        {
            GameObject dustParticle = GameObject.Find("Dust Particle");
            if (dustParticle != null)
            {
                lookTarget = dustParticle.transform;
            }
        }
        
        if (lookTarget != null)
        {
            // Direction from player to dust particle
            Vector3 directionToTarget = lookTarget.position - target.position;

            // Horizontal (XZ) direction from player to dust particle
            Vector3 flatDir = new Vector3(directionToTarget.x, 0f, directionToTarget.z);
            if (flatDir.sqrMagnitude < 0.0001f)
            {
                return; // Avoid degenerate case if they are on top of each other
            }

            flatDir.Normalize();

            // Yaw: we want the direction from camera -> player to line up
            // with player -> dust, so dust is in front of the bunny
            currentX = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;

            // Pitch: angle based on vertical offset to dust
            float horizontalDistance = new Vector2(directionToTarget.x, directionToTarget.z).magnitude;
            currentY = Mathf.Atan2(directionToTarget.y, horizontalDistance) * Mathf.Rad2Deg;

            // Clamp vertical rotation to valid range
            currentY = Mathf.Clamp(currentY, -20f, 60f);
        }
    }

    void Update()
    {
        // 1. Get Gamepad Input (New Input System)
        Vector2 rightStick = Vector2.zero;
        if (Gamepad.current != null)
        {
            rightStick = Gamepad.current.rightStick.ReadValue();
        }

        // 2. Get Mouse Input (Legacy Input System)
        // We combine both so you can use either device at any time
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // 3. Apply Rotation
        // Add Stick X + Mouse X
        currentX += (rightStick.x + mouseX) * rotationSpeed;
        
        // Subtract Stick Y + Mouse Y (Invert Y axis logic)
        currentY -= (rightStick.y + mouseY) * rotationSpeed;
        
        // Limit vertical rotation so camera doesn't flip
        currentY = Mathf.Clamp(currentY, -20f, 60f);
    }

    void LateUpdate()
    {
        if (!target) return;

        // Dynamic Distance based on Target Size (Growth)
        float currentScale = target.localScale.x;
        float actualDistance = baseDistance * currentScale;
        float actualHeight = height * currentScale;

        // Calculate Rotation and Position
        Vector3 dir = new Vector3(0, 0, -actualDistance);
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        
        // Final position logic
        transform.position = target.position + rotation * dir;
        transform.position += Vector3.up * actualHeight; // Add height offset
        
        // Look at point above player = player appears in bottom third (Katamari-style)
        float scaledLookOffset = lookAtHeightOffset * currentScale;
        Vector3 lookAtPoint = target.position + Vector3.up * scaledLookOffset;
        transform.LookAt(lookAtPoint);
    }
}