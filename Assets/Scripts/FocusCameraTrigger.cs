using UnityEngine;
using System.Collections;

/// <summary>
/// A diagnostic version of the camera focus trigger with Debug Logs.
/// </summary>
public class CameraFocusTrigger : MonoBehaviour
{
    [Header("--- Target Settings ---")]
    public Transform cameraStandpoint;
    public Transform focusTarget;

    [Header("--- Timing Settings ---")]
    public float transitionInTime = 1.5f;
    public float holdTime = 2.0f;
    public float transitionOutTime = 1.0f;

    [Header("--- Optional Settings ---")]
    public bool triggerOnlyOnce = true;

    private bool hasTriggered = false;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
        
        // --- Setup Checks ---
        if (mainCam == null) Debug.LogError("[CameraFocus] Error: Main Camera not found! Is your camera tagged as 'MainCamera'?");
        if (cameraStandpoint == null) Debug.LogError("[CameraFocus] Error: Missing Camera Standpoint!");
        if (focusTarget == null) Debug.LogError("[CameraFocus] Error: Missing Focus Target!");
    }

    void OnTriggerEnter(Collider other)
    {
        // Log EVERYTHING that touches this trigger
        Debug.Log($"[CameraFocus] Something entered the trigger: {other.gameObject.name}");

        // Check if it's the player
        if (!hasTriggered && other.CompareTag("Player"))
        {
            Debug.Log("[CameraFocus] SUCCESS: Player detected! Starting cinematic...");
            if (triggerOnlyOnce) hasTriggered = true;
            
            StartCoroutine(CinematicFocusRoutine(other.gameObject));
        }
        else if (!other.CompareTag("Player"))
        {
            Debug.Log($"[CameraFocus] FAILED: Ignored {other.gameObject.name} because its Tag is NOT 'Player'. Its current tag is: {other.tag}");
        }
    }

    IEnumerator CinematicFocusRoutine(GameObject player)
    {
        if (mainCam == null) yield break;

        Debug.Log("[CameraFocus] Step 1: Disabling player and camera controls...");
        DustBunnyController bunnyController = player.GetComponent<DustBunnyController>();
        Rigidbody bunnyRb = player.GetComponent<Rigidbody>();
        ThirdPersonCamera tpCamera = FindFirstObjectByType<ThirdPersonCamera>();
        
        // Find CinemachineBrain safely
        Behaviour cinemachineBrain = mainCam.GetComponent("CinemachineBrain") as Behaviour;

        if (bunnyController != null) bunnyController.enabled = false;
        if (bunnyRb != null) bunnyRb.linearVelocity = Vector3.zero; 
        if (tpCamera != null) tpCamera.enabled = false;
        if (cinemachineBrain != null) cinemachineBrain.enabled = false;

        Vector3 originalPos = mainCam.transform.position;
        Quaternion originalRot = mainCam.transform.rotation;
        Vector3 targetPos = cameraStandpoint.position;
        Quaternion targetRot = Quaternion.LookRotation(focusTarget.position - cameraStandpoint.position);

        Debug.Log("[CameraFocus] Step 2: Transitioning IN...");
        float elapsed = 0f;
        while (elapsed < transitionInTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionInTime);
            mainCam.transform.position = Vector3.Lerp(originalPos, targetPos, t);
            mainCam.transform.rotation = Quaternion.Slerp(originalRot, targetRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        mainCam.transform.position = targetPos;
        mainCam.transform.rotation = targetRot;

        Debug.Log($"[CameraFocus] Step 3: Holding focus for {holdTime} seconds...");
        yield return new WaitForSeconds(holdTime);

        Debug.Log("[CameraFocus] Step 4: Transitioning OUT...");
        elapsed = 0f;
        while (elapsed < transitionOutTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionOutTime);
            mainCam.transform.position = Vector3.Lerp(targetPos, originalPos, t);
            mainCam.transform.rotation = Quaternion.Slerp(targetRot, originalRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("[CameraFocus] Step 5: Restoring controls...");
        if (tpCamera != null) tpCamera.enabled = true;
        if (cinemachineBrain != null) cinemachineBrain.enabled = true;
        if (bunnyController != null) bunnyController.enabled = true;
        
        Debug.Log("[CameraFocus] Sequence complete!");
    }
}