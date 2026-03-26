using UnityEngine;
using System.Collections;

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
    [SerializeField] private float delayBeforeCameraFocus = 0.5f;
    [SerializeField] private float delayBeforeMovingWall = 1.75f;


    [SerializeField] private CameraFocusTrigger cameraFocusScript;

    private Vector3 startPosition;
    private Vector3 hookStartPos;
    private Vector3 wallStartPos;

    private bool isActivated = false;

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
            isActivated = true; 
            StartCoroutine(PlateSequence());
        }
        else
        {
            Debug.Log("Lever is not hooked yet!");
        }
    }

    private IEnumerator PlateSequence()
    {
        // Press the button DOWN
        Vector3 pressedPos = startPosition;
        pressedPos.y -= pressAmount;

        yield return StartCoroutine(MoveSmooth(transform, pressedPos, pressDuration));

        // Wait before raising hook/wall
        yield return new WaitForSeconds(delayBeforeCameraFocus);

        // Enable camera focus
        if (cameraFocusScript != null)
        {
            cameraFocusScript.enabled = true;
            cameraFocusScript.hasTriggered = false;
        }
        // Wait for camera to reach focus
        yield return new WaitForSeconds(delayBeforeMovingWall);

        // Raise wall and hook
        if (hook != null)
        {
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