using UnityEngine;
using System.Collections;

/// <summary>
/// A standalone trigger that temporarily hijacks the camera to focus on a specific point,
/// completely freezes the player (disabling DustBunnyController and physics), 
/// and smoothly returns control afterward.
/// </summary>
public class CameraFocusTrigger : MonoBehaviour
{
    [Header("--- Target Settings ---")]
    [Tooltip("The exact position where the camera should move to during the cinematic.")]
    public Transform cameraStandpoint;
    
    [Tooltip("The object or point the camera should look at.")]
    public Transform focusTarget;

    [Header("--- Timing Settings ---")]
    [Tooltip("Time it takes to move from the player to the camera standpoint.")]
    public float transitionInTime = 1.5f;
    [Tooltip("How long the camera stays at the standpoint.")]
    public float holdTime = 2.0f;
    [Tooltip("Time it takes to return to the player.")]
    public float transitionOutTime = 1.0f;

    [Header("--- Optional Settings ---")]
    [Tooltip("Should this only trigger once?")]
    public bool triggerOnlyOnce = true;

    private bool hasTriggered = false;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if it's the player and if it hasn't been triggered yet
        if (!hasTriggered && other.CompareTag("Player"))
        {
            if (triggerOnlyOnce) hasTriggered = true;
            
            StartCoroutine(CinematicFocusRoutine(other.gameObject));
        }
    }

    IEnumerator CinematicFocusRoutine(GameObject player)
    {
        if (mainCam == null) yield break;

        // Get all necessary components
        DustBunnyController bunnyController = player.GetComponent<DustBunnyController>();
        Rigidbody bunnyRb = player.GetComponent<Rigidbody>();
        
        // Find the custom ThirdPersonCamera script
        ThirdPersonCamera tpCamera = FindFirstObjectByType<ThirdPersonCamera>();
        
        // Find CinemachineBrain if it exists
        Behaviour cinemachineBrain = null;
        Component[] components = mainCam.GetComponents<Component>();
        foreach (Component comp in components)
        {
            if (comp.GetType().Name == "CinemachineBrain")
            {
                cinemachineBrain = (Behaviour)comp;
                break;
            }
        }

        // --- 1. ABSOLUTE PLAYER FREEZE ---
        if (bunnyController != null) 
        {
            Animator anim = player.GetComponent<Animator>();
            if (anim != null)
            {
                anim.SetBool("isRunning", false);
                anim.SetBool("isRolling", false);
                anim.SetBool("isGliding", false);
            }
            // Disable the controller so no input is processed
            bunnyController.enabled = false; 
        }
        if (bunnyRb != null) 
        {
            // Kill all momentum and disable physics/gravity temporarily
            bunnyRb.linearVelocity = Vector3.zero; 
            bunnyRb.angularVelocity = Vector3.zero;
            bunnyRb.isKinematic = true; 
        }

        // Lower sound volume/kill movement sounds
        AkUnitySoundEngine.SetState("player_state", "pause");

        // --- 2. DISABLE CAMERA CONTROLS ---
        if (tpCamera != null) tpCamera.enabled = false;
        if (cinemachineBrain != null) cinemachineBrain.enabled = false;

        // --- 3. SAVE ORIGINAL CAMERA STATE ---
        Vector3 originalPos = mainCam.transform.position;
        Quaternion originalRot = mainCam.transform.rotation;

        Vector3 targetPos = cameraStandpoint.position;
        Quaternion targetRot = Quaternion.LookRotation(focusTarget.position - cameraStandpoint.position);

        // --- 4. TRANSITION IN ---
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

        // --- 5. HOLD FOCUS ---
        yield return new WaitForSeconds(holdTime);

        // --- 6. TRANSITION OUT ---
        elapsed = 0f;
        while (elapsed < transitionOutTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionOutTime);
            
            mainCam.transform.position = Vector3.Lerp(targetPos, originalPos, t);
            mainCam.transform.rotation = Quaternion.Slerp(targetRot, originalRot, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        // --- 7. RESTORE EVERYTHING ---
        if (tpCamera != null) tpCamera.enabled = true;
        if (cinemachineBrain != null) cinemachineBrain.enabled = true;
        
        // UNFREEZE THE PLAYER
        if (bunnyRb != null) 
        {
            // Re-enable physics and gravity
            bunnyRb.isKinematic = false; 
        }
        if (bunnyController != null) 
        {
            // Re-enable input and movement
            bunnyController.enabled = true; 
        }

        // Resume normal sound
        AkUnitySoundEngine.SetState("player_state", "None");
    }
}