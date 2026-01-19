using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target; // Drag Player here
    
    [Header("Distance Settings")]
    public float baseDistance = 10.0f;
    public float height = 5.0f;
    
    [Header("Controls")]
    public float rotationSpeed = 5.0f;

    private float currentX = 0.0f;
    private float currentY = 0.0f;

    void Start()
    {
        // Hide mouse cursor
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Mouse Input
        currentX += Input.GetAxis("Mouse X") * rotationSpeed;
        currentY -= Input.GetAxis("Mouse Y") * rotationSpeed;
        currentY = Mathf.Clamp(currentY, -20, 60); // Limit vertical angle
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
        
        transform.LookAt(target.position);
    }
}