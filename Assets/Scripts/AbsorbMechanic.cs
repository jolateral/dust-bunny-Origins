using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// AbsorbMechanic.cs (UPDATED — now supports KeyItem)
/// 
/// This handles all absorption logic when the player rolls into a "StickyObject".
/// 
/// Supported item types (checked in this order):
/// 1. FleeingAbsorbable  — moving items with bonus growth on catch
/// 2. PaperItem          — shows full-screen paper/fragment overlay
/// 3. MemoryItem         — shows a text popup via MemoryUIManager
/// 4. KeyItem            — marks the key as collected so DiaryItem can unlock
/// 5. Generic StickyObject — just gets absorbed and grows the bunny
/// </summary>
public class AbsorbMechanic : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector Settings
    // -----------------------------------------------------------------------

    [Header("Growth Settings")]
    [Tooltip("How much the bunny grows per absorbed item.")]
    public float growthFactor = 0.05f;

    [Tooltip("Player must be this many times bigger than the target to absorb it.")]
    public float sizeTolerance = 1.2f;

    [Header("Visual Settings")]
    [Tooltip("How much the item shrinks after being absorbed (0.3 = 30% of original size).")]
    public float absorbedItemScaleMultiplier = 0.3f;

    [Tooltip("How far from the center the item sits on the bunny surface.")]
    public float surfaceStickRadius = 0.5f;

    [Header("Absorb Constraint")]
    [Tooltip("Message shown when the player is too small to absorb something.")]
    public string tooBigMessage = "You're not quite big enough yet...";

    [Tooltip("Color of the too-small hint message.")]
    public Color tooBigColor = Color.red;

    [Tooltip("How many seconds before the too-small hint can show again.")]
    public float tooBigMessageCooldown = .075f;

    // -----------------------------------------------------------------------
    // Private Fields
    // -----------------------------------------------------------------------

    /// <summary>Timestamp of the last time the too-small message was shown.</summary>
    private float nextTooBigMessageTime = 0f;

    private DustBunnyController controller;

    public AK.Wwise.Event bunnyAbsorbSfx;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    void Start()
    {
        controller = GetComponent<DustBunnyController>();
    }

    void OnCollisionEnter(Collision collision)
    {
        // Only absorb when the player is in Rolling Mode (dash/Shift held)
        if (controller.isRolling && collision.gameObject.CompareTag("StickyObject"))
        {
            AttemptAbsorb(collision.gameObject);
        }
    }
    void OnCollisionStay(Collision collision)
    {
        // Only absorb when the player is in Rolling Mode (dash/Shift held)
        if (controller.isRolling && collision.gameObject.CompareTag("StickyObject"))
        {
            AttemptAbsorb(collision.gameObject);
        }
    }

    // -----------------------------------------------------------------------
    // Core Absorption Logic
    // -----------------------------------------------------------------------

    void AttemptAbsorb(GameObject item)
    {
        // Safety: skip if object is already gone
        if (item == null || !item.activeSelf)
            return;

        // ===================================================================
        // 1. FLEEING ABSORBABLE — handled before size check (has its own rules)
        // ===================================================================
        FleeingAbsorbable fleeing = item.GetComponent<FleeingAbsorbable>();
        if (fleeing != null)
        {
            Collider itemCollider = item.GetComponent<Collider>();
            if (itemCollider != null)
                itemCollider.enabled = false;

            float playerSize = GetComponent<Collider>().bounds.extents.magnitude;
            float fleeingItemSize = itemCollider != null ? itemCollider.bounds.extents.magnitude : 0.5f;

            if (playerSize >= fleeingItemSize * sizeTolerance)
            {
                float actualGrowthFactor = growthFactor * fleeing.growthMultiplier;
                Debug.Log($"Absorbed fleeing item! Bonus growth: {actualGrowthFactor} (x{fleeing.growthMultiplier})");

                transform.localScale += Vector3.one * actualGrowthFactor;

                if (fleeing != null) fleeing.enabled = false;

                // Hide renderers before destroying (avoids visual pop)
                Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers)
                    if (r != null) r.enabled = false;

                item.SetActive(false);
                Destroy(item);
                return;
            }
            else
            {
                // Re-enable collider so the item keeps working
                if (itemCollider != null) itemCollider.enabled = true;
                Debug.Log("Too small to absorb fleeing item yet!");
                ShowTooBigUI();
                return;
            }
        }

        // ===================================================================
        // 2. SIZE CHECK — must be big enough to absorb the target
        // ===================================================================
        float mySize   = GetComponent<Collider>().bounds.extents.magnitude;
        float itemSize = item.GetComponent<Collider>().bounds.extents.magnitude;

        if (mySize < itemSize * sizeTolerance)
        {
            Debug.Log("Too small to absorb yet!");
            ShowTooBigUI();
            return;
        }

        // ===================================================================
        // 3. PAPER ITEM — trigger paper/fragment UI overlay
        // ===================================================================
        PaperItem paper = item.GetComponent<PaperItem>();
        if (paper != null)
        {
            paper.OnAbsorbed();
            if (bunnyAbsorbSfx != null) bunnyAbsorbSfx.Post(gameObject);
        }

        // ===================================================================
        // 4. MEMORY ITEM — show text popup via MemoryUIManager
        // ===================================================================
        MemoryItem memory = item.GetComponent<MemoryItem>();
        if (memory != null)
        {
            MemoryUIManager.Instance.ShowMemory(memory.memoryText, memory.textColor);
        }

        // ===================================================================
        // 5. KEY ITEM — mark the key as collected
        //    The key sticks to the bunny visually (same as any other item),
        //    but OnAbsorbed() also sets the flag that DiaryItem checks.
        // ===================================================================
        KeyItem key = item.GetComponent<KeyItem>();
        if (key != null)
        {
            key.OnAbsorbed(); // Sets KeyItem.CollectedKeys[keyID] = true
        }

        // ===================================================================
        // 6. VISUAL ABSORPTION — attach item to bunny, shrink, randomize position
        //    This runs for ALL successfully absorbed items.
        // ===================================================================

        // Remove physics so the item doesn't fight the bunny's movement
        Destroy(item.GetComponent<Rigidbody>());
        Destroy(item.GetComponent<Collider>());

        // Parent the item to the player so it travels with the bunny
        item.transform.SetParent(this.transform);

        // Shrink the item so it looks proportional stuck on the bunny surface
        item.transform.localScale *= absorbedItemScaleMultiplier;

        // Place it on a random spot on the bunny's surface (Katamari clump effect)
        item.transform.localPosition = Random.onUnitSphere * surfaceStickRadius;

        // Random rotation so items look messily stuck together
        item.transform.localRotation = Random.rotation;

        // Grow the bunny
        transform.localScale += Vector3.one * growthFactor;

        // Play absorb sound
        if (bunnyAbsorbSfx != null) bunnyAbsorbSfx.Post(gameObject);

        Debug.Log("Absorbed: " + item.name);

        // Notify the objective UI that something was collected
        ObjectiveUI.Instance.SetObjective();

        // Set bunny-size parameter for audio variation
        AkUnitySoundEngine.SetRTPCValue("bunny-size", mySize, gameObject);
    }

    // -----------------------------------------------------------------------
    // UI Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Shows the "too small" hint message via MemoryUIManager.
    /// Respects a cooldown so the message doesn't spam every frame.
    /// </summary>
    void ShowTooBigUI()
    {
        if (Time.time < nextTooBigMessageTime) return;
        nextTooBigMessageTime = Time.time + tooBigMessageCooldown;

        if (MemoryUIManager.Instance != null)
            MemoryUIManager.Instance.ShowMemory(tooBigMessage, tooBigColor);
    }
}