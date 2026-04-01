using UnityEngine;

/// <summary>
/// DiaryItem.cs
/// 
/// Attach this to a Diary GameObject in the world.
/// When the player rolls into the diary while carrying the matching key,
/// the diary "unlocks" and DiaryUIManager shows the full-screen diary image.
/// The key disappears from the player when the diary is unlocked.
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

    public GameObject diaryLock;

    [Header("World Model Swap (Closed -> Open)")]
    [Tooltip("The closed diary model to hide after the entry is read (usually this GameObject or its mesh root).")]
    public GameObject diaryClosedObject;

    [Tooltip("The open diary model to show after the entry is read (e.g. 'DiaryOpen').")]
    public GameObject diaryOpenObject;

    [Tooltip("Optional text shown inside the diary (leave empty for image-only).")]
    [TextArea(5, 15)]
    public string diaryText = "";

    [Header("Locked Behaviour")]
    [Tooltip("Message shown when the player doesn't have the key yet. Uses MemoryUIManager if available.")]
    public string lockedHintText = "It's locked... I need to find the key.";
    [SerializeField] private Sprite hintImage;

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

        if (diaryOpenObject != null)
            diaryOpenObject.SetActive(false);

        if (diaryClosedObject != null)
            diaryClosedObject.SetActive(true);
    }

    /// <summary>
    /// Fires when the player's collider touches the diary's collider.
    /// We check the DustBunnyController to confirm it's the player (not a random object).
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        DustBunnyController player = collision.gameObject.GetComponent<DustBunnyController>();
        if (player == null) return;

        if (hasBeenUnlocked) return;

        bool hasKey = KeyItem.CollectedKeys.ContainsKey(keyID) && KeyItem.CollectedKeys[keyID];

        if (hasKey)
        {
            ForcePlayerIdle(player);
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
    /// The player has the key — hide the key, unlock the diary, and show the UI.
    /// </summary>
    private void UnlockDiary()
    {
        hasBeenUnlocked = true;

        if (diaryLock != null)
        {
            diaryLock.SetActive(false);
        }

        Debug.Log($"[DiaryItem] Diary unlocked with key '{keyID}'!");

        // Play unlock sound
        if (unlockSfx != null)
            unlockSfx.Post(gameObject);

        // --- Hide the floating key ---
        // The key is parented to the player after absorption, but
        // FindObjectsByType still finds it regardless of where it is in the hierarchy.
        KeyItem[] allKeys = FindObjectsByType<KeyItem>(FindObjectsSortMode.None);
        foreach (KeyItem k in allKeys)
        {
            // Only affect the key that matches this diary's keyID
            if (k.keyID != keyID) continue;

            // Stop the floating/spinning behaviour immediately
            FloatingKeyBehaviour floater = k.GetComponent<FloatingKeyBehaviour>();
            if (floater != null)
                floater.enabled = false;

            // Hide all renderers on the key and any child meshes instantly
            // so it visually disappears the moment the diary is touched
            Renderer[] renderers = k.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
                r.enabled = false;

            // Fully destroy the key GameObject after a short delay
            // so any audio events on it have time to finish playing
            Destroy(k.gameObject, 0.5f);

            break; // Only one key per diary
        }

        // Show the diary UI (full-screen image, just like the paper system)
        if (DiaryUIManager.Instance != null)
        {
            DiaryUIManager.Instance.DiaryClosed -= OnDiaryClosed;
            DiaryUIManager.Instance.DiaryClosed += OnDiaryClosed;
            DiaryUIManager.Instance.ShowDiary(diaryText, diarySprite);
        }
        else
        {
            Debug.LogError("[DiaryItem] DiaryUIManager.Instance is null! Can't show diary.");
            SwapToOpenModel();
        }
    }

    private void OnDiaryClosed()
    {
        if (DiaryUIManager.Instance != null)
            DiaryUIManager.Instance.DiaryClosed -= OnDiaryClosed;

        SwapToOpenModel();
    }

    private void SwapToOpenModel()
    {
        if (diaryClosedObject != null)
            diaryClosedObject.SetActive(false);

        if (diaryOpenObject != null)
            diaryOpenObject.SetActive(true);
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
            MemoryUIManager.Instance.ShowImage(hintImage);
        }
    }

    // -----------------------------------------------------------------------
    // Editor Visualization
    // -----------------------------------------------------------------------

    void OnDrawGizmos()
    {
        // Brown cube when locked, green when unlocked (runtime only)
        Gizmos.color = hasBeenUnlocked
            ? new Color(0f, 1f, 0f, 0.4f)
            : new Color(0.6f, 0.3f, 0.1f, 0.6f);

        Gizmos.DrawCube(transform.position, transform.localScale);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.7f,
            $"📖 Diary\nKey: {keyID}"
        );
#endif
    }
    private void ForcePlayerIdle(DustBunnyController player)
    {
        if (player == null) return;

        Animator anim = player.GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetBool("isRunning", false);
            anim.SetBool("isRolling", false);
            anim.SetBool("isGliding", false);
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}