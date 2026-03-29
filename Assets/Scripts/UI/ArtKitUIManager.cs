using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// ArtKitUIManager.cs  (UPDATED — ArtKitClosed event added)
///
/// Handles a full-screen Art Kit popup.
/// Similar to DiaryUIManager, but without any key/unlock logic.
/// The Art Kit opens immediately when the player collides with it,
/// and closes when the player presses Space or controller X.
///
/// CHANGE FROM ORIGINAL:
///   Added the public event 'ArtKitClosed' (line marked NEW below).
///   ArtKitItem subscribes to this so it knows exactly when the popup
///   is dismissed and can then spawn the floating badge + set the unlock flag.
///   Every other line is unchanged from your original script.
/// </summary>
public class ArtKitUIManager : MonoBehaviour
{
    public static ArtKitUIManager Instance;

    // -----------------------------------------------------------------------
    // NEW: Closed Callback Event
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fired the moment the Art Kit UI finishes hiding.
    /// ArtKitItem subscribes to this to know when to spawn the floating badge
    /// and set ArtKitItem.HasViewedArtKit = true.
    ///
    /// Usage:
    ///   ArtKitUIManager.Instance.ArtKitClosed += MyMethod;   // subscribe
    ///   ArtKitUIManager.Instance.ArtKitClosed -= MyMethod;   // unsubscribe
    /// </summary>
    public event System.Action ArtKitClosed;

    // -----------------------------------------------------------------------
    // Inspector References (unchanged from original)
    // -----------------------------------------------------------------------

    [Header("Background Overlay")]
    [Tooltip("CanvasGroup on the dark background panel that dims the rest of the screen.")]
    public CanvasGroup backgroundGroup;

    [Header("Art Kit Panel")]
    [Tooltip("CanvasGroup on the panel that contains the image and optional text.")]
    public CanvasGroup artKitPanelGroup;

    [Tooltip("The Image component that displays the Art Kit sprite.")]
    public Image artKitImage;

    [Tooltip("(Optional) TextMeshProUGUI for text shown over the image. Leave empty for image-only.")]
    public TextMeshProUGUI artKitText;

    [Header("Instruction Image")]
    [Tooltip("UI Image that blinks to show the close instruction (e.g. 'Press Space to continue').")]
    public Image instructionImage;

    [Header("Settings")]
    [Tooltip("How fast the UI fades in and out.")]
    public float fadeSpeed = 3f;

    [Tooltip("Background overlay color.")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 1f);

    [Header("Player Reference")]
    [Tooltip("Drag the Player GameObject here, or leave empty to auto-find.")]
    public DustBunnyController playerController;

    [Header("SFX")]
    public AK.Wwise.Event uiNext;

    // -----------------------------------------------------------------------
    // Private State (unchanged from original)
    // -----------------------------------------------------------------------

    private bool isArtKitShowing = false;
    private bool waitingForInput = false;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    private void Awake()
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

