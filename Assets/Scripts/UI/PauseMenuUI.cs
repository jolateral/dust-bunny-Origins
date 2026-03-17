// =============================================================================
// PauseMenuUI.cs
// -----------------------------------------------------------------------------
// Controls the pause menu book UI — showing/hiding, populating the controller
// diagram labels, handling the Continue / Main Menu buttons, the sensitivity
// slider, and the PlayStation selection icon that follows the focused button.
//
// HIERARCHY SETUP (see SETUP_INSTRUCTIONS.md for full detail):
//
//   PauseCanvas  (Canvas)
//   └── BookPanel  (Image — PauseMenu.png)
//       ├── LeftPage  (empty RectTransform — left half of book)
//       │   ├── ContinueButton      (Button + TextMeshProUGUI child)
//       │   ├── MainMenuButton      (Button + TextMeshProUGUI child)
//       │   ├── SensitivityLabel    (TextMeshProUGUI  — "Camera Sensitivity")
//       │   └── SensitivitySlider   (Slider)
//       ├── RightPage  (empty RectTransform — right half of book)
//       │   └── ControllerDiagram   (Image — ControllerDiagram.png)
//       ├── Labels  (empty RectTransform)
//       │   └── LabelMove / LabelLook / LabelJump / LabelDash / LabelGlide
//       └── PlaystationXIcon        (Image — your PS X icon sprite)
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public class PauseMenuUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------

    public static PauseMenuUI Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector — Book Root
    // -------------------------------------------------------------------------

    [Header("--- Book Root ---")]
    [Tooltip("The root panel of the pause book. This is shown/hidden on pause toggle.")]
    [SerializeField] private GameObject bookRoot;

    // -------------------------------------------------------------------------
    // Inspector — Left Page Buttons
    // -------------------------------------------------------------------------

    [Header("--- Left Page Buttons ---")]
    [Tooltip("Button that resumes the game and closes the pause menu.")]
    [SerializeField] private Button continueButton;

    [Tooltip("Button that fades to the StartMenu scene.")]
    [SerializeField] private Button mainMenuButton;

    [Tooltip("The first button selected when the pause menu opens. " +
             "Drag ContinueButton here so the controller lands on it immediately.")]
    [SerializeField] private GameObject firstSelected;

    // -------------------------------------------------------------------------
    // Inspector — Sensitivity Slider
    // -------------------------------------------------------------------------

    [Header("--- Sensitivity Slider ---")]
    [Tooltip("The UI Slider that controls camera rotation speed.")]
    [SerializeField] private Slider sensitivitySlider;

    [Tooltip("Minimum rotationSpeed value the slider maps to.")]
    [SerializeField] private float sensitivityMin = 0.5f;

    [Tooltip("Maximum rotationSpeed value the slider maps to.")]
    [SerializeField] private float sensitivityMax = 6f;

    // -------------------------------------------------------------------------
    // Inspector — PlayStation Selection Icon
    // -------------------------------------------------------------------------

    [Header("--- PlayStation Selection Icon ---")]
    [Tooltip("The X icon RectTransform that moves to sit beside the selected UI element.")]
    [SerializeField] private RectTransform playstationXIcon;

    [Tooltip("Pixel offset from the selected element's anchored position.")]
    [SerializeField] private Vector2 iconOffset = new Vector2(-169f, 0f);

    [Tooltip("If true, hides the icon when nothing is selected.")]
    [SerializeField] private bool hideIconWhenNothingSelected = false;

    // -------------------------------------------------------------------------
    // Inspector — Wwise Audio
    // -------------------------------------------------------------------------

    [Header("--- Audio ---")]
    [Tooltip("Wwise event played when Continue or Main Menu is pressed.")]
    public AK.Wwise.Event uiSelect;

    // -------------------------------------------------------------------------
    // Inspector — Controller Diagram Labels
    // -------------------------------------------------------------------------

    [Header("--- Controller Diagram Labels ---")]
    [Tooltip("Label for the Left Stick — Move")]
    [SerializeField] private TextMeshProUGUI labelMove;

    [Tooltip("Label for the Right Stick — Look / Camera")]
    [SerializeField] private TextMeshProUGUI labelLook;

    [Tooltip("Label for the X button — Jump")]
    [SerializeField] private TextMeshProUGUI labelJump;

    [Tooltip("Label for R1 — Dash")]
    [SerializeField] private TextMeshProUGUI labelDash;

    [Tooltip("Label for R2 (hold) — Glide")]
    [SerializeField] private TextMeshProUGUI labelGlide;

    // -------------------------------------------------------------------------
    // Label Text Constants
    // Edit these strings to change what the diagram labels say.
    // -------------------------------------------------------------------------

    private const string TEXT_MOVE  = "Move";
    private const string TEXT_LOOK  = "Look";
    private const string TEXT_JUMP  = "Jump";
    private const string TEXT_DASH  = "Dash";
    private const string TEXT_GLIDE = "Glide (Hold)";

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    // Cached reference so the slider can update rotationSpeed live.
    private ThirdPersonCamera thirdPersonCamera;

    // Tracks the last icon target so we don't reposition unnecessarily.
    private GameObject lastIconTarget;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // --- Singleton enforcement ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Book starts hidden when the scene loads.
        if (bookRoot != null)
            bookRoot.SetActive(false);
    }

    private void Start()
    {
        // Populate controller diagram labels.
        SetupLabels();

        // Wire up button onClick listeners.
        SetupButtons();

        // Cache the camera script for the sensitivity slider.
        if (Camera.main != null)
            thirdPersonCamera = Camera.main.GetComponent<ThirdPersonCamera>();

        if (thirdPersonCamera == null)
            Debug.LogWarning("[PauseMenuUI] ThirdPersonCamera not found on Main Camera. " +
                             "Sensitivity slider will have no effect.");

        // Set up the slider range and sync it to the camera's current value.
        SetupSensitivitySlider();
    }

    private void Update()
    {
        // Keep the PlayStation X icon tracking the currently selected element.
        UpdateSelectionIcon(false);
    }

    // -------------------------------------------------------------------------
    // Public API — called by PauseManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// Shows the pause book, unlocks the cursor, and selects the first button
    /// so the controller can navigate immediately.
    /// </summary>
    public void Show()
    {
        if (bookRoot != null)
            bookRoot.SetActive(true);

        // Unlock cursor so mouse/touchpad can interact with the UI.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Select the first button so the controller highlights it straight away.
        SelectObject(firstSelected);

        // Force the icon to refresh its position on open.
        UpdateSelectionIcon(true);
    }

    /// <summary>
    /// Hides the pause book and re-locks the cursor for gameplay.
    /// </summary>
    public void Hide()
    {
        if (bookRoot != null)
            bookRoot.SetActive(false);

        // Re-lock cursor for gameplay — adjust CursorLockMode if your game uses a different default.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Clear EventSystem selection so no button stays highlighted during gameplay.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>
    /// Returns true while the pause book is visible.
    /// Used by DustBunnyController and other scripts to block input.
    /// </summary>
    public bool IsPauseMenuShowing()
    {
        return bookRoot != null && bookRoot.activeSelf;
    }

    // -------------------------------------------------------------------------
    // Button Setup & Callbacks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers onClick listeners on the Continue and Main Menu buttons.
    /// </summary>
    private void SetupButtons()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinuePressed);
        else
            Debug.LogWarning("[PauseMenuUI] Continue Button is not assigned in the Inspector.");

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuPressed);
        else
            Debug.LogWarning("[PauseMenuUI] Main Menu Button is not assigned in the Inspector.");
    }

    /// <summary>
    /// Continue button — plays UI sound then tells PauseManager to unpause.
    /// PauseManager.Unpause() calls Hide() internally, so we don't call it here.
    /// </summary>
    private void OnContinuePressed()
    {
        uiSelect.Post(gameObject);

        if (PauseManager.Instance != null)
            PauseManager.Instance.Unpause();
    }

    /// <summary>
    /// Main Menu button — plays UI sound, restores timeScale, then fades to StartMenu.
    /// timeScale must be reset to 1 BEFORE the fade, otherwise the fade coroutine
    /// runs at timeScale 0 and never completes.
    /// </summary>
    private void OnMainMenuPressed()
    {
        uiSelect.Post(gameObject);

        // Restore time before the scene transition — fade won't work at timeScale 0.
        Time.timeScale = 1f;

        if (FadeSequenceManager.Instance != null)
        {
            FadeSequenceManager.Instance.FadeToScene("StartMenu", 1.5f);
        }
        else
        {
            // Safe fallback if FadeSequenceManager isn't present.
            Debug.LogWarning("[PauseMenuUI] FadeSequenceManager not found. Loading StartMenu directly.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("StartMenu");
        }
    }

    // -------------------------------------------------------------------------
    // Sensitivity Slider
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sets the slider's min/max range and initialises its value to match the
    /// camera's current rotationSpeed, so it doesn't snap on first open.
    /// </summary>
    private void SetupSensitivitySlider()
    {
        if (sensitivitySlider == null)
        {
            Debug.LogWarning("[PauseMenuUI] Sensitivity Slider is not assigned in the Inspector.");
            return;
        }

        sensitivitySlider.minValue = sensitivityMin;
        sensitivitySlider.maxValue = sensitivityMax;

        // Sync the slider's starting position to the camera's current sensitivity.
        if (thirdPersonCamera != null)
            sensitivitySlider.value = thirdPersonCamera.rotationSpeed;
        else
            sensitivitySlider.value = (sensitivityMin + sensitivityMax) / 2f;

        // Subscribe to value changes so sensitivity updates live as the player drags.
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
    }

    /// <summary>
    /// Called every time the slider value changes.
    /// Immediately writes the new value to ThirdPersonCamera.rotationSpeed.
    /// </summary>
    private void OnSensitivityChanged(float value)
    {
        if (thirdPersonCamera != null)
            thirdPersonCamera.rotationSpeed = value;
    }

    // -------------------------------------------------------------------------
    // Controller Diagram Labels
    // -------------------------------------------------------------------------

    /// <summary>
    /// Assigns text to all five controller diagram TMP labels.
    /// Add more SetLabel() calls here if you expand the diagram later.
    /// </summary>
    private void SetupLabels()
    {
        SetLabel(labelMove,  TEXT_MOVE);
        SetLabel(labelLook,  TEXT_LOOK);
        SetLabel(labelJump,  TEXT_JUMP);
        SetLabel(labelDash,  TEXT_DASH);
        SetLabel(labelGlide, TEXT_GLIDE);
    }

    /// <summary>
    /// Safely assigns text to a TMP label and warns if the reference is missing.
    /// </summary>
    private void SetLabel(TextMeshProUGUI label, string text)
    {
        if (label != null)
            label.text = text;
        else
            Debug.LogWarning($"[PauseMenuUI] A label reference is missing in the Inspector. " +
                             $"Expected text: \"{text}\"");
    }

    // -------------------------------------------------------------------------
    // PlayStation Selection Icon  (mirrors GameMenu behaviour exactly)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Repositions the PlayStation X icon next to the currently selected UI element.
    /// The icon is only shown for buttons — it hides when the slider is selected.
    /// Pass force = true to always update (used on Show()), false to skip if unchanged.
    /// </summary>
    private void UpdateSelectionIcon(bool force)
    {
        // Only run while the menu is open.
        if (!IsPauseMenuShowing()) return;
        if (EventSystem.current == null || playstationXIcon == null) return;

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        // Skip if nothing has changed and we're not forcing a refresh.
        if (!force && currentSelected == lastIconTarget && playstationXIcon.gameObject.activeSelf)
            return;

        // Nothing selected.
        if (currentSelected == null)
        {
            lastIconTarget = null;
            playstationXIcon.gameObject.SetActive(false);
            return;
        }

        // Hide the icon if the selected object is the sensitivity slider —
        // the icon only makes sense next to buttons, not a slider.
        if (sensitivitySlider != null &&
            currentSelected == sensitivitySlider.gameObject)
        {
            lastIconTarget = currentSelected;
            playstationXIcon.gameObject.SetActive(false);
            return;
        }

        // Selected object has no RectTransform — skip safely.
        RectTransform selectedRect = currentSelected.GetComponent<RectTransform>();
        if (selectedRect == null)
        {
            lastIconTarget = null;
            if (hideIconWhenNothingSelected)
                playstationXIcon.gameObject.SetActive(false);
            return;
        }

        // Reparent and reposition the icon beside the selected element.
        playstationXIcon.gameObject.SetActive(true);
        playstationXIcon.SetParent(selectedRect.parent, false);
        playstationXIcon.anchoredPosition = selectedRect.anchoredPosition + iconOffset;
        lastIconTarget = currentSelected;
    }

    // -------------------------------------------------------------------------
    // EventSystem Helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Clears then re-sets the EventSystem selection so Unity reliably fires
    /// the Select event even if the object was already selected last frame.
    /// Matches GameMenu.SelectObject() exactly.
    /// </summary>
    private void SelectObject(GameObject obj)
    {
        if (EventSystem.current == null || obj == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(obj);
    }
}