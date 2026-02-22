using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

using UnityEngine.InputSystem;

public class ObjectiveUI : MonoBehaviour
{
    public static ObjectiveUI Instance;

    [Header("Refs")]
    public TextMeshProUGUI objectiveText;
    public RectTransform panel;     // drag the ObjectiveText rect OR a parent panel rect

    [Header("Slide")]
    public float slideTime = 0.35f;
    public float offscreenPadding = 40f; // extra push offscreen

    //public GameObject infoIcon;

    [Header("Toggle")]
    public bool startVisible = true;
    public InputActionReference toggleObjectiveAction;

    private Vector2 shownPos;
    private Vector2 hiddenPos;
    private bool isVisible;
    private Coroutine slideRoutine;
    private Coroutine hideRoutine;

    void Awake()
    {
        Instance = this;

        if (panel == null && objectiveText != null)
            panel = objectiveText.rectTransform;

        // Save current anchored position as "shown"
        shownPos = panel.anchoredPosition;

        // Move off-screen to the right by width + padding
        hiddenPos = shownPos + new Vector2(panel.rect.width + offscreenPadding, 0f);

        // Start state
        isVisible = startVisible;
        panel.anchoredPosition = isVisible ? shownPos : hiddenPos;
        objectiveText.gameObject.SetActive(true); // keep active so it can slide

        //if (infoIcon != null)
        //    infoIcon.SetActive(!isVisible);
    }

    private void OnEnable()
    {
        if (toggleObjectiveAction != null)
        {
            toggleObjectiveAction.action.Enable();
            toggleObjectiveAction.action.performed += OnToggleObjective;
        }
    }

    private void OnDisable()
    {
        if (toggleObjectiveAction != null)
            toggleObjectiveAction.action.performed -= OnToggleObjective;
    }

    private void OnToggleObjective(InputAction.CallbackContext ctx)
    {
        Toggle();
    }

    public void Toggle()
    {
        if (isVisible) SlideOut();
        else SlideIn();
    }

    public void SlideIn()
    {
        isVisible = true;
        StartSlide(shownPos);

        //if (infoIcon != null)
        //    infoIcon.SetActive(false);   // hide icon when panel visible
    }

    public void SlideOut()
    {
        isVisible = false;
        StartSlide(hiddenPos);

        //if (infoIcon != null)
        //    infoIcon.SetActive(true);    // show icon when panel hidden
    }

    public void SetObjective()
    {
        var paperData = PaperUIManager.Instance != null ? PaperUIManager.Instance.CurrentPaperData : null;
        if (paperData == null) return;

        // If objective was hidden, you can choose to slide it in when it updates:
        // SlideIn();

        if (paperData.GetCollectedCount() >= paperData.totalPieces)
        {
            objectiveText.text = "All memory fragments collected!";

            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(SlideOutAfterDelay(3f));
            return;
        }

        objectiveText.text =
            $"Find the memory fragments of the childhood drawing. " +
            $"{paperData.GetCollectedCount()}/{paperData.totalPieces} absorbed.";
    }

    // --- Sliding ---
    private void StartSlide(Vector2 target)
    {
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideTo(target));
    }

    private IEnumerator SlideTo(Vector2 target)
    {
        Vector2 start = panel.anchoredPosition;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / slideTime;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            panel.anchoredPosition = Vector2.Lerp(start, target, eased);
            yield return null;
        }

        panel.anchoredPosition = target;
    }

    private IEnumerator SlideOutAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SlideOut();
    }
}