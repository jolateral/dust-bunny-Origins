using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// PaperUIManager.cs
///
/// Manages full-screen paper overlay UI for both single-piece and multi-piece papers.
///
/// Coordinates with ObjectiveUI so the fragment progress panel:
/// - Is hidden BEFORE the full-screen paper view opens (never overlaps)
/// - Slides back in AFTER the player dismisses the paper view
///
/// SINGLE-PIECE MODE:
/// - Shows one complete image with optional text overlay
///
/// MULTI-PIECE MODE:
/// - Shows only collected piece sprites on a black background
/// - Uncollected areas remain dark
/// - Progress indicator shows X/Y pieces collected
///
/// FEATURES:
/// - Freezes player movement while viewing
/// - Fade in/out animations
/// - Instruction text only shows when paper is active
/// - Press Space or joystick button to dismiss
/// </summary>
public class PaperUIManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Singleton
    // -----------------------------------------------------------------------

    public static PaperUIManager Instance;

    // -----------------------------------------------------------------------
    // Inspector References
    // -----------------------------------------------------------------------

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
    [Tooltip("The instruction text ('Press X to continue')")]
    public TextMeshProUGUI instructionText;

    [Header("Settings")]
    [Tooltip("Color of the black background (leave as black)")]
    public Color backgroundColor = Color.black;

    [Tooltip("How fast the paper fades in/out")]
    public float fadeSpeed = 3f;

    [Header("Player Reference")]
    [Tooltip("Reference to player controller to freeze movement")]
    public DustBunnyController playerController;

    [Header("Audio")]
    public AK.Wwise.Event uiSelect;

    // -----------------------------------------------------------------------
    // Internal State
    // -----------------------------------------------------------------------

    private bool isPaperShowing = false;
    private bool waitingForInput = false;
    private bool isMultiPieceMode = false;

    /// <summary>The currently active multi-piece paper data. Read by ObjectiveUI.SetObjective().</summary>
    private MultiPiecePaperData currentPaperData;
    public MultiPiecePaperData CurrentPaperData => currentPaperData;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

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

        if (playerController == null)
            playerController = FindObjectOfType<DustBunnyController>();

        if (blackBackgroundPanel != null)
            blackBackgroundPanel.color = backgroundColor;

        // Hide all UI at start
        if (backgroundGroup != null) backgroundGroup.alpha = 0;
        if (singlePieceGroup != null) singlePieceGroup.alpha = 0;
        if (multiPieceGroup != null) multiPieceGroup.alpha = 0;

        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = false;
        if (singlePieceGroup != null) singlePieceGroup.blocksRaycasts = false;
        if (multiPieceGroup != null) multiPieceGroup.blocksRaycasts = false;

        if (instructionText != null)
            instructionText.gameObject.SetActive(false);
    }

    void Start()
    {
        ResetUI();
    }

    void ResetUI()
    {
        isPaperShowing = false;
        waitingForInput = false;
        isMultiPieceMode = false;
        currentPaperData = null;

        if (backgroundGroup != null) backgroundGroup.alpha = 0;
        if (singlePieceGroup != null) singlePieceGroup.alpha = 0;
        if (multiPieceGroup != null) multiPieceGroup.alpha = 0;

        if (instructionText != null)
            instructionText.gameObject.SetActive(false);
    }

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

    // -----------------------------------------------------------------------
    // Single-Piece Methods
    // -----------------------------------------------------------------------

    /// <summary>Show a single-piece paper with text only.</summary>
    public void ShowPaper(string text)
    {
        if (isPaperShowing) return;

        // Hide the progress panel immediately before anything fades in
        if (ObjectiveUI.Instance != null)
            ObjectiveUI.Instance.HideForPaper();

        isMultiPieceMode = false;
        StopAllCoroutines();
        StartCoroutine(DisplaySinglePiece(text, null));
    }

    /// <summary>Show a single-piece paper with text and a custom sprite.</summary>
    public void ShowPaper(string text, Sprite customSprite)
    {
        if (isPaperShowing) return;

        // Hide the progress panel immediately before anything fades in
        if (ObjectiveUI.Instance != null)
            ObjectiveUI.Instance.HideForPaper();

        isMultiPieceMode = false;
        StopAllCoroutines();
        StartCoroutine(DisplaySinglePiece(text, customSprite));
    }

    private IEnumerator DisplaySinglePiece(string text, Sprite sprite)
    {
        isPaperShowing = true;
        waitingForInput = false;

        // Freeze player
        if (playerController != null)
            playerController.enabled = false;

        // Set content
        if (sprite != null && singlePieceImage != null)
            singlePieceImage.sprite = sprite;

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
        {
            instructionText.gameObject.SetActive(true);
            StartCoroutine(BlinkInstruction());
        }
    }

    // -----------------------------------------------------------------------
    // Multi-Piece Methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Show a multi-piece paper puzzle with current progress.
    /// Hides the ObjectiveUI panel FIRST so it never overlaps the full-screen view.
    /// </summary>
    public void ShowMultiPiecePaper(MultiPiecePaperData paperData, Sprite[] collectedSprites)
    {
        if (isPaperShowing) return;

        currentPaperData = paperData;

        // --- Order matters here ---
        // 1. Hide the progress panel FIRST before it has a chance to slide in
        if (ObjectiveUI.Instance != null)
            ObjectiveUI.Instance.HideForPaper();

        // 2. Sync the fragment data in the background (no visual change since panel is hidden)
        if (ObjectiveUI.Instance != null)
            ObjectiveUI.Instance.SetObjective();

        isMultiPieceMode = true;
        StopAllCoroutines();
        StartCoroutine(DisplayMultiPiece(paperData, collectedSprites));
    }

    private IEnumerator DisplayMultiPiece(MultiPiecePaperData paperData, Sprite[] collectedSprites)
    {
        isPaperShowing = true;
        waitingForInput = false;

        // Freeze player
        if (playerController != null)
            playerController.enabled = false;

        // Rebuild the piece display
        ClearPieceContainer();

        if (pieceContainer != null && piecePrefab != null)
        {
            for (int i = 0; i < collectedSprites.Length; i++)
            {
                if (collectedSprites[i] != null)
                    CreatePieceImage(collectedSprites[i]);
            }
        }

        // Update progress text
        if (progressText != null)
        {
            int collected = paperData.GetCollectedCount();
            int total = paperData.totalPieces;
            progressText.text = $"{collected}/{total} pieces collected";

            if (paperData.IsComplete())
                progressText.text += "\n<color=yellow>COMPLETE!</color>";
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

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            StartCoroutine(BlinkInstruction());
        }
    }

    /// <summary>Remove all dynamically created piece images from the container.</summary>
    private void ClearPieceContainer()
    {
        if (pieceContainer == null) return;
        foreach (Transform child in pieceContainer)
            Destroy(child.gameObject);
    }

    /// <summary>Instantiate and configure a piece image inside the container.</summary>
    private void CreatePieceImage(Sprite pieceSprite)
    {
        if (piecePrefab == null || pieceContainer == null) return;

        GameObject pieceObj = Instantiate(piecePrefab, pieceContainer);
        Image pieceImage = pieceObj.GetComponent<Image>();

        if (pieceImage != null)
        {
            pieceImage.sprite = pieceSprite;
            pieceImage.preserveAspect = true;

            RectTransform rectTransform = pieceObj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Stretch to fill the container so fragment PNGs sit in the correct position
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Shared Methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fade out the paper UI, unfreeze the player, then tell ObjectiveUI
    /// to slide the progress panel back in now that the view is closed.
    /// </summary>
    private IEnumerator HidePaper()
    {
        waitingForInput = false;

        // Fade out the active panel
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

        // Hide instruction text
        if (instructionText != null)
            instructionText.gameObject.SetActive(false);

        // Resume audio
        AkUnitySoundEngine.SetState("player_state", "None");

        // Unfreeze player (suppress jump briefly so Space used to close doesn't jump)
        if (playerController != null)
        {
            playerController.SuppressJumpForSeconds(0.2f);
            playerController.enabled = true;
        }

        // Paper is fully closed — now slide the progress panel back in
        if (ObjectiveUI.Instance != null)
            ObjectiveUI.Instance.ShowAfterPaper();
    }

    /// <summary>Blink the instruction text while waiting for player input.</summary>
    private IEnumerator BlinkInstruction()
    {
        if (instructionText == null) yield break;

        while (waitingForInput)
        {
            // Fade out to 30%
            for (float t = 0; t < 1f; t += Time.deltaTime * 2f)
            {
                if (!waitingForInput) yield break;
                Color c = instructionText.color;
                c.a = Mathf.Lerp(1f, 0.3f, t);
                instructionText.color = c;
                yield return null;
            }

            // Fade back to 100%
            for (float t = 0; t < 1f; t += Time.deltaTime * 2f)
            {
                if (!waitingForInput) yield break;
                Color c = instructionText.color;
                c.a = Mathf.Lerp(0.3f, 1f, t);
                instructionText.color = c;
                yield return null;
            }
        }

        // Restore full opacity when done blinking
        Color final = instructionText.color;
        final.a = 1f;
        instructionText.color = final;
    }

    // -----------------------------------------------------------------------
    // Public Utilities
    // -----------------------------------------------------------------------

    /// <summary>Returns true while the paper overlay is on screen.</summary>
    public bool IsPaperShowing() => isPaperShowing;

    /// <summary>Programmatically dismiss the paper (e.g. from a cutscene).</summary>
    public void DismissPaper()
    {
        if (isPaperShowing)
        {
            uiSelect.Post(gameObject);
            StartCoroutine(HidePaper());
        }
    }
}