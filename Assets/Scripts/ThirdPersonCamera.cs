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

        // 核心修改：如果射线打到墙壁
        if (Physics.SphereCast(lookAtPoint, cameraRadius * currentScale, castDir.normalized, out RaycastHit hit, targetBaseDistance, collisionLayers))
        {
            // 在击中点之前，再往后退一点点 (collisionPadding)，确保镜头玻璃不穿墙
            targetDistance = Mathf.Max(0.1f, hit.distance - (collisionPadding * currentScale));
        }

        if (targetDistance < currentDistance)
        {
            currentDistance = targetDistance; // 瞬间拉近，防止穿墙
        }
        else
        {
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * recoverSpeed); // 平滑恢复
        }

        Vector3 finalDirection = new Vector3(0, 0, -currentDistance);
        transform.position = lookAtPoint + rotation * finalDirection;
        transform.LookAt(lookAtPoint);
    }
}