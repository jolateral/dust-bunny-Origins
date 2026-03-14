using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// DiaryUIManager.cs
/// 
/// Manages the full-screen diary overlay UI.
/// Works identically to PaperUIManager but is dedicated to the diary so the
/// two systems don't conflict (e.g., a paper popup won't accidentally override
/// the diary mid-read).
/// 
/// UI HIERARCHY TO BUILD IN UNITY:
/// 
///   DiaryCanvas  (Canvas, CanvasScaler, GraphicRaycaster — Render Mode: Screen Space Overlay)
///   └── DiaryBackground          (Image, black/dark color — CanvasGroup on this)
///       └── DiaryPanel           (Image or just a RectTransform — CanvasGroup on this)
///           ├── DiaryImage       (Image component — shows the diary sprite)
///           ├── DiaryText        (TextMeshProUGUI — optional text on top of image)
///           └── InstructionText  (TextMeshProUGUI — "Press SPACE to close")
/// 
/// OR reuse your existing PaperCanvas by adding a new sibling panel called "DiaryPanel".
/// 
/// SETUP STEPS:
/// 1. Create the UI hierarchy above (or extend PaperCanvas)
/// 2. Add this DiaryUIManager script to a GameObject (e.g., the DiaryCanvas root)
/// 3. Assign all the serialized references in the Inspector
/// 4. Assign the playerController reference (or it auto-finds DustBunnyController)
/// 5. The DiaryItem script calls DiaryUIManager.Instance.ShowDiary() automatically
/// </summary>
public class DiaryUIManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Singleton
    // -----------------------------------------------------------------------

    /// <summary>
    /// Global access point. DiaryItem calls DiaryUIManager.Instance.ShowDiary().
    /// </summary>
    public static DiaryUIManager Instance;

    // -----------------------------------------------------------------------
    // Inspector References
    // -----------------------------------------------------------------------

    [Header("Background Overlay")]
    [Tooltip("CanvasGroup on the dark background panel that dims the rest of the screen.")]
    public CanvasGroup backgroundGroup;

    [Header("Diary Panel")]
    [Tooltip("CanvasGroup on the diary panel that contains the image and text.")]
    public CanvasGroup diaryPanelGroup;

    [Tooltip("The Image component that displays the diary sprite.")]
    public Image diaryImage;

    [Tooltip("(Optional) TextMeshProUGUI for diary text shown over the image. Leave empty for image-only.")]
    public TextMeshProUGUI diaryText;

    [Header("Instruction Text")]
    [Tooltip("TextMeshProUGUI that blinks 'Press SPACE to close'.")]
    public TextMeshProUGUI instructionText;

    [Header("Settings")]
    [Tooltip("How fast the diary fades in and out.")]
    public float fadeSpeed = 3f;

    [Tooltip("Background overlay color (default black, semi-transparent).")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 1f);

    [Header("Player Reference")]
    [Tooltip("Drag the Player GameObject here, or leave empty to auto-find.")]
    public DustBunnyController playerController;

    public bool diaryShown = false;

    // -----------------------------------------------------------------------
    // Runtime State
    // -----------------------------------------------------------------------

    /// <summary>True while the diary overlay is on screen.</summary>
    private bool isDiaryShowing = false;

    /// <summary>True when the diary is fully faded in and waiting for player to press SPACE.</summary>
    private bool waitingForInput = false;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    void Awake()
    {
        // --- Singleton setup ---
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Auto-find the player controller if not assigned
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<DustBunnyController>();
            if (playerController == null)
                Debug.LogWarning("[DiaryUIManager] No DustBunnyController found in scene!");
        }

        // Apply background color
        if (diaryImage != null && backgroundGroup != null)
        {
            // The background panel's Image color is set separately via its own Image component.
            // We just make sure CanvasGroups start hidden.
        }

        // --- Hide everything at startup ---
        SetGroupAlpha(backgroundGroup, 0f, false);
        SetGroupAlpha(diaryPanelGroup, 0f, false);

        if (instructionText != null)
            instructionText.gameObject.SetActive(false);
    }

    void Update()
    {
        // --- Listen for dismiss input ---
        if (isDiaryShowing && waitingForInput)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                StartCoroutine(HideDiary());
            }
        }
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Show the diary overlay with the given text and sprite.
    /// Called automatically by DiaryItem when the player unlocks the diary.
    /// </summary>
    /// <param name="text">Optional text to display. Pass empty string for image-only.</param>
    /// <param name="sprite">The diary page image to display full-screen.</param>
    public void ShowDiary(string text, Sprite sprite)
    {
        // Don't double-open if already showing
        if (isDiaryShowing) return;
        diaryShown = true;

        StopAllCoroutines();
        StartCoroutine(DisplayDiary(text, sprite));
    }

    /// <summary>
    /// Programmatically dismiss the diary (e.g., from a cutscene manager).
    /// </summary>
    public void DismissDiary()
    {
        if (isDiaryShowing)
            StartCoroutine(HideDiary());
    }

    /// <summary>Whether the diary is currently on screen.</summary>
    public bool IsDiaryShowing() => isDiaryShowing;

    // -----------------------------------------------------------------------
    // Private Coroutines
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fade in the diary overlay, then wait for the player to press SPACE.
    /// </summary>
    private IEnumerator DisplayDiary(string text, Sprite sprite)
    {
        isDiaryShowing = true;
        waitingForInput = false;

        // --- 1. Freeze the player so they can't run around while reading ---
        if (playerController != null)
            playerController.enabled = false;

        // --- 2. Set diary content ---

        // Set the diary image
        if (diaryImage != null && sprite != null)
        {
            diaryImage.sprite = sprite;
            diaryImage.preserveAspect = true;   // Don't stretch the image
        }

        // Set the text overlay (hidden if empty)
        if (diaryText != null)
        {
            if (!string.IsNullOrEmpty(text))
            {
                diaryText.text = text;
                diaryText.gameObject.SetActive(true);
            }
            else
            {
                diaryText.gameObject.SetActive(false);
            }
        }

        // --- 3. Enable raycasting so nothing behind the panel gets clicked ---
        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = true;
        if (diaryPanelGroup != null) diaryPanelGroup.blocksRaycasts = true;

        // --- 4. Fade in the dark background ---
        yield return StartCoroutine(FadeGroup(backgroundGroup, 0f, 1f));

        // --- 5. Fade in the diary panel ---
        yield return StartCoroutine(FadeGroup(diaryPanelGroup, 0f, 1f));

        // Small pause so the player registers the screen before we accept input
        yield return new WaitForSeconds(0.3f);

        // --- 6. Show the "Press SPACE" instruction and start blinking ---
        waitingForInput = true;

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            StartCoroutine(BlinkInstruction());
        }
    }

    /// <summary>
    /// Fade out the diary overlay and unfreeze the player.
    /// </summary>
    private IEnumerator HideDiary()
    {
        waitingForInput = false;

        // --- 1. Fade out the diary panel ---
        yield return StartCoroutine(FadeGroup(diaryPanelGroup, 1f, 0f));

        // --- 2. Fade out the background ---
        yield return StartCoroutine(FadeGroup(backgroundGroup, 1f, 0f));

        // --- 3. Disable raycasting so UI doesn't swallow input ---
        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = false;
        if (diaryPanelGroup != null) diaryPanelGroup.blocksRaycasts = false;

        // --- 4. Hide instruction text ---
        if (instructionText != null)
            instructionText.gameObject.SetActive(false);

        // --- 5. Unfreeze the player and briefly suppress jump so the same
        // Space/confirm press used to close the diary doesn't immediately trigger a jump.
        if (playerController != null)
        {
            playerController.SuppressJumpForSeconds(0.2f);
            playerController.enabled = true;
        }

        isDiaryShowing = false;
    }

    // -----------------------------------------------------------------------
    // Utility Coroutines
    // -----------------------------------------------------------------------

    /// <summary>
    /// Smoothly fade a CanvasGroup from startAlpha to endAlpha.
    /// </summary>
    private IEnumerator FadeGroup(CanvasGroup group, float startAlpha, float endAlpha)
    {
        if (group == null) yield break;

        group.alpha = startAlpha;

        // Determine direction and loop until target reached
        while (!Mathf.Approximately(group.alpha, endAlpha))
        {
            group.alpha = Mathf.MoveTowards(group.alpha, endAlpha, Time.deltaTime * fadeSpeed);
            yield return null;
        }

        group.alpha = endAlpha;
    }

    /// <summary>
    /// Set a CanvasGroup's alpha immediately (no fade).
    /// Also controls interactivity.
    /// </summary>
    private void SetGroupAlpha(CanvasGroup group, float alpha, bool blocksRaycasts)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.blocksRaycasts = blocksRaycasts;
        group.interactable = blocksRaycasts;
    }

    /// <summary>
    /// Gently pulses the instruction text alpha between full and 30%
    /// so the player notices it.
    /// </summary>
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

        // Restore full opacity when done
        Color final = instructionText.color;
        final.a = 1f;
        instructionText.color = final;
    }
}