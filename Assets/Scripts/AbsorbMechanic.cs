using UnityEngine;

/// <summary>
/// AbsorbMechanic.cs (UPDATED — now supports KeyItem)
/// 
/// This handles all absorption logic when the player rolls into a "StickyObject".
/// 
/// Supported item types (checked in this order):
/// 1. FleeingAbsorbable  — moving items with bonus growth on catch
/// 2. PaperItem          — shows full-screen paper/fragment overlay
/// 3. MemoryItem         — shows a text popup via MemoryUIManager
/// 4. KeyItem  (NEW)     — marks the key as collected so DiaryItem can unlock
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

    // -----------------------------------------------------------------------
    // References
    // -----------------------------------------------------------------------

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
                if (itemCollider != null) itemCollider.enabled = true;
                Debug.Log("Too small to absorb fleeing item yet!");
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
            Debug.Log("Too big to eat yet!");
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
        // 4. MEMORY ITEM — show text popup
        // ===================================================================
        MemoryItem memory = item.GetComponent<MemoryItem>();
        if (memory != null)
        {
            MemoryUIManager.Instance.ShowMemory(memory.memoryText, memory.textColor);
        }

        // ===================================================================
        // 5. KEY ITEM (NEW) — mark the key as collected
        //    The key sticks to the bunny visually (same as any other item),
        //    but OnAbsorbed() also sets the static flag DiaryItem checks.
        // ===================================================================
        KeyItem key = item.GetComponent<KeyItem>();
        if (key != null)
        {
            key.OnAbsorbed(); // Sets KeyItem.CollectedKeys[keyID] = true
        }

        // ===================================================================
        // 6. VISUAL ABSORPTION — attach item to bunny, shrink, randomize position
        //    This runs for ALL absorbed items (paper, memory, key, generic).
        // ===================================================================

        // Remove physics so the item doesn't fight the bunny
        Destroy(item.GetComponent<Rigidbody>());
        Destroy(item.GetComponent<Collider>());

        // Parent the item to the player
        item.transform.SetParent(this.transform);

        // Shrink the item so it looks proportional on the bunny surface
        item.transform.localScale *= absorbedItemScaleMultiplier;

        // Place it on a random spot on the bunny's surface (Katamari effect)
        item.transform.localPosition = Random.onUnitSphere * surfaceStickRadius;

        // Random rotation for a messy, organic clump look
        item.transform.localRotation = Random.rotation;

        // Grow the bunny
        transform.localScale += Vector3.one * growthFactor;

        // Play absorb sound
        if (bunnyAbsorbSfx != null) bunnyAbsorbSfx.Post(gameObject);

        Debug.Log($"[AbsorbMechanic] Absorbed: {item.name}");
    }
}