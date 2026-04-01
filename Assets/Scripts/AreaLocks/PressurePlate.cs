using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PressurePlate : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Lever lever;
    [SerializeField] private Transform hook;
    [SerializeField] private Transform wall;

    [Header("Movement Settings")]
    [SerializeField] private float raiseAmount = 2.5f;
    [SerializeField] private float moveDuration = 1f;

    [SerializeField] private float pressAmount = 0.3f;
    [SerializeField] private float pressDuration = 1f;

    [Header("Timing")]
    [SerializeField] private float delayBeforeCameraFocus = 0.2f;
    [SerializeField] private float delayBeforeMovingWall = 1.75f;

    [SerializeField] private Sprite displayTooLightPopup;

    [SerializeField] private CameraFocusTrigger cameraFocusScript;

    [Header("SFX")]
    public AK.Wwise.Event buttonPress;
    public AK.Wwise.Event buttonClick;
    public AK.Wwise.Event doorOpen;

    private Vector3 startPosition;
    private Vector3 hookStartPos;
    private Vector3 wallStartPos;

    private bool isActivated = false;

    // 🔹 Player references
    private DustBunnyController currentPlayer;
    private Rigidbody playerRb;
    private Animator playerAnimator;

    private void Start()
    {
        if (hook != null)
            hookStartPos = hook.localPosition;

        if (wall != null)
            wallStartPos = wall.localPosition;

        startPosition = transform.localPosition;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isActivated) return;

        Collider other = collision.collider;

        DustBunnyController player = other.GetComponent<DustBunnyController>();
        if (player == null)
            player = other.GetComponentInParent<DustBunnyController>();

        if (player == null) return;

        if (lever != null && lever.IsHooked())
        {
            // Store player references
            currentPlayer = player;
            playerRb = player.GetComponent<Rigidbody>();
            playerAnimator = player.GetComponentInChildren<Animator>();

            isActivated = true;

            StartCoroutine(PlateSequence());
        }
        else
        {
            MemoryUIManager.Instance.ShowImage(displayTooLightPopup);
        }
    }

    private IEnumerator PlateSequence()
    {
        // Freeze player
        if (currentPlayer != null)
            currentPlayer.enabled = false;

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

        // Press button DOWN
        Vector3 pressedPos = startPosition;
        pressedPos.y -= pressAmount;

        buttonPress.Post(gameObject);

        yield return StartCoroutine(MoveSmooth(transform, pressedPos, pressDuration));

        buttonClick.Post(gameObject);

        // Wait before camera focus
        yield return new WaitForSeconds(delayBeforeCameraFocus);

        // Enable camera focus
        if (cameraFocusScript != null)
        {
            cameraFocusScript.enabled = true;
            cameraFocusScript.hasTriggered = false;
        }

        // Wait before moving wall
        yield return new WaitForSeconds(delayBeforeMovingWall);

        // Raise hook
        if (hook != null)
        {
            doorOpen.Post(gameObject);
            Vector3 target = hookStartPos;
            target.y += raiseAmount;
            StartCoroutine(MoveSmooth(hook, target, moveDuration));
        }

        // Raise wall
        if (wall != null)
        {
            Vector3 target = wallStartPos;
            target.y += raiseAmount + 0.9f;
            StartCoroutine(MoveSmooth(wall, target, moveDuration));
        }

        // Unfreeze player AFTER everything starts moving
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator MoveSmooth(Transform obj, Vector3 target, float duration)
    {
        Vector3 start = obj.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            obj.localPosition = Vector3.Lerp(start, target, t);
            yield return null;
        }

        obj.localPosition = target;
    }
}