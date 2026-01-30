using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// PaperUIManager.cs
/// 
/// Manages the full-screen paper overlay UI that appears when the player
/// reads a paper item. This script:
/// - Shows a large white rectangle (paper) with text
/// - Freezes player movement while paper is showing
/// - Waits for player to press a button (Space/Jump) to dismiss
/// - Handles fade in/out animations
/// 
/// Setup:
/// 1. Create a Canvas in your scene (Screen Space - Overlay)
/// 2. Add this script to a GameObject
/// 3. Create UI elements and assign them in the inspector:
///    - Background Panel (semi-transparent black)
///    - Paper Panel (white rectangle)
///    - Text element (TextMeshProUGUI for the content)
///    - Instruction text ("Press SPACE to continue")
/// </summary>
public class PaperUIManager : MonoBehaviour
{
    // Singleton pattern (same as MemoryUIManager)
    public static PaperUIManager Instance;
    
    [Header("UI References")]
    [Tooltip("The dark background that dims the screen")]
    public CanvasGroup backgroundGroup;
    
    [Tooltip("The white paper rectangle")]
    public CanvasGroup paperGroup;
    
    [Tooltip("The text component that displays the paper content")]
    public TextMeshProUGUI paperText;
    
    [Tooltip("The instruction text (e.g., 'Press SPACE to continue')")]
    public TextMeshProUGUI instructionText;
    
    [Header("Animation Settings")]
    [Tooltip("How fast the paper fades in/out")]
    public float fadeSpeed = 3f;
    
    [Header("Player Reference")]
    [Tooltip("Reference to the player controller to freeze movement")]
    public DustBunnyController playerController;
    
    // Internal state
    private bool isPaperShowing = false;
    private bool waitingForInput = false;
    
    /// <summary>
    /// Initialize the singleton and hide the UI
    /// </summary>
    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Find player if not assigned
        if (playerController == null)
        {
            playerController = FindObjectOfType<DustBunnyController>();
        }
        
        // Hide UI at start
        if (backgroundGroup != null) backgroundGroup.alpha = 0;
        if (paperGroup != null) paperGroup.alpha = 0;
        
        // Disable interaction at start
        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = false;
        if (paperGroup != null) paperGroup.blocksRaycasts = false;
    }
    
    /// <summary>
    /// Check for input to dismiss the paper
    /// </summary>
    void Update()
    {
        // If we're showing the paper and waiting for input
        if (isPaperShowing && waitingForInput)
        {
            // Check for Space key or Jump button (same as jumping)
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                // Dismiss the paper
                StartCoroutine(HidePaper());
            }
        }
    }
    
    /// <summary>
    /// Show the paper overlay with the given text
    /// Called by PaperItem when player absorbs it
    /// </summary>
    /// <param name="text">The text to display on the paper</param>
    public void ShowPaper(string text)
    {
        // Don't show if already showing
        if (isPaperShowing)
        {
            Debug.LogWarning("Paper is already showing!");
            return;
        }
        
        StopAllCoroutines();
        StartCoroutine(DisplayPaper(text));
    }
    
    /// <summary>
    /// Coroutine that handles showing the paper with fade-in animation
    /// </summary>
    private IEnumerator DisplayPaper(string text)
    {
        isPaperShowing = true;
        waitingForInput = false;
        
        // Freeze player movement
        if (playerController != null)
        {
            playerController.enabled = false;
        }
        
        // Set the text content
        if (paperText != null)
        {
            paperText.text = text;
        }
        
        // Enable raycasting to block clicks
        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = true;
        if (paperGroup != null) paperGroup.blocksRaycasts = true;
        
        // Fade in the background
        while (backgroundGroup != null && backgroundGroup.alpha < 1f)
        {
            backgroundGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        
        // Fade in the paper
        while (paperGroup != null && paperGroup.alpha < 1f)
        {
            paperGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        
        // Small delay before accepting input (prevents instant dismissal)
        yield return new WaitForSeconds(0.3f);
        
        // Now we're ready to accept input
        waitingForInput = true;
        
        // Optional: Make instruction text blink
        if (instructionText != null)
        {
            StartCoroutine(BlinkInstruction());
        }
    }
    
    /// <summary>
    /// Coroutine that handles hiding the paper with fade-out animation
    /// </summary>
    private IEnumerator HidePaper()
    {
        waitingForInput = false;
        
        // Fade out the paper
        while (paperGroup != null && paperGroup.alpha > 0f)
        {
            paperGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }
        
        // Fade out the background
        while (backgroundGroup != null && backgroundGroup.alpha > 0f)
        {
            backgroundGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }
        
        // Disable raycasting
        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = false;
        if (paperGroup != null) paperGroup.blocksRaycasts = false;
        
        isPaperShowing = false;
        
        // Unfreeze player movement
        if (playerController != null)
        {
            playerController.enabled = true;
        }
    }
    
    /// <summary>
    /// Make the instruction text blink to draw attention
    /// </summary>
    private IEnumerator BlinkInstruction()
    {
        if (instructionText == null) yield break;
        
        while (waitingForInput)
        {
            // Fade out
            for (float t = 0; t < 1f; t += Time.deltaTime * 2f)
            {
                if (!waitingForInput) yield break;
                Color c = instructionText.color;
                c.a = Mathf.Lerp(1f, 0.3f, t);
                instructionText.color = c;
                yield return null;
            }
            
            // Fade in
            for (float t = 0; t < 1f; t += Time.deltaTime * 2f)
            {
                if (!waitingForInput) yield break;
                Color c = instructionText.color;
                c.a = Mathf.Lerp(0.3f, 1f, t);
                instructionText.color = c;
                yield return null;
            }
        }
        
        // Reset alpha when done
        Color final = instructionText.color;
        final.a = 1f;
        instructionText.color = final;
    }
    
    /// <summary>
    /// Check if paper is currently being displayed
    /// Useful for other scripts that need to know if player is reading
    /// </summary>
    public bool IsPaperShowing()
    {
        return isPaperShowing;
    }
    
    /// <summary>
    /// Manually dismiss the paper (useful for debugging or scripted events)
    /// </summary>
    public void DismissPaper()
    {
        if (isPaperShowing)
        {
            StartCoroutine(HidePaper());
        }
    }
}