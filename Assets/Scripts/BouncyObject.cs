using UnityEngine;

public class BouncyObject : MonoBehaviour
{
    [Header("Settings")]
    public float bounceForce = 25f;

    [SerializeField] private bool bookshelfStapler = false;

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
            if (rb != null && !bookshelfStapler)
            {
                bounce(rb);
            }
            // Lock stapler from desk to bookshelf until they find diary
            else if (rb != null && bookshelfStapler)
            {
                if (DiaryUIManager.Instance.diaryShown)
                {
                    bounce(rb);
                }
                else
                {
                    MemoryUIManager.Instance.ShowMemory("This stapler isn't working...Maybe I need to view the diary first.", Color.red);
                }
                
            }
        }
    }

    void bounce(Rigidbody rb)
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