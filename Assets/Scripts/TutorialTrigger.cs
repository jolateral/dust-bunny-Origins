using UnityEngine;

/// <summary>
/// TutorialTrigger.cs
/// 
/// Shows tutorial text when player approaches this object.
/// Uses MemoryUIManager to display tutorial prompts (similar to memory object fragments).
/// 
/// Usage:
/// 1. Attach this script to a GameObject (Dust Particle, Fleeing Dust Particle, Knockable Block, etc.)
/// 2. Set the tutorial text and color
/// 3. Set the trigger distance
/// 4. Set whether it should only trigger once
/// 
/// Note: The collider on this GameObject doesn't need to be a trigger - this script uses distance checking.
/// </summary>
public class TutorialTrigger : MonoBehaviour
{
    [Header("Tutorial Settings")]
    [TextArea(2, 5)]
    [Tooltip("The tutorial text to display")]
    public string tutorialText = "Press SHIFT to roll and absorb an object";
    [Tooltip("Color of the tutorial text")]
    public Color textColor = Color.white;
    
    [Header("Trigger Settings")]
    [Tooltip("Distance at which the tutorial triggers")]
    public float triggerDistance = 5f;
    
    [Tooltip("Should this tutorial only show once?")]
    public bool oneTimeOnly = true;
    
    [Tooltip("Delay before showing tutorial (in seconds)")]
    public float showDelay = 0.5f;
    
    private bool hasBeenTriggered = false;
    private Transform playerTransform;
    private bool isShowing = false;
    private float lastCheckTime = 0f;
    private const float CHECK_INTERVAL = 0.1f; // Check every 0.1 seconds for performance
    
    void Start()
    {
        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning($"TutorialTrigger '{name}': No player found with 'Player' tag!");
            enabled = false;
        }
    }
    
    void Update()
    {
        if (playerTransform == null || (hasBeenTriggered && oneTimeOnly) || isShowing)
            return;
        
        // Throttle distance checks for performance
        if (Time.time - lastCheckTime < CHECK_INTERVAL)
            return;
        
        lastCheckTime = Time.time;
        
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        
        if (distanceToPlayer <= triggerDistance)
        {
            TriggerTutorial();
        }
    }
    
    void TriggerTutorial()
    {
        if ((hasBeenTriggered && oneTimeOnly) || isShowing)
            return;
        
        hasBeenTriggered = true;
        isShowing = true;
        
        // Show tutorial text using MemoryUIManager after delay
        if (showDelay > 0f)
        {
            Invoke(nameof(ShowTutorialText), showDelay);
        }
        else
        {
            ShowTutorialText();
        }
        
        // Reset isShowing flag after display time (3 seconds is the display time from MemoryUIManager)
        Invoke(nameof(ResetShowing), showDelay + 3f);
    }
    
    void ShowTutorialText()
    {
        if (MemoryUIManager.Instance != null)
        {
            MemoryUIManager.Instance.ShowMemory(tutorialText, textColor);
        }
        else
        {
            Debug.LogWarning($"TutorialTrigger '{name}': MemoryUIManager.Instance not found!");
        }
    }

    /// <summary> Show this tutorial's text now (e.g. called by GlideLaunchSpot when player enters zone). </summary>
    public void ShowTutorial()
    {
        ShowTutorialText();
    }

    /// <summary> Hide the tutorial display (e.g. when player leaves the zone). </summary>
    public void HideTutorial()
    {
        if (MemoryUIManager.Instance != null)
            MemoryUIManager.Instance.Hide();
    }
    
    void ResetShowing()
    {
        isShowing = false;
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw trigger distance sphere in editor
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, triggerDistance);
        
        // Draw a line to show it's a tutorial trigger
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (triggerDistance + 0.5f),
            "Tutorial Trigger"
        );
        #endif
    }
}
