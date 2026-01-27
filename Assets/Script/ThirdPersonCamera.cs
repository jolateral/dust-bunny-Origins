using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target; // Drag Player here
    
    [Header("Distance Settings")]
    public float baseDistance = 10.0f;
    public float height = 5.0f;
    
    [Header("Controls")]
    public float rotationSpeed = 2.0f;

    private float currentX = 0.0f;
    private float currentY = 0.0f;

    void Start()
    {
        // Hide mouse cursor
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Right stick input via New Input System
        Vector2 rightStick = Vector2.zero;
        if (Gamepad.current != null)
        {
            rightStick = Gamepad.current.rightStick.ReadValue(); // X = horizontal, Y = vertical
        }

        // Mouse Input via Legacy Input System
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // Combine both mouse and right stick
        currentX += (rightStick.x + mouseX) * rotationSpeed;
        currentY -= (rightStick.y + mouseY) * rotationSpeed;
        currentY = Mathf.Clamp(currentY, -20f, 60f); // Limit vertical rotation

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