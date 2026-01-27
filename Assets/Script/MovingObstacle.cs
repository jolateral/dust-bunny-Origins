using UnityEngine;

public class MovingObstacle : MonoBehaviour
{
    [Header("Movement Settings")]
    public Vector3 movementDirection = Vector3.right; // Movement direction
    public float speed = 2.0f;      // Movement speed
    public float distance = 3.0f;   // Movement distance

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // Use PingPong function to generate oscillating values between 0 and 1
        float cycle = Mathf.PingPong(Time.time * speed, 1f);
        
        // Interpolate between start and end positions
        // EndPos = StartPos + Direction * Distance
        Vector3 endPos = startPos + movementDirection.normalized * distance;
        
        transform.position = Vector3.Lerp(startPos, endPos, cycle);
    }
}