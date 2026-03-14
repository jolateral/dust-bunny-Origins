using UnityEngine;
using System.Collections;

public class BouncyObject : MonoBehaviour
{
    [Header("Settings")]
    public float bounceForce = 25f;
    public float bounceDuration = 0.12f; // longer duration = softer launch

    [SerializeField] private bool bookshelfStapler = false;

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") && bookshelfStapler && DiaryUIManager.Instance != null && !DiaryUIManager.Instance.diaryShown)
        {
            MemoryUIManager.Instance.ShowMemory("This stapler isn't working...Maybe I need to view the diary first.", Color.red);
        }
    }

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

        StartCoroutine(DoBounceSmooth(playerRb));
        return true;
    }

    IEnumerator DoBounceSmooth(Rigidbody rb)
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        float elapsed = 0f;
        float totalForce = bounceForce * rb.mass;

        if (animator != null)
            animator.SetTrigger("bounce");

        while (elapsed < bounceDuration)
        {
            rb.AddForce(Vector3.up * (totalForce / bounceDuration), ForceMode.Force);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }
}