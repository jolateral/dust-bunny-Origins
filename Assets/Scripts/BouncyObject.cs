using UnityEngine;

public class BouncyObject : MonoBehaviour
{
    [Header("Settings")]
    public float bounceForce = 25f; // Power of the bounce

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Rigidbody rb = collision.gameObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // This ensures consistent jump height regardless of how fast you fell
                Vector3 velocity = rb.linearVelocity;
                velocity.y = 0f;
                rb.linearVelocity = velocity;

                // Apply instantaneous upward force
                rb.AddForce(Vector3.up * bounceForce, ForceMode.Impulse);
            }
        }
    }
}