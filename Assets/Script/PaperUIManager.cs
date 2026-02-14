using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// PaperUIManager.cs - UPDATED WITH MULTI-PIECE SUPPORT (NO BACKGROUND PREVIEW)
/// 
/// Now handles two types of paper display:
/// 
/// SINGLE-PIECE MODE (original):
/// - Shows one complete image
/// - Optional text overlay
/// 
/// MULTI-PIECE MODE (new):
/// - Shows only the collected piece sprites on a black background
/// - Uncollected areas remain as translucent black rectangles
/// - Progress indicator shows X/Y pieces collected
/// - No preview of the complete image until all pieces are collected
/// 
/// Setup:
/// 1. Keep existing single-piece UI elements
/// 2. Add new multi-piece container with:
///    - Black background panel
///    - Piece container (holds individual piece images)
///    - Progress text
/// </summary>
public class PaperUIManager : MonoBehaviour
{
    // Singleton pattern
    public static PaperUIManager Instance;
    
    [Header("Single-Piece UI References")]
    [Tooltip("The dark background that dims the screen")]
    public CanvasGroup backgroundGroup;
    
    [Tooltip("Container for single-piece papers")]
    public CanvasGroup singlePieceGroup;
    
    [Tooltip("The Image component for single-piece papers")]
    public Image singlePieceImage;
    
    [Tooltip("(Optional) Text for single-piece papers")]
    public TextMeshProUGUI singlePieceText;
    
    [Header("Multi-Piece UI References")]
    [Tooltip("Container for multi-piece puzzle display")]
    public CanvasGroup multiPieceGroup;
    
    [Tooltip("The black background panel (where uncollected pieces appear dark)")]
    public Image blackBackgroundPanel;
    
    [Tooltip("Container that holds individual piece images")]
    public RectTransform pieceContainer;
    
    [Tooltip("Prefab for displaying individual pieces (should be an Image component)")]
    public GameObject piecePrefab;
    
    [Tooltip("Text showing progress (e.g., '3/5 pieces collected')")]
    public TextMeshProUGUI progressText;
    
    [Header("Shared UI References")]
    [Tooltip("The instruction text ('Press SPACE to continue')")]
    public TextMeshProUGUI instructionText;
    
    [Header("Settings")]
    [Tooltip("Color of the black background (leave as black)")]
    public Color backgroundColor = Color.black;
    
    [Tooltip("How fast the paper fades in/out")]
    public float fadeSpeed = 3f;
    
    [Header("Player Reference")]
    [Tooltip("Reference to player controller to freeze movement")]
    public DustBunnyController playerController;
    
    // Internal state
    private bool isPaperShowing = false;
    private bool waitingForInput = false;
    private bool isMultiPieceMode = false;
    
    /// <summary>
    /// Initialize singleton and setup
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
        
        // Setup black background color
        if (blackBackgroundPanel != null)
        {
            blackBackgroundPanel.color = backgroundColor;
        }
        
        // Hide all UI at start
        if (backgroundGroup != null) backgroundGroup.alpha = 0;
        if (singlePieceGroup != null) singlePieceGroup.alpha = 0;
        if (multiPieceGroup != null) multiPieceGroup.alpha = 0;
        
