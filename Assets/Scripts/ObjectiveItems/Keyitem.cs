using UnityEngine;

/// <summary>
/// KeyItem.cs
/// 
/// Attach this component to a GameObject that represents a collectable key in the world.
/// The key is absorbed by the player just like any other StickyObject,
/// but it also sets a static flag (KeyItem.IsCollected) that the DiaryItem checks.
/// 
/// SETUP:
/// 1. Create a GameObject (e.g., a 3D key mesh or a cube for testing)
/// 2. Add a Collider and a Rigidbody
/// 3. Set the tag to "StickyObject" so AbsorbMechanic picks it up
/// 4. Add this KeyItem component
/// 5. (Optional) Assign a keyID if you want multiple different keys in your game
/// 
/// HOW IT WORKS:
/// - When AbsorbMechanic absorbs this object, OnCollisionEnter in AbsorbMechanic fires
/// - AbsorbMechanic checks for PaperItem first, then absorbs normally
/// - We hook into OnDestroy / a custom trigger here to set the flag
/// - DiaryItem polls KeyItem.IsCollected to know if it can be unlocked
/// </summary>
public class KeyItem : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector Fields
    // -----------------------------------------------------------------------

    [Header("Key Identity")]
    [Tooltip("Give each unique key its own ID so you can have multiple key/diary pairs in a scene.")]
    public string keyID = "Diary_Key_01";

    [Tooltip("Optional: Play a sound when the key is picked up (uses Wwise if assigned).")]
    public AK.Wwise.Event pickupSfx;

    [Header("Visual Feedback")]
    [Tooltip("Optional: A particle effect to spawn when the key is absorbed.")]
    public GameObject pickupParticlePrefab;

    // -----------------------------------------------------------------------
    // Static / Runtime State
    // -----------------------------------------------------------------------

    /// <summary>
    /// Simple static flag. Any DiaryItem in the scene can check this.
    /// If you have multiple key/diary pairs, use the Dictionary version below.
    /// </summary>
    public static bool IsCollected = false;

    /// <summary>
    /// Dictionary-based version for scenes with multiple key types.
    /// Key = keyID string, Value = whether it has been collected.
    /// </summary>
    public static System.Collections.Generic.Dictionary<string, bool> CollectedKeys
        = new System.Collections.Generic.Dictionary<string, bool>();

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    void Start()
    {
        // Make sure this object is tagged correctly so AbsorbMechanic can see it
        if (!CompareTag("StickyObject"))
        {
            Debug.LogWarning($"[KeyItem] '{name}' needs the tag 'StickyObject' to be absorbed!");
        }

        // Register this key as not yet collected
        if (!CollectedKeys.ContainsKey(keyID))
            CollectedKeys[keyID] = false;
    }

    /// <summary>
    /// Called by AbsorbMechanic (via a GetComponent check) the moment the player absorbs this object.
    /// AbsorbMechanic already handles attaching the visual to the player and growing the bunny;
    /// this method handles the game-logic side of collecting the key.
    /// </summary>
    public void OnAbsorbed()
    {
        // Mark the key as collected (both the simple flag and the dictionary)
        IsCollected = true;
        CollectedKeys[keyID] = true;

        Debug.Log($"[KeyItem] Key '{keyID}' collected!");

        // Activate floating behaviour so the key hovers above the bunny
        FloatingKeyBehaviour floater = GetComponent<FloatingKeyBehaviour>();

        // Optional: spawn pickup particles at the key's current position
        if (pickupParticlePrefab != null)
        {
            Instantiate(pickupParticlePrefab, transform.position, Quaternion.identity);
        }

        // Optional: play pickup sound
        if (pickupSfx != null)
        {
            pickupSfx.Post(gameObject);
        }
    }

    /// <summary>
    /// Utility: call this from a New Game / scene reset to clear collected state.
    /// </summary>
    public static void ResetAll()
    {
        IsCollected = false;
        CollectedKeys.Clear();
    }

    // -----------------------------------------------------------------------
    // Editor Visualization
    // -----------------------------------------------------------------------

    void OnDrawGizmos()
    {
        // Draw a gold cube in the editor so the key is easy to spot
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.6f);
        Gizmos.DrawCube(transform.position, transform.localScale);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.6f,
            $"🔑 Key: {keyID}"
        );
#endif
    }
}