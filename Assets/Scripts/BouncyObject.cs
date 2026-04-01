using UnityEngine;
using System.Collections;

public class BouncyObject : MonoBehaviour
{
    [Header("Settings")]
    public float bounceForce = 25f;
    public float bounceDuration = 0.12f; // longer duration = softer launch

    [SerializeField] private bool bookshelfStapler = false;

    public AK.Wwise.Event staplerWindup;
    public AK.Wwise.Event staplerRelease;

    [SerializeField] private Sprite hintImage;

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnCollisionEnter(Collision collision)
    {
        staplerWindup.Post(gameObject);

        if (collision.gameObject.CompareTag("Player") && bookshelfStapler && DiaryUIManager.Instance != null && !DiaryUIManager.Instance.diaryShown)
        {
            MemoryUIManager.Instance.ShowImage(hintImage);
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
                    MemoryUIManager.Instance.ShowImage(hintImage);
                return false;
            }
        }

        StartCoroutine(DoBounceSmooth(playerRb));
        return true;
    }

    IEnumerator DoBounceSmooth(Rigidbody rb)
    {
        staplerRelease.Post(gameObject);

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