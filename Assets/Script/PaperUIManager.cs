using UnityEngine;
using UnityEngine.UI; // Added for Image component
using TMPro;
using System.Collections;

/// <summary>
/// PaperUIManager.cs - IMAGE VERSION
/// 
/// Manages the full-screen paper overlay UI that appears when the player
/// reads a paper item. This script:
/// - Shows a PNG image instead of a white rectangle
/// - Optionally shows text overlaid on the image
/// - Freezes player movement while paper is showing
/// - Waits for player to press a button (Space/Jump) to dismiss
/// - Handles fade in/out animations
/// 
/// Setup:
/// 1. Create a Canvas in your scene (Screen Space - Overlay)
/// 2. Add this script to a GameObject
/// 3. Create UI elements and assign them in the inspector:
///    - Background Panel (semi-transparent black)
///    - Paper Image (Image component instead of Panel)
///    - (Optional) Text element (TextMeshProUGUI for content)
///    - Instruction text ("Press SPACE to continue")
/// 4. Assign your PNG image to the paperImage component
/// </summary>
public class PaperUIManager : MonoBehaviour
{
    // Singleton pattern (same as MemoryUIManager)
    public static PaperUIManager Instance;
    
    [Header("UI References")]
    [Tooltip("The dark background that dims the screen")]
    public CanvasGroup backgroundGroup;
    
    [Tooltip("The CanvasGroup that contains the paper image (for fading)")]
    public CanvasGroup paperGroup;
    
    [Tooltip("The Image component that displays the PNG paper")]
    public Image paperImage;
    
    [Tooltip("(Optional) The text component that displays content on top of the image")]
    public TextMeshProUGUI paperText;
    
    [Tooltip("The instruction text (e.g., 'Press SPACE to continue')")]
    public TextMeshProUGUI instructionText;
    
    [Header("Image Settings")]
    [Tooltip("The sprite/texture to display as the paper (assign your PNG here)")]
    public Sprite paperSprite;
    
    [Tooltip("Should the image maintain its aspect ratio?")]
    public bool preserveAspect = true;
    
    [Tooltip("How should the image fill its container?")]
    public Image.Type imageType = Image.Type.Simple;
    
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
        
        // Configure the paper image component
        if (paperImage != null)
        {
            paperImage.preserveAspect = preserveAspect;
            paperImage.type = imageType;
            
            // Set the sprite if one was assigned in the inspector
            if (paperSprite != null)
            {
                paperImage.sprite = paperSprite;
            }
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
    /// Show the paper overlay with the given text (optional)
    /// Called by PaperItem when player absorbs it
    /// </summary>
    /// <param name="text">The text to display on the paper (can be empty)</param>
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
    /// Show the paper overlay with a custom image
    /// Use this if different papers should show different images
    /// </summary>
    /// <param name="text">The text to display (can be empty)</param>
    /// <param name="customSprite">A custom sprite to display instead of the default</param>
    public void ShowPaper(string text, Sprite customSprite)
    {
        // Don't show if already showing
        if (isPaperShowing)
        {
            Debug.LogWarning("Paper is already showing!");
            return;
        }
        
        // Set the custom sprite if provided
        if (customSprite != null && paperImage != null)
        {
            paperImage.sprite = customSprite;
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
        
        // Set the text content (if text component exists and text is provided)
        if (paperText != null && !string.IsNullOrEmpty(text))
        {
            paperText.text = text;
            paperText.gameObject.SetActive(true);
        }
        else if (paperText != null)
        {
            // Hide text if none provided
            paperText.gameObject.SetActive(false);
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
        
        // Fade in the paper image
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