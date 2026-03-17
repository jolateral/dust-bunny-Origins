// =============================================================================
// ButtonTextHighlight.cs
// -----------------------------------------------------------------------------
// Attach this to any Button GameObject that has a TextMeshProUGUI child.
// It watches the EventSystem each frame and changes the text colour depending
// on whether this button is currently selected (controller focus / mouse hover).
//
// SETUP:
//   1. Select ContinueButton (or any button) in the Hierarchy.
//   2. Add Component → ButtonTextHighlight.
//   3. Drag the Text (TMP) child into the "Label" field in the Inspector.
//   4. Pick your Normal and Selected colours in the Inspector.
//   5. Repeat for MainMenuButton.
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonTextHighlight : MonoBehaviour,
    ISelectHandler,    // called by EventSystem when controller/tab selects this
    IDeselectHandler,  // called by EventSystem when something else is selected
    IPointerEnterHandler, // called when mouse enters the button
    IPointerExitHandler   // called when mouse leaves the button
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("--- Text Reference ---")]
    [Tooltip("Drag the Text (TMP) child of this button here.")]
    [SerializeField] private TextMeshProUGUI label;

    [Header("--- Colours ---")]
    [Tooltip("Text colour when the button is in its normal/unselected state.")]
    [SerializeField] private Color normalColor = Color.black;

    [Tooltip("Text colour when the button is selected or hovered.")]
    [SerializeField] private Color selectedColor = Color.white;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Try to find the TMP label automatically if not assigned in Inspector.
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>();

        if (label == null)
            Debug.LogWarning($"[ButtonTextHighlight] No TextMeshProUGUI found on {gameObject.name}. " +
                             "Please assign it in the Inspector.");
    }

    private void Start()
    {
        // Always start in the normal (unselected) colour.
        SetColor(normalColor);
    }

    // -------------------------------------------------------------------------
    // EventSystem Interface Callbacks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called when this button is selected via controller, Tab key, or code.
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        SetColor(selectedColor);
    }

    /// <summary>
    /// Called when this button loses selection (another element is selected).
    /// </summary>
    public void OnDeselect(BaseEventData eventData)
    {
        SetColor(normalColor);
    }

    /// <summary>
    /// Called when the mouse cursor enters the button area.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetColor(selectedColor);
    }

    /// <summary>
    /// Called when the mouse cursor leaves the button area.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        // Only revert to normal if this button isn't still selected by the controller.
        // This prevents a flicker when the mouse leaves but the controller has focus.
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject)
            return;

        SetColor(normalColor);
    }

    // -------------------------------------------------------------------------
    // Private Helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies a colour to the TMP label safely.
    /// </summary>
    private void SetColor(Color color)
    {
        if (label != null)
            label.color = color;
    }
}