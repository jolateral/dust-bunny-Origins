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
        // Only show "stapler isn't working" hint when touching bookshelf stapler without diary; no auto-bounce
        if (collision.gameObject.CompareTag("Player") && bookshelfStapler && DiaryUIManager.Instance != null && !DiaryUIManager.Instance.diaryShown)
        {
            MemoryUIManager.Instance.ShowMemory("This stapler isn't working...Maybe I need to view the diary first.", Color.red);
        }
    }

    /// <summary>
    /// Called when the player presses jump while standing on this object. Returns true if a bounce was applied (player should not do normal jump).
    /// </summary>
    public bool TryBounce(Rigidbody playerRb)
    {
        if (playerRb == null) return false;
        if (bookshelfStapler)
        {
            if (DiaryUIManager.Instance == null || !DiaryUIManager.Instance.diaryShown)
            {
                if (MemoryUIManager.Instance != null)
                    MemoryUIManager.Instance.ShowMemory("This stapler isn't working...Maybe I need to view the diary first.", Color.red);
                return false;
            }
        }
        DoBounce(playerRb);
        return true;
    }

    void DoBounce(Rigidbody rb)
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        // Scale impulse by mass so bounce height is consistent (matches player's jump scaling)
        rb.AddForce(Vector3.up * (bounceForce * rb.mass), ForceMode.Impulse);

        if (animator != null)
            animator.SetTrigger("bounce");
    }
}