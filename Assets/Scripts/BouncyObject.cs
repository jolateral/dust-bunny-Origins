using UnityEngine;

public class BouncyObject : MonoBehaviour
{
    [Header("Settings")]
    public float bounceForce = 25f;

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Rigidbody rb = collision.gameObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 velocity = rb.linearVelocity;
                velocity.y = 0f;
                rb.linearVelocity = velocity;

                rb.AddForce(Vector3.up * bounceForce, ForceMode.Impulse);

                // Trigger stapler animation
                if (animator != null)
                    animator.SetTrigger("bounce");
            }
        }
    }
}