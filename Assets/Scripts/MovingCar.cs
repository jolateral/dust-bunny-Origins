using UnityEngine;

public class MovingCar : MonoBehaviour
{
    [Header("Movement Settings")]
    public Vector3 movementDirection = Vector3.forward; // Movement direction
    public float speed = 1.0f;      // Movement speed
    public float distance = 10.0f;   // Movement distance

    private Vector3 startPos;
    private float phaseOffset; // Random offset so cars aren't in sync

    void Start()
    {
        startPos = transform.position;
        phaseOffset = Random.Range(0f, 1f); // Random phase so each car is at a different point in the cycle
    }

    void Update()
    {
        // Use PingPong with phase offset so cars move out of sync
        float cycle = Mathf.PingPong(Time.time * speed + phaseOffset, 1f);

        // Interpolate between start and end positions
        // EndPos = StartPos + Direction * Distance
        Vector3 endPos = startPos + movementDirection.normalized * distance;

        transform.position = Vector3.Lerp(startPos, endPos, cycle);
    }
}
