// =============================================================================
// PauseManager.cs
// -----------------------------------------------------------------------------
// Singleton that owns all pause/unpause logic for the game.
//
// HOW IT WORKS:
//   - Listens for the "Pause" action on the UI action map (Options button on PS).
//   - Calls PauseMenuUI.Show() / PauseMenuUI.Hide() to drive the book UI.
//   - Sets Time.timeScale = 0 when paused so all physics/animations freeze.
//   - Sets Time.timeScale = 1 when unpaused.
//
// SETUP IN UNITY:
//   1. Create an empty GameObject in your scene called "PauseManager".
//   2. Attach this script to it.
//   3. Assign your PlayerInputActions asset in the Inspector.
//   4. Make sure "Don't Destroy On Load" is ticked if you have multiple scenes.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------

    /// <summary>
    /// Global access point. Other scripts can call PauseManager.Instance.IsPaused.
    /// </summary>
    public static PauseManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector References
    // -------------------------------------------------------------------------

    [Header("--- Input ---")]
    [Tooltip("Drag your PlayerInputActions asset here.")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("--- UI Reference ---")]
    [Tooltip("Drag the PauseMenuUI component from your Canvas here.")]
    [SerializeField] private PauseMenuUI pauseMenuUI;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    /// <summary>True while the game is paused.</summary>
    public bool IsPaused { get; private set; } = false;

    // The specific InputAction we will listen to (resolved from the asset above).
    private InputAction pauseAction;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // --- Singleton enforcement ---
        // If another PauseManager already exists, destroy this duplicate.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Keep this object alive when loading new scenes (remove if not needed).
        DontDestroyOnLoad(gameObject);

        // --- Resolve the Pause InputAction from the asset ---
        // This looks for an action map called "UI" and an action called "Pause".
        // You will create these in your Input Actions asset (see SETUP_INSTRUCTIONS.md).
        if (inputActions != null)
        {
            // Try to find the action in a "UI" map first, then fall back to any map.
            var uiMap = inputActions.FindActionMap("UI", throwIfNotFound: false);
            if (uiMap != null)
            {
                pauseAction = uiMap.FindAction("Pause", throwIfNotFound: false);
            }

            // Fallback: search every action map if not found in "UI"
            if (pauseAction == null)
            {
                pauseAction = inputActions.FindAction("Pause", throwIfNotFound: false);
            }

            if (pauseAction == null)
            {
                Debug.LogWarning("[PauseManager] Could not find a 'Pause' action in the InputActions asset. " +
                                 "Please add it (see SETUP_INSTRUCTIONS.md).");
            }
        }
        else
        {
            Debug.LogWarning("[PauseManager] No InputActionAsset assigned in the Inspector.");
        }
    }

    private void OnEnable()
    {
        // Subscribe to the pause button press and enable the action.
        if (pauseAction != null)
        {
            pauseAction.performed += OnPausePerformed;
            pauseAction.Enable();
        }
    }

    private void OnDisable()
    {
        // Always unsubscribe to prevent memory leaks.
        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePerformed;
            pauseAction.Disable();
        }
    }

    // -------------------------------------------------------------------------
    // Input Callback
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by the Input System when the Options button is pressed.
    /// </summary>
    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        TogglePause();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Toggles between paused and unpaused states.
    /// Safe to call from any script (e.g. a pause button in the UI later).
    /// </summary>
    public void TogglePause()
    {
        if (IsPaused)
            Unpause();
        else
            Pause();
    }

    /// <summary>
    /// Freezes the game and shows the pause menu book.
    /// </summary>
    public void Pause()
    {
        if (IsPaused) return;

        IsPaused = true;

        // Freeze all physics, animations, and Update loops that use Time.deltaTime.
        Time.timeScale = 0f;

        // Show the book UI.
        if (pauseMenuUI != null)
            pauseMenuUI.Show();
        else
            Debug.LogWarning("[PauseManager] PauseMenuUI reference is missing.");
    }

    /// <summary>
    /// Resumes the game and hides the pause menu book.
    /// </summary>
    public void Unpause()
    {
        if (!IsPaused) return;

        IsPaused = false;

        // Restore normal time.
        Time.timeScale = 1f;

        // Hide the book UI.
        if (pauseMenuUI != null)
            pauseMenuUI.Hide();
    }
}