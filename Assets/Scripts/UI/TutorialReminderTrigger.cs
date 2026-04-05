using UnityEngine;

/// <summary>
/// TutorialReminderTrigger — A reminder-style tutorial trigger for dash-gated areas.
///
/// Unlike TutorialTriggerCollision (which fires immediately on entry),
/// this script waits a set amount of time after the player enters the zone.
/// If the player has NOT performed a dash during that window AND is still
/// inside the trigger area, the tutorial popup is shown as a hint/reminder.
///
/// Dash detection uses DustBunnyController.isRolling, which is set to true
/// for the duration of a dash in PerformDash().
///
/// Intended use: Place this in areas where the player is expected to dash.
/// If they linger without dashing, they clearly need a nudge.
/// </summary>
public class TutorialReminderTrigger : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // ENUMS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Whether this reminder shows a text message or a sprite image.
    /// </summary>
    public enum TutorialDisplayMode
    {
        Text,
        Image
    }

    // ─────────────────────────────────────────────
    // INSPECTOR FIELDS
    // ─────────────────────────────────────────────

    [Header("Display Mode")]
    /// <summary>
    /// Choose between showing a text tutorial or an image tutorial.
    /// </summary>
    public TutorialDisplayMode displayMode = TutorialDisplayMode.Text;

    [Header("Text Tutorial")]
    [TextArea(2, 5)]
    /// <summary>
    /// The reminder message shown if the player hasn't dashed in time.
    /// </summary>
    public string tutorialText = "Try dashing into objects to absorb them!";

    /// <summary>
    /// Color of the tutorial text when displayed via MemoryUIManager.
    /// </summary>
    public Color textColor = Color.white;

    [Header("Image Tutorial")]
    /// <summary>
    /// The sprite to display if displayMode is set to Image.
    /// </summary>
    public Sprite tutorialImage;

    [Header("Reminder Settings")]
    /// <summary>
    /// How long (in seconds) the player must be inside the zone without dashing
    /// before the reminder popup appears. Default is 10 seconds.
    /// </summary>
    public float reminderDelay = 10f;

    /// <summary>
    /// If true, the reminder will only ever show once, even if the player
    /// leaves and re-enters the trigger zone.
    /// </summary>
    public bool oneTimeOnly = true;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    /// <summary>
    /// Whether the player is currently inside this trigger zone.
    /// Used to confirm the player is still present before showing the reminder.
    /// </summary>
    private bool playerIsInZone = false;

    /// <summary>
    /// Whether the player has dashed at least once while inside this zone.
    /// Detected via DustBunnyController.isRolling, which is true during PerformDash().
    /// If true, the reminder will not show.
    /// </summary>
    private bool playerHasDashed = false;

    /// <summary>
    /// Whether this reminder has already been shown. Used with oneTimeOnly
    /// to prevent repeat displays across multiple zone entries.
    /// </summary>
    private bool hasShownReminder = false;

    /// <summary>
    /// Reference to the player controller, cached on zone entry.
    /// Used to poll isRolling each frame to detect a dash.
    /// </summary>
    private DustBunnyController playerController = null;

    // ─────────────────────────────────────────────
    // COLLISION DETECTION
    // ─────────────────────────────────────────────

    /// <summary>
    /// Called by Unity when the player enters the trigger zone.
    /// Caches the player reference, resets dash tracking, and starts
    /// the reminder countdown via Invoke.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Don't re-trigger if already shown and oneTimeOnly is enabled
        if (hasShownReminder && oneTimeOnly) return;

        // Only respond to the player character
        var player = other.GetComponentInParent<DustBunnyController>();
        if (player == null) return;

        // Mark player as present and cache the reference for Update() checks
        playerIsInZone = true;
        playerController = player;
        playerHasDashed = false;    // Reset dash state for this new zone visit

        // Begin the countdown — ShowReminderIfNeeded() will check conditions
        // after reminderDelay seconds and only show if requirements are met
        Invoke(nameof(ShowReminderIfNeeded), reminderDelay);
    }

    /// <summary>
    /// Called by Unity when the player exits the trigger zone.
    /// Cancels the pending reminder and clears player tracking state.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<DustBunnyController>();
        if (player == null) return;

        // Player has left — cancel the scheduled reminder and clean up
        playerIsInZone = false;
        playerController = null;
        CancelInvoke(nameof(ShowReminderIfNeeded));
    }

    // ─────────────────────────────────────────────
    // DASH DETECTION
    // ─────────────────────────────────────────────

    /// <summary>
    /// Checked every frame while the player is in the zone.
    /// Polls DustBunnyController.isRolling, which is set to true for the
    /// full duration of a dash inside PerformDash() in DustBunnyController.
    /// Once a dash is detected, playerHasDashed is permanently set to true
    /// for this zone visit, suppressing the reminder.
    /// </summary>
    private void Update()
    {
        // Only check for dashes while the player is inside the zone
        if (!playerIsInZone || playerController == null) return;

        // isRolling is true during the entire dash duration (see PerformDash()
        // in DustBunnyController — it's set at the start and cleared after dashDuration)
        if (playerController.isRolling)
        {
            playerHasDashed = true;
        }
    }

    // ─────────────────────────────────────────────
    // REMINDER LOGIC
    // ─────────────────────────────────────────────

    /// <summary>
    /// Called after reminderDelay seconds via Invoke.
    /// Shows the tutorial popup only if ALL three conditions are met:
    ///   1. The player is still inside the trigger zone.
    ///   2. The player has NOT dashed during the delay window.
    ///   3. The reminder hasn't already been shown (if oneTimeOnly is true).
    /// </summary>
    void ShowReminderIfNeeded()
    {
        // Condition 1: Player must still be in the zone
        if (!playerIsInZone) return;

        // Condition 2: Player must not have dashed
        if (playerHasDashed) return;

        // Condition 3: Don't show again if oneTimeOnly is set
        if (hasShownReminder && oneTimeOnly) return;

        // All conditions met — show the tutorial reminder
        hasShownReminder = true;
        ShowTutorial();
    }

    /// <summary>
    /// Passes the tutorial content to MemoryUIManager to render on screen
    /// as either a text message or a sprite image.
    /// </summary>
    void ShowTutorial()
    {
        if (MemoryUIManager.Instance == null) return;

        if (displayMode == TutorialDisplayMode.Image && tutorialImage != null)
        {
            // Display the assigned sprite via the UI manager
            MemoryUIManager.Instance.ShowImage(tutorialImage);
        }
        else
        {
            // Display the reminder text with the assigned color
            MemoryUIManager.Instance.ShowMemory(tutorialText, textColor);
        }
    }
}