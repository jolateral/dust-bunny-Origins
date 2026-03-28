// =============================================================================
// DiaryUIManager.cs
// -----------------------------------------------------------------------------
// Manages the full-screen diary overlay UI.
//
// CHANGES FROM ORIGINAL:
//   - Added public ResetState() method so FadeSequenceManager can reset it
//     when the Level scene reloads.
//   - ResetState() stops all coroutines and re-enables the player controller
//     to prevent soft-locks if the scene reloads mid-diary-display.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

public class DiaryUIManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------

    public static DiaryUIManager Instance;

    // -------------------------------------------------------------------------
    // Inspector References
    // -------------------------------------------------------------------------

    [Header("Background Overlay")]
    [Tooltip("CanvasGroup on the dark background panel that dims the rest of the screen.")]
    public CanvasGroup backgroundGroup;

    [Header("Diary Panel")]
    [Tooltip("CanvasGroup on the diary panel that contains the image and text.")]
    public CanvasGroup diaryPanelGroup;

    [Tooltip("The Image component that displays the diary sprite.")]
    public Image diaryImage;

    [Tooltip("(Optional) TextMeshProUGUI for diary text shown over the image.")]
    public TextMeshProUGUI diaryText;

    [Header("Instruction Image")]
    [Tooltip("UI Image that blinks to show the close instruction.")]
    public Image instructionImage;

    [Header("Settings")]
    [Tooltip("How fast the diary fades in and out.")]
    public float fadeSpeed = 3f;

    [Tooltip("Background overlay color (default black).")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 1f);

    [Header("Player Reference")]
    [Tooltip("Drag the Player GameObject here, or leave empty to auto-find.")]
    public DustBunnyController playerController;

    [Header("SFX")]
    public AK.Wwise.Event uiSelect;

    // -------------------------------------------------------------------------
    // Public State
    // -------------------------------------------------------------------------

    // Tracks whether the diary has been shown this session.
    // Reset by ResetState() so it can be shown again on level reload.
    public bool diaryShown = false;

    /// <summary>
    /// Fired after the diary has fully closed and player control is restored.
    /// </summary>
    public event Action DiaryClosed;

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    private bool isDiaryShowing = false;
    private bool waitingForInput = false;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
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
        {
            playerController = FindAnyObjectByType<DustBunnyController>();
            if (playerController == null)
                Debug.LogWarning("[DiaryUIManager] No DustBunnyController found in scene!");
        }

        SetGroupAlpha(backgroundGroup, 0f, false);
        SetGroupAlpha(diaryPanelGroup, 0f, false);

        if (instructionImage != null)
            instructionImage.gameObject.SetActive(false);
    }

    void Update()
    {
        if (isDiaryShowing && waitingForInput)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                StartCoroutine(HideDiary());
            }
        }
    }

    // -------------------------------------------------------------------------
    // Public Reset — called by FadeSequenceManager on scene reload
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fully resets all diary UI state back to its initial values.
    /// Called automatically by FadeSequenceManager when the Level scene loads.
    /// Safe to call mid-display — stops coroutines and re-enables the player.
    /// </summary>
    public void ResetState()
    {
        // Stop any running fade/blink coroutines.
        StopAllCoroutines();

        // Reset all state flags — including diaryShown so it can trigger again.
        isDiaryShowing = false;
        waitingForInput = false;
        diaryShown = false;

        // Hide all canvas groups immediately.
        SetGroupAlpha(backgroundGroup, 0f, false);
        SetGroupAlpha(diaryPanelGroup, 0f, false);

        if (instructionImage != null)
            instructionImage.gameObject.SetActive(false);

        // Re-enable the player controller in case scene reloaded mid-display.
        if (playerController != null)
            playerController.enabled = true;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void ShowDiary(string text, Sprite sprite)
    {
        if (isDiaryShowing) return;
        diaryShown = true;

        StopAllCoroutines();
        StartCoroutine(DisplayDiary(text, sprite));
    }

    public void DismissDiary()
    {
        if (isDiaryShowing)
            StartCoroutine(HideDiary());
    }

    public bool IsDiaryShowing() => isDiaryShowing;

    // -------------------------------------------------------------------------
    // Private Coroutines
    // -------------------------------------------------------------------------

    private IEnumerator DisplayDiary(string text, Sprite sprite)
    {
        AkUnitySoundEngine.SetState("player_state", "memory");

        isDiaryShowing = true;
        waitingForInput = false;

        if (playerController != null)
            playerController.enabled = false;

        if (diaryImage != null && sprite != null)
        {
            diaryImage.sprite = sprite;
            diaryImage.preserveAspect = true;
        }

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

        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = true;
        if (diaryPanelGroup != null) diaryPanelGroup.blocksRaycasts = true;

        yield return StartCoroutine(FadeGroup(backgroundGroup, 0f, 1f));
        yield return StartCoroutine(FadeGroup(diaryPanelGroup, 0f, 1f));

        yield return new WaitForSeconds(0.3f);

        waitingForInput = true;

        if (instructionImage != null)
        {
            instructionImage.gameObject.SetActive(true);

            Color c = instructionImage.color;
            c.a = 1f;
            instructionImage.color = c;

            StartCoroutine(BlinkInstructionImage());
        }
    }

    private IEnumerator HideDiary()
    {
        AkUnitySoundEngine.SetState("player_state", "None");
        AkUnitySoundEngine.SetState("mus_zone2", "mus_zone2_4");

        Debug.Log("Music State set to: mus_zone2, mus_zone2_4");

        waitingForInput = false;

        yield return StartCoroutine(FadeGroup(diaryPanelGroup, 1f, 0f));
        yield return StartCoroutine(FadeGroup(backgroundGroup, 1f, 0f));

        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = false;
        if (diaryPanelGroup != null) diaryPanelGroup.blocksRaycasts = false;

        if (instructionImage != null)
            instructionImage.gameObject.SetActive(false);

        if (playerController != null)
        {
            playerController.SuppressJumpForSeconds(0.2f);
            playerController.enabled = true;
        }

        isDiaryShowing = false;
        DiaryClosed?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

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

    private void SetGroupAlpha(CanvasGroup group, float alpha, bool blocksRaycasts)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.blocksRaycasts = blocksRaycasts;
        group.interactable = blocksRaycasts;
    }

    private IEnumerator BlinkInstructionImage()
    {
        if (instructionImage == null) yield break;

        while (waitingForInput)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime * 2f)
            {
                if (!waitingForInput) yield break;
                Color c = instructionImage.color;
                c.a = Mathf.Lerp(1f, 0.3f, t);
                instructionImage.color = c;
                yield return null;
            }

            for (float t = 0; t < 1f; t += Time.deltaTime * 2f)
            {
                if (!waitingForInput) yield break;
                Color c = instructionImage.color;
                c.a = Mathf.Lerp(0.3f, 1f, t);
                instructionImage.color = c;
                yield return null;
            }
        }

        Color final = instructionImage.color;
        final.a = 1f;
        instructionImage.color = final;
    }
}