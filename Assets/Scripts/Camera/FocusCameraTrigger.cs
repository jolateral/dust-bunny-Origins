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

    [Tooltip("If off, OnTriggerEnter/Stay do nothing — use TryTriggerFocus() from another script (e.g. pressure plate collision).")]
    public bool useTriggerVolume = true;

    [Tooltip("If set, shown through MemoryUIManager when this focus starts (e.g. PressurePlate.png with the door pan).")]
    public Sprite popupImage;

    public AK.Wwise.Event stingerSfx;

    public bool hasTriggered = false;
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!useTriggerVolume) return;
        if (hasTriggered && triggerOnlyOnce) return;
        if (!other.CompareTag("Player")) return;

        TryStartFocus(other.gameObject);
    }

    void OnTriggerStay(Collider other)
    {
        if (!useTriggerVolume) return;
        if (hasTriggered && triggerOnlyOnce) return;
        if (!other.CompareTag("Player")) return;

        TryStartFocus(other.gameObject);
    }

    /// <summary>
    /// Starts the focus cinematic from code (e.g. first collision with the pressure plate). Returns false if skipped.
    /// </summary>
    public bool TryTriggerFocus(GameObject playerRoot)
    {
        if (!isActiveAndEnabled) return false;
        if (playerRoot == null) return false;
        if (hasTriggered && triggerOnlyOnce) return false;
        if (playerRoot.GetComponentInParent<DustBunnyController>() == null) return false;

        TryStartFocus(playerRoot);
        return true;
    }

    private void TryStartFocus(GameObject playerRoot)
    {
        if (hasTriggered && triggerOnlyOnce) return;

        if (triggerOnlyOnce) hasTriggered = true;

        if (stingerSfx != null)
            stingerSfx.Post(gameObject);

        StartCoroutine(CinematicFocusRoutine(playerRoot));
    }

    IEnumerator CinematicFocusRoutine(GameObject player)
    {
        if (mainCam == null) yield break;

        if (popupImage != null && MemoryUIManager.Instance != null)
            MemoryUIManager.Instance.ShowImage(popupImage);

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
        AkUnitySoundEngine.SetState("player_state", "memory");

        // --- 2. DISABLE CAMERA CONTROLS & COLLIDERS ---
        if (tpCamera != null) tpCamera.enabled = false;
        if (cinemachineBrain != null) cinemachineBrain.enabled = false;

        // NEW: Turn off any colliders on the camera so it doesn't hit or trigger things while panning
        Collider[] cameraColliders = mainCam.GetComponents<Collider>();
        foreach (Collider col in cameraColliders)
        {
            col.enabled = false;
        }

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

        // NEW: Re-enable the camera colliders after the pan is finished
        foreach (Collider col in cameraColliders)
        {
            col.enabled = true;
        }
        
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