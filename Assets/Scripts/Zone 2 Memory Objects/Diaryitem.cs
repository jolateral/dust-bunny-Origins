using UnityEngine;

/// <summary>
/// DiaryItem.cs
/// 
/// Attach this to a Diary GameObject in the world.
/// When the player rolls into the diary while carrying the matching key,
/// the diary "unlocks" and DiaryUIManager shows the full-screen diary image.
/// 
/// If the player rolls into the diary WITHOUT the key, an optional hint message
/// is shown (via MemoryUIManager or a simple Debug.Log, your choice).
/// 
/// SETUP:
/// 1. Create a GameObject with a Collider and Rigidbody
/// 2. Set the tag to "StickyObject" so AbsorbMechanic's OnCollisionEnter fires
/// 3. Add this DiaryItem component
/// 4. Set keyID to match the KeyItem's keyID
/// 5. Assign diarySprite (the single full-screen image to display when unlocked)
/// 6. Optionally assign lockedHintText for the "you need the key" message
/// 
/// SCENE REQUIREMENTS:
/// - A DiaryUIManager must exist somewhere in the scene (on its own GameObject, or PaperCanvas)
/// - A KeyItem with a matching keyID must exist in the scene
/// </summary>
public class DiaryItem : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector Fields
    // -----------------------------------------------------------------------

    [Header("Key Pairing")]
    [Tooltip("Must match the keyID on the KeyItem that unlocks this diary.")]
    public string keyID = "Diary_Key_01";

    [Header("Diary Content")]
    [Tooltip("The full-screen image shown when the diary is unlocked.")]
    public Sprite diarySprite;

    [Tooltip("Optional text shown inside the diary (leave empty for image-only).")]
    [TextArea(5, 15)]
    public string diaryText = "";

    [Header("Locked Behaviour")]
    [Tooltip("Message shown when the player doesn't have the key yet. Uses MemoryUIManager if available.")]
    public string lockedHintText = "It's locked... I need to find the key.";

    [Tooltip("How many seconds to wait before the hint can show again (prevents spam).")]
    public float hintCooldown = 3f;

    [Header("Audio")]
    [Tooltip("Sound played when the diary is successfully unlocked.")]
    public AK.Wwise.Event unlockSfx;

    [Tooltip("Sound played when the player bumps the locked diary.")]
    public AK.Wwise.Event lockedBumpSfx;

    // -----------------------------------------------------------------------
    // Runtime State
    // -----------------------------------------------------------------------

    /// <summary>Whether this specific diary has already been unlocked/read.</summary>
    private bool hasBeenUnlocked = false;

    /// <summary>Cooldown timer so the locked-hint doesn't spam.</summary>
    private float lastHintTime = -99f;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    void Start()
    {
        // Diary does NOT need to be a StickyObject — the player collides with it,
        // but we handle the logic ourselves via OnCollisionEnter here.
        // However, if you WANT the diary to stick to the player after unlocking,
        // keep the StickyObject tag and let AbsorbMechanic attach it.
        // 
        // Default recommended setup: Diary is a regular collider (no StickyObject tag).
        // The player walks into it and this OnCollisionEnter fires.

        if (diarySprite == null)
        {
            Debug.LogWarning($"[DiaryItem] '{name}' has no diarySprite assigned! The UI won't show an image.");
        }

        if (DiaryUIManager.Instance == null)
        {
            Debug.LogWarning($"[DiaryItem] No DiaryUIManager found in scene! Make sure one exists.");
        }
    }

    /// <summary>
    /// Fires when the player's collider touches the diary's collider.
    /// We check the DustBunnyController to confirm it's the player (not a random object).
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        // Only react to the player
        DustBunnyController player = collision.gameObject.GetComponent<DustBunnyController>();
        if (player == null) return;

        // If already unlocked, do nothing (or optionally re-show the diary)
        if (hasBeenUnlocked) return;

        // Check whether the player is carrying the matching key
        bool hasKey = KeyItem.CollectedKeys.ContainsKey(keyID) && KeyItem.CollectedKeys[keyID];

        if (hasKey)
        {
            UnlockDiary();
        }
        else
        {
            ShowLockedHint();
        }
    }

    // -----------------------------------------------------------------------
    // Private Methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// The player has the key — unlock the diary and show the UI.
    /// </summary>
    private void UnlockDiary()
    {
        hasBeenUnlocked = true;

        Debug.Log($"[DiaryItem] Diary unlocked with key '{keyID}'!");

        // Play unlock sound
        if (unlockSfx != null)
            unlockSfx.Post(gameObject);

        // Show the diary UI (full-screen image, just like the paper system)
        if (DiaryUIManager.Instance != null)
        {
            DiaryUIManager.Instance.ShowDiary(diaryText, diarySprite);
        }
        else
        {
            Debug.LogError("[DiaryItem] DiaryUIManager.Instance is null! Can't show diary.");
        }
    }

    /// <summary>
    /// The player doesn't have the key yet — show a hint message with cooldown.
    /// </summary>
    private void ShowLockedHint()
    {
        // Respect cooldown so we don't spam the hint
        if (Time.time - lastHintTime < hintCooldown) return;
        lastHintTime = Time.time;

        Debug.Log($"[DiaryItem] Diary is locked. Hint: '{lockedHintText}'");

        // Play bump sound
        if (lockedBumpSfx != null)
            lockedBumpSfx.Post(gameObject);

        // Show hint via MemoryUIManager if it exists in the scene
        // (same system used for other text popups in your game)
        if (MemoryUIManager.Instance != null)
        {
            MemoryUIManager.Instance.ShowMemory(lockedHintText, Color.white);
        }
    }

    // -----------------------------------------------------------------------
    // Editor Visualization
    // -----------------------------------------------------------------------

    void OnDrawGizmos()
    {
        // Brown/red cube to mark the diary in the editor
        Gizmos.color = hasBeenUnlocked
            ? new Color(0f, 1f, 0f, 0.4f)    // Green when unlocked (runtime only)
            : new Color(0.6f, 0.3f, 0.1f, 0.6f); // Brown when locked

        Gizmos.DrawCube(transform.position, transform.localScale);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.7f,
            $"📖 Diary\nKey: {keyID}"
        );
#endif
    }
}