        // Auto-find the player if not assigned in the Inspector
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<DustBunnyController>();
            if (playerController == null)
                Debug.LogWarning("[ArtKitUIManager] No DustBunnyController found in scene!");
        }

        // Start with all UI invisible and non-interactive
        SetGroupAlpha(backgroundGroup, 0f, false);
        SetGroupAlpha(artKitPanelGroup, 0f, false);

        if (instructionImage != null)
            instructionImage.gameObject.SetActive(false);

        // Apply background tint
        if (backgroundGroup != null)
        {
            Image bgImage = backgroundGroup.GetComponent<Image>();
            if (bgImage != null)
                bgImage.color = backgroundColor;
        }
    }

    private void Update()
    {
        // Poll for the dismiss input while the popup is open and ready
        if (isArtKitShowing && waitingForInput)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                if (uiNext != null) uiNext.Post(gameObject);
                StartCoroutine(HideArtKit());
            }
        }
    }

    // -----------------------------------------------------------------------
    // Public API (unchanged from original)
    // -----------------------------------------------------------------------

    /// <summary>Shows the full-screen art kit popup with the given text and sprite.</summary>
    public void ShowArtKit(string text, Sprite sprite)
    {
        if (isArtKitShowing) return;

        StopAllCoroutines();
        StartCoroutine(DisplayArtKit(text, sprite));
    }

    /// <summary>Hides the art kit popup from external code (e.g. a skip button).</summary>
    public void DismissArtKit()
    {
        if (isArtKitShowing)
            StartCoroutine(HideArtKit());
    }

    /// <summary>Returns true while the popup is visible.</summary>
    public bool IsArtKitShowing() => isArtKitShowing;

    // -----------------------------------------------------------------------
    // Private Coroutines (unchanged from original, except HideArtKit fires the new event)
    // -----------------------------------------------------------------------

    private IEnumerator DisplayArtKit(string text, Sprite sprite)
    {
        // Switch Wwise state so music reacts to the popup
        AkUnitySoundEngine.SetState("player_state", "memory");

        isArtKitShowing = true;
        waitingForInput = false;

        // Disable player movement while the popup is open
        if (playerController != null)
            playerController.enabled = false;

        // Load content into the UI elements
        if (artKitImage != null && sprite != null)
        {
            artKitImage.sprite = sprite;
            artKitImage.preserveAspect = true;
        }

        if (artKitText != null)
        {
            if (!string.IsNullOrEmpty(text))
            {
                artKitText.text = text;
                artKitText.gameObject.SetActive(true);
            }
            else
            {
                artKitText.gameObject.SetActive(false);
            }
        }

        // Enable raycast blocking so clicks don't pass through the overlay
        if (backgroundGroup != null)
        {
            backgroundGroup.blocksRaycasts = true;
            backgroundGroup.interactable = true;
        }

        if (artKitPanelGroup != null)
        {
            artKitPanelGroup.blocksRaycasts = true;
            artKitPanelGroup.interactable = true;
        }

        // Fade in background, then the panel
        yield return StartCoroutine(FadeGroup(backgroundGroup, 0f, 1f));
        yield return StartCoroutine(FadeGroup(artKitPanelGroup, 0f, 1f));

        // Short pause before accepting input (prevents instant dismiss)
        yield return new WaitForSeconds(0.3f);

        waitingForInput = true;

        // Show and blink the "press to continue" instruction image
        if (instructionImage != null)
        {
            instructionImage.gameObject.SetActive(true);

            Color c = instructionImage.color;
            c.a = 1f;
            instructionImage.color = c;

            StartCoroutine(BlinkInstructionImage());
        }
    }

    private IEnumerator HideArtKit()
    {
        // Restore Wwise music state
        AkUnitySoundEngine.SetState("player_state", "None");
        AkUnitySoundEngine.SetState("mus_zone2", "mus_zone2_2");
        Debug.Log("[ArtKitUIManager] Music state set: mus_zone2 → mus_zone2_2");

        waitingForInput = false;

        // Fade out the panel, then the background overlay
        yield return StartCoroutine(FadeGroup(artKitPanelGroup, 1f, 0f));
        yield return StartCoroutine(FadeGroup(backgroundGroup, 1f, 0f));

        // Restore non-interactive state on canvas groups
        if (backgroundGroup != null)
        {
            backgroundGroup.blocksRaycasts = false;
            backgroundGroup.interactable = false;
        }

        if (artKitPanelGroup != null)
        {
            artKitPanelGroup.blocksRaycasts = false;
            artKitPanelGroup.interactable = false;
        }

        // Hide the instruction image
        if (instructionImage != null)
            instructionImage.gameObject.SetActive(false);

        // Re-enable the player, with a tiny grace period so Space doesn't double-fire a jump
        if (playerController != null)
        {
            playerController.SuppressJumpForSeconds(0.5f);
            playerController.enabled = true;
        }

        isArtKitShowing = false;

        // -----------------------------------------------------------------------
        // NEW: Fire the closed event so ArtKitItem (and any other subscribers)
        //      know the popup has fully hidden. This is the only addition to
        //      the original HideArtKit coroutine.
        // -----------------------------------------------------------------------
        ArtKitClosed?.Invoke();
    }

    // -----------------------------------------------------------------------
    // Utility Helpers (unchanged from original)
    // -----------------------------------------------------------------------

    /// <summary>Smoothly moves a CanvasGroup's alpha from startAlpha to endAlpha.</summary>
    private IEnumerator FadeGroup(CanvasGroup group, float startAlpha, float endAlpha)
    {
        if (group == null) yield break;

        group.alpha = startAlpha;

        while (!Mathf.Approximately(group.alpha, endAlpha))
        {
            group.alpha = Mathf.MoveTowards(group.alpha, endAlpha, Time.deltaTime * fadeSpeed);
            yield return null;
        }

        group.alpha = endAlpha;
    }

    /// <summary>Sets a CanvasGroup's alpha and raycast properties in one call.</summary>
    private void SetGroupAlpha(CanvasGroup group, float alpha, bool blocksRaycasts)
    {
        if (group == null) return;

        group.alpha = alpha;
        group.blocksRaycasts = blocksRaycasts;
        group.interactable = blocksRaycasts;
    }

    /// <summary>Pulses the instruction image alpha between 1 and 0.3 while waiting for input.</summary>
    private IEnumerator BlinkInstructionImage()
    {
        if (instructionImage == null) yield break;

        while (waitingForInput)
        {
            // Fade out
            for (float t = 0; t < 1f; t += Time.deltaTime * 2f)
            {
                if (!waitingForInput) yield break;
                Color c = instructionImage.color;
                c.a = Mathf.Lerp(1f, 0.3f, t);
                instructionImage.color = c;
                yield return null;
            }

            // Fade back in
            for (float t = 0; t < 1f; t += Time.deltaTime * 2f)
            {
                if (!waitingForInput) yield break;
                Color c = instructionImage.color;
                c.a = Mathf.Lerp(0.3f, 1f, t);
                instructionImage.color = c;
                yield return null;
            }
        }

        // Snap back to full opacity when blinking ends
        Color final = instructionImage.color;
        final.a = 1f;
        instructionImage.color = final;
    }
}