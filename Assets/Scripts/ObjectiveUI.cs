using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// ObjectiveUI.cs
///
/// Manages the fragment progress panel in the top-right corner.
///
/// BEHAVIOUR:
/// - Starts completely hidden (alpha 0, disabled)
/// - Fades IN after the player dismisses the paper UI
/// - Fades OUT instantly when the paper UI opens
/// - Auto-fades out after all pieces are collected
/// </summary>
public class ObjectiveUI : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Singleton
    // -----------------------------------------------------------------------

    public static ObjectiveUI Instance;

    // -----------------------------------------------------------------------
    // Inspector References
    // -----------------------------------------------------------------------

    [Header("Panel References")]
    [Tooltip("The CanvasGroup on the Panel — used for fading in and out.")]
    public CanvasGroup panelCanvasGroup;

    [Tooltip("The RectTransform of the Panel (still needed to find the GameObject).")]
    public RectTransform panel;

    [Tooltip("The FragmentProgressUI component on the Panel child.")]
    public FragmentProgressUI fragmentProgressUI;

    [Header("Fade Settings")]
    [Tooltip("How long the panel takes to fade in or out.")]
    public float fadeTime = 0.4f;

    [Header("Toggle Input")]
    [Tooltip("Input action that toggles the panel. Optional.")]
    public InputActionReference toggleObjectiveAction;

    // -----------------------------------------------------------------------
    // Private State
    // -----------------------------------------------------------------------

    private bool isVisible = false;
    private bool hasCollectedAny = false;
    private Coroutine fadeRoutine;
    private Coroutine hideRoutine;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    void Awake()
    {
        Instance = this;

        if (panelCanvasGroup == null && panel != null)
        {
            // Auto-add a CanvasGroup if one wasn't assigned
            panelCanvasGroup = panel.gameObject.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }

        // Start fully hidden and disabled
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

    // -----------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------

    private void OnToggleObjective(InputAction.CallbackContext ctx)
    {
        if (hasCollectedAny)
            Toggle();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void Toggle()
    {
        if (isVisible) FadeOut();
        else FadeIn();
    }

    public void FadeIn()
    {
        if (!hasCollectedAny) return;

        panel.gameObject.SetActive(true);
        isVisible = true;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTo(1f));
    }

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
    /// Fades the panel back in.
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
    /// Called when a fragment is collected.
    /// Syncs all fragment image data silently.
    /// Panel only appears after ShowAfterPaper() is called.
    /// </summary>
    public void SetObjective()
    {
        var paperData = PaperUIManager.Instance != null
            ? PaperUIManager.Instance.CurrentPaperData
            : null;

        if (paperData == null) return;

        hasCollectedAny = true;

        int collected = paperData.GetCollectedCount();
        int total = paperData.totalPieces;

        if (fragmentProgressUI != null)
            fragmentProgressUI.SyncProgress(paperData.GetCollectedPieces(), total);

        // Auto-hide after a delay once all pieces are collected
        if (collected >= total)
        {
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(FadeOutAfterDelay(3f));
        }
    }

    /// <summary>
    /// Reveal a single fragment image by index.
    /// Called from PaperItem immediately when a piece is absorbed.
    /// Panel is still hidden — this just updates the image data so it's
    /// ready to show when ShowAfterPaper() is called.
    /// </summary>
    public void RevealFragment(int pieceIndex, int total)
    {
        hasCollectedAny = true;

        if (fragmentProgressUI != null)
            fragmentProgressUI.RevealFragment(pieceIndex, total);
    }

    // -----------------------------------------------------------------------
    // Private Helpers
    // -----------------------------------------------------------------------

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

        // If fully faded out, disable the GameObject
        if (targetAlpha <= 0f)
        {
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.interactable = false;
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