        // Disable interaction at start
        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = false;
        if (singlePieceGroup != null) singlePieceGroup.blocksRaycasts = false;
        if (multiPieceGroup != null) multiPieceGroup.blocksRaycasts = false;
    }
    
    /// <summary>
    /// Check for input to dismiss paper
    /// </summary>
    void Update()
    {
        if (isPaperShowing && waitingForInput)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                StartCoroutine(HidePaper());
            }
        }
    }
    
    // ========== SINGLE-PIECE METHODS (ORIGINAL) ==========
    
    /// <summary>
    /// Show a single-piece paper (original functionality)
    /// </summary>
    public void ShowPaper(string text)
    {
        if (isPaperShowing) return;
        
        isMultiPieceMode = false;
        StopAllCoroutines();
        StartCoroutine(DisplaySinglePiece(text, null));
    }
    
    /// <summary>
    /// Show a single-piece paper with custom sprite
    /// </summary>
    public void ShowPaper(string text, Sprite customSprite)
    {
        if (isPaperShowing) return;
        
        isMultiPieceMode = false;
        StopAllCoroutines();
        StartCoroutine(DisplaySinglePiece(text, customSprite));
    }
    
    /// <summary>
    /// Display single-piece paper coroutine
    /// </summary>
    private IEnumerator DisplaySinglePiece(string text, Sprite sprite)
    {
        isPaperShowing = true;
        waitingForInput = false;
        
        // Freeze player
        if (playerController != null)
            playerController.enabled = false;
        
        // Set sprite
        if (sprite != null && singlePieceImage != null)
        {
            singlePieceImage.sprite = sprite;
        }
        
        // Set text
        if (singlePieceText != null && !string.IsNullOrEmpty(text))
        {
            singlePieceText.text = text;
            singlePieceText.gameObject.SetActive(true);
        }
        else if (singlePieceText != null)
        {
            singlePieceText.gameObject.SetActive(false);
        }
        
        // Enable raycasting
        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = true;
        if (singlePieceGroup != null) singlePieceGroup.blocksRaycasts = true;
        
        // Fade in background
        while (backgroundGroup != null && backgroundGroup.alpha < 1f)
        {
            backgroundGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        
        // Fade in paper
        while (singlePieceGroup != null && singlePieceGroup.alpha < 1f)
        {
            singlePieceGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        
        yield return new WaitForSeconds(0.3f);
        
        waitingForInput = true;
        
        if (instructionText != null)
            StartCoroutine(BlinkInstruction());
    }
    
    // ========== MULTI-PIECE METHODS (NEW) ==========
    
    /// <summary>
    /// Show a multi-piece paper puzzle with current progress
    /// </summary>
    /// <param name="paperData">The multi-piece paper data</param>
    /// <param name="collectedSprites">Array of collected piece sprites (null entries for uncollected)</param>
    public void ShowMultiPiecePaper(MultiPiecePaperData paperData, Sprite[] collectedSprites)
    {
        if (isPaperShowing) return;
        
        isMultiPieceMode = true;
        StopAllCoroutines();
        StartCoroutine(DisplayMultiPiece(paperData, collectedSprites));
    }
    
    /// <summary>
    /// Display multi-piece paper coroutine
    /// Shows only collected pieces on a black background
    /// </summary>
    private IEnumerator DisplayMultiPiece(MultiPiecePaperData paperData, Sprite[] collectedSprites)
    {
        isPaperShowing = true;
        waitingForInput = false;
        
        // Freeze player
        if (playerController != null)
            playerController.enabled = false;
        
        // Clear any existing piece images
        ClearPieceContainer();
        
        // Create piece images ONLY for each collected piece
        // Uncollected pieces will just be the black background showing through
        if (pieceContainer != null && piecePrefab != null)
        {
            for (int i = 0; i < collectedSprites.Length; i++)
            {
                if (collectedSprites[i] != null)
                {
                    // This piece has been collected - display it
                    CreatePieceImage(collectedSprites[i]);
                }
                // If collectedSprites[i] is null, we don't create anything
                // The black background will show in that spot
            }
        }
        
        // Update progress text
        if (progressText != null)
        {
            int collected = paperData.GetCollectedCount();
            int total = paperData.totalPieces;
            progressText.text = $"{collected}/{total} pieces collected";
            
            // Add completion message if all pieces found
            if (paperData.IsComplete())
            {
                progressText.text += "\n<color=yellow>COMPLETE!</color>";
            }
        }
        
        // Enable raycasting
        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = true;
        if (multiPieceGroup != null) multiPieceGroup.blocksRaycasts = true;
        
        // Fade in background
        while (backgroundGroup != null && backgroundGroup.alpha < 1f)
        {
            backgroundGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        
        // Fade in multi-piece display
        while (multiPieceGroup != null && multiPieceGroup.alpha < 1f)
        {
            multiPieceGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        
        yield return new WaitForSeconds(0.3f);
        
        waitingForInput = true;
        
waitingForInput = true;

// Show and blink the instruction text

// Show and blink the instruction text
if (instructionText != null)
{
    instructionText.gameObject.SetActive(true);
    StartCoroutine(BlinkInstruction());
}
    }
    
    /// <summary>
    /// Clear all piece images from the container
    /// </summary>
    private void ClearPieceContainer()
    {
        if (pieceContainer == null) return;
        
        // Destroy all children
        foreach (Transform child in pieceContainer)
        {
            Destroy(child.gameObject);
        }
    }
    
    /// <summary>
    /// Create an image for a collected piece
    /// The piece sprite should already be positioned correctly in the image
    /// (your fragment PNGs have the piece in the right spot with transparency elsewhere)
    /// </summary>
    private void CreatePieceImage(Sprite pieceSprite)
    {
        if (piecePrefab == null || pieceContainer == null) return;
        
        // Instantiate the piece
        GameObject pieceObj = Instantiate(piecePrefab, pieceContainer);
        
        // Get the Image component
        Image pieceImage = pieceObj.GetComponent<Image>();
        if (pieceImage != null)
        {
            pieceImage.sprite = pieceSprite;
            pieceImage.preserveAspect = true;
            
            // Make sure it fills the container
            // Since your fragment PNGs already have the piece positioned correctly,
            // we just need to make the image fill the entire display area
            RectTransform rectTransform = pieceObj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Stretch to fill parent (the piece container)
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }
    
    // ========== SHARED METHODS ==========
    
    /// <summary>
    /// Hide the paper (works for both modes)
    /// </summary>
    private IEnumerator HidePaper()
    {
        waitingForInput = false;
        
        // Fade out the appropriate group
        CanvasGroup activeGroup = isMultiPieceMode ? multiPieceGroup : singlePieceGroup;
        
        while (activeGroup != null && activeGroup.alpha > 0f)
        {
            activeGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }
        
        // Fade out background
        while (backgroundGroup != null && backgroundGroup.alpha > 0f)
        {
            backgroundGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }
        
        // Disable raycasting
        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = false;
        if (singlePieceGroup != null) singlePieceGroup.blocksRaycasts = false;
        if (multiPieceGroup != null) multiPieceGroup.blocksRaycasts = false;
        
isPaperShowing = false;

// Hide the instruction text
if (instructionText != null)
{
    instructionText.gameObject.SetActive(false);
}

// Unfreeze player
if (playerController != null)
    playerController.enabled = true;
    }
    
    /// <summary>
    /// Blink the instruction text
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
        
        Color final = instructionText.color;
        final.a = 1f;
        instructionText.color = final;
    }
    
    /// <summary>
    /// Check if paper is showing
    /// </summary>
    public bool IsPaperShowing()
    {
        return isPaperShowing;
    }
    
    /// <summary>
    /// Manually dismiss paper
    /// </summary>
    public void DismissPaper()
    {
        if (isPaperShowing)
            StartCoroutine(HidePaper());
    }
}