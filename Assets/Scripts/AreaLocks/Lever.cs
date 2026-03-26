using UnityEngine;
using System.Collections;

public class Lever : MonoBehaviour
{
    [Header("Lever State")]
    [SerializeField] private bool isHooked = false;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform hook;

    [Header("Hook Settings")]
    [SerializeField] private float hookDropAmount = 0.8f;
    [SerializeField] private float hookMoveDuration = 0.5f;

    [Header("Camera")]
    [SerializeField] private CameraFocusTrigger cameraFocusScript;
    [SerializeField] private float delayBeforeCamera = 1f;
    [SerializeField] private float delayBeforeMovingHook = 1.5f;

    private bool hasActivated = false;
    private Vector3 hookStartPosition;

    // 🔹 Player references
    private DustBunnyController currentPlayer;
    private Rigidbody playerRb;
    private Animator playerAnimator;

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        if (hook != null)
            hookStartPosition = hook.localPosition;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryActivate(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryActivate(collision.collider);
    }

    private void TryActivate(Collider other)
    {
        if (other == null) return;
        if (hasActivated) return;

        DustBunnyController player = other.GetComponent<DustBunnyController>();
        if (player == null)
            player = other.GetComponentInParent<DustBunnyController>();

        if (player == null) return;
        if (!player.isRolling) return;

        // store references
        currentPlayer = player;
        playerRb = player.GetComponent<Rigidbody>();
        playerAnimator = player.GetComponentInChildren<Animator>();

        HookLever();
    }

    public void HookLever()
    {
        if (hasActivated) return;

        isHooked = true;
        hasActivated = true;

        StartCoroutine(LeverSequence());
    }

    private IEnumerator LeverSequence()
    {
        // Freeze player completely
        if (currentPlayer != null)
        {
            currentPlayer.enabled = false;
        }

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        if (playerAnimator != null)
        {
            playerAnimator.enabled = false;
            playerAnimator.enabled = true;
        }

        // Play lever animation
        if (animator != null)
        {
            animator.SetBool("isHooked", true);
        }

        // Wait before camera pan
        yield return new WaitForSeconds(delayBeforeCamera);

        // Enable camera focus
        if (cameraFocusScript != null)
        {
            cameraFocusScript.enabled = true;
            cameraFocusScript.hasTriggered = false;
        }

        // Wait before moving hook
        yield return new WaitForSeconds(delayBeforeMovingHook);

        // Move hook
        if (hook != null)
        {
            Vector3 targetPos = hookStartPosition;
            targetPos.y -= hookDropAmount;

            yield return StartCoroutine(MoveHookSmoothly(targetPos));
        }
    }

    public bool IsHooked()
    {
        return isHooked;
    }

    private IEnumerator MoveHookSmoothly(Vector3 targetPosition)
    {
        Vector3 startPos = hook.localPosition;
        float elapsed = 0f;

        while (elapsed < hookMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / hookMoveDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            hook.localPosition = Vector3.Lerp(startPos, targetPosition, t);
            yield return null;
        }

        hook.localPosition = targetPosition;
    }
}