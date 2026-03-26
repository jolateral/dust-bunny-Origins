// =============================================================================
// ObjectiveUI.cs
// -----------------------------------------------------------------------------
// Manages the fragment progress panel in the top-right corner.
//
// CHANGES FROM ORIGINAL:
//   - Added public ResetState() method so FadeSequenceManager can reset it
//     when the Level scene reloads.
//   - ResetState() clears hasCollectedAny and hides the panel so the objective
//     UI starts fresh every time the level is entered.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class ObjectiveUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------

    public static ObjectiveUI Instance;

    // -------------------------------------------------------------------------
    // Inspector References
    // -------------------------------------------------------------------------

    [Header("Panel References")]
    [Tooltip("The RectTransform of the Panel child.")]
    public RectTransform panel;

    [Tooltip("The CanvasGroup on the Panel — used for fading.")]
    public CanvasGroup panelCanvasGroup;

    [Tooltip("The FragmentProgressUI component on the Panel.")]
    public FragmentProgressUI fragmentProgressUI;

    [Header("Fade Settings")]
    [Tooltip("How long the panel takes to fade in or out.")]
    public float fadeTime = 0.4f;

    [Header("Toggle Input")]
    [Tooltip("Input action that toggles the panel. Optional.")]
    public InputActionReference toggleObjectiveAction;

    [Header("Objective Text")]
    public GameObject objectiveImage;

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    private bool isVisible = false;
    private bool hasCollectedAny = false;
    private Coroutine fadeRoutine;
    private Coroutine hideRoutine;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        Instance = this;

        // Auto-grab CanvasGroup from Panel if not assigned.
        if (panelCanvasGroup == null && panel != null)
        {
            panelCanvasGroup = panel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }

        // Start fully hidden and disabled.
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
        }

        if (panel != null)
            panel.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (toggleObjectiveAction != null)
        {
            toggleObjectiveAction.action.Enable();
            toggleObjectiveAction.action.performed += OnToggleObjective;
        }
    }

    void OnDisable()
    {
        if (toggleObjectiveAction != null)
            toggleObjectiveAction.action.performed -= OnToggleObjective;
    }

    // -------------------------------------------------------------------------
    // Public Reset — called by FadeSequenceManager on scene reload
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resets the objective UI back to its initial hidden state.
    /// Called automatically by FadeSequenceManager when the Level scene loads.
    /// Clears hasCollectedAny so the first-fragment logic fires correctly again.
    /// </summary>
    public void ResetState()
    {
        // Stop any in-progress fades.
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        // Reset state flags.
        isVisible = false;
        hasCollectedAny = false;

        // Hide the panel immediately.
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
        }

        if (panel != null)
            panel.gameObject.SetActive(false);

        // Re-show the objective text since no fragments have been collected yet.
        if (objectiveImage != null)
            objectiveImage.SetActive(true);

        // Clear the fragment progress display so it doesn't show stale data.
        if (fragmentProgressUI != null)
            fragmentProgressUI.ResetAll();
    }

    // -------------------------------------------------------------------------
    // Input
    // -------------------------------------------------------------------------

    private void OnToggleObjective(InputAction.CallbackContext ctx)
    {
        if (hasCollectedAny)
            Toggle();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void Toggle()
    {
        if (isVisible) FadeOut();
        else FadeIn();
    }

    /// <summary>Fade the panel in. Only works if at least one fragment has been collected.</summary>
    public void FadeIn()
    {
        if (!hasCollectedAny) return;

        if (panel != null)
            panel.gameObject.SetActive(true);

        isVisible = true;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTo(1f));
    }

    /// <summary>Fade the panel out.</summary>
    public void FadeOut()
    {
        isVisible = false;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTo(0f));
    }

    /// <summary>
    /// Called by PaperUIManager the instant paper opens.
    /// Hides the panel immediately with no animation.
    /// </summary>
    public void HideForPaper()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        isVisible = false;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
        }

        if (panel != null)
            panel.gameObject.SetActive(false);
    }

    /// <summary>
    /// Called by PaperUIManager after paper fully closes.
    /// Fades the panel back in so the player can see their progress.
    /// </summary>
    public void ShowAfterPaper()
    {
        if (!hasCollectedAny) return;

        var paperData = PaperUIManager.Instance != null
            ? PaperUIManager.Instance.CurrentPaperData
            : null;

        if (paperData != null && paperData.IsComplete()) return;

        FadeIn();
    }

    /// <summary>
    /// Called when a fragment is collected. Syncs all fragment image data.
    /// </summary>
    public void SetObjective()
    {
        var paperData = PaperUIManager.Instance != null
            ? PaperUIManager.Instance.CurrentPaperData
            : null;

        if (paperData == null) return;

        if (!hasCollectedAny)
        {
            hasCollectedAny = true;

            if (objectiveImage != null)
                objectiveImage.SetActive(false);
        }

        int collected = paperData.GetCollectedCount();
        int total = paperData.totalPieces;

        if (fragmentProgressUI != null)
            fragmentProgressUI.SyncProgress(paperData.GetCollectedPieces(), total);

        if (collected >= total)
        {
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(FadeOutAfterDelay(3f));
        }
    }

    /// <summary>
    /// Reveal a single fragment image by index.
    /// </summary>
    public void RevealFragment(int pieceIndex, int total)
    {
        if (!hasCollectedAny)
        {
            hasCollectedAny = true;

            if (objectiveImage != null)
                objectiveImage.SetActive(false);
        }

        if (fragmentProgressUI != null)
            fragmentProgressUI.RevealFragment(pieceIndex, total);
    }

    // -------------------------------------------------------------------------
    // Private Helpers
    // -------------------------------------------------------------------------

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (panelCanvasGroup == null) yield break;

        float startAlpha = panelCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeTime);
            yield return null;
        }

        panelCanvasGroup.alpha = targetAlpha;

        if (targetAlpha <= 0f)
        {
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
            if (panel != null)
                panel.gameObject.SetActive(false);
        }
        else
        {
            panelCanvasGroup.blocksRaycasts = true;
            panelCanvasGroup.interactable = true;
        }
    }

    private IEnumerator FadeOutAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        FadeOut();
    }
}