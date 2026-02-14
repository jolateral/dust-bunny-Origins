using UnityEngine;

/// <summary>
/// AbsorbMechanic.cs (UPDATED VERSION)
/// 
/// This is an updated version of your AbsorbMechanic that now supports:
/// - MemoryItem (shows text UI overlay)
/// - PaperItem (shows full-screen paper overlay and freezes player)
/// - FleeingAbsorbable (moving items that grant bonus growth when caught)
/// 
/// Changes from original:
/// - Added check for PaperItem component
/// - Calls PaperItem.OnAbsorbed() when a paper is picked up
/// - Added support for FleeingAbsorbable bonus growth multiplier
/// </summary>
public class AbsorbMechanic : MonoBehaviour
{
    [Header("Growth Settings")]
    public float growthFactor = 0.05f; // How much to grow per item
    public float sizeTolerance = 1.2f; // Player must be this much bigger than target

    private DustBunnyController controller;

    public AK.Wwise.Event bunnyAbsorbSfx;

    void Start()
    {
        controller = GetComponent<DustBunnyController>();
    }

    void OnCollisionEnter(Collision collision)
    {
        // Only absorb if we are in Rolling Mode (Shift held down)
        if (controller.isRolling && collision.gameObject.CompareTag("StickyObject"))
        {
            AttemptAbsorb(collision.gameObject);
        }
    }

    void AttemptAbsorb(GameObject item)
    {
        // Safety check: Don't process if item is already being destroyed or inactive
        if (item == null || !item.activeSelf)
            return;
        
        // ===== Check for fleeing absorbable FIRST (before size check) =====
        // Fleeing items should be destroyed immediately on collision, regardless of size
        FleeingAbsorbable fleeing = item.GetComponent<FleeingAbsorbable>();
        if (fleeing != null)
        {
            // Disable collider immediately to prevent multiple collision events
            Collider itemCollider = item.GetComponent<Collider>();
            if (itemCollider != null)
                itemCollider.enabled = false;
            
            // Check size: Use rough bounds magnitude to compare
            float playerSize = GetComponent<Collider>().bounds.extents.magnitude;
            float fleeingItemSize = itemCollider != null ? itemCollider.bounds.extents.magnitude : 0.5f;
            
            // Only absorb if player is big enough
            if (playerSize >= fleeingItemSize * sizeTolerance)
            {
                // Fleeing items just contribute to growth and disappear immediately on collision
                float actualGrowthFactor = growthFactor * fleeing.growthMultiplier;
                Debug.Log($"Absorbed fleeing item! Bonus growth: {actualGrowthFactor} (multiplier: {fleeing.growthMultiplier}x)");
                
                // Grow player with bonus
                transform.localScale += Vector3.one * actualGrowthFactor;
                
                // Immediately destroy the fleeing item - it disappears on collision
                // Disable components first to stop any behavior
                if (fleeing != null)
                    fleeing.enabled = false;
                
                // Make invisible immediately by disabling renderers
                Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    if (renderer != null)
                        renderer.enabled = false;
                }
                
                // Disable the GameObject immediately (makes it invisible right away)
                item.SetActive(false);
                
                // Destroy the object (SetActive(false) makes it invisible immediately, Destroy removes it at end of frame)
                Destroy(item);
                Debug.Log($"Destroyed fleeing item on collision: {item.name}");
                return;
            }
            else
            {
                // Player too small - re-enable collider so they can try again later
                if (itemCollider != null)
                    itemCollider.enabled = true;
                Debug.Log("Too small to absorb fleeing item yet!");
                return;
            }
        }
        
        // Check size: Use rough bounds magnitude to compare
        float mySize = GetComponent<Collider>().bounds.extents.magnitude;
        float itemSize = item.GetComponent<Collider>().bounds.extents.magnitude;

        if (mySize >= itemSize * sizeTolerance)
        {
            // ===== NEW: Check for paper item =====
            // We check paper before memory because paper needs special handling
            PaperItem paper = item.GetComponent<PaperItem>();
            if (paper != null)
            {
                // Notify the paper that it's been absorbed
                // This will trigger the paper UI to show
                paper.OnAbsorbed();

                // Play absorb SFX
                bunnyAbsorbSfx.Post(gameObject);

                // If the paper should be destroyed after reading, we'll handle that
                // For now, we still attach it to the player like other objects
                // You can modify this behavior if you want papers to disappear immediately
            }
            
            // ===== EXISTING: Check for memory item =====
            MemoryItem memory = item.GetComponent<MemoryItem>();
            if (memory != null)
            {
                MemoryUIManager.Instance.ShowMemory(memory.memoryText, memory.textColor);
            }

            // ===== EXISTING: Absorb regular items (they stick to player) =====
            // 1. Disable physics on the item
            Destroy(item.GetComponent<Rigidbody>());
            Destroy(item.GetComponent<Collider>());

            // 2. Attach to player
            item.transform.SetParent(this.transform);
            item.transform.localPosition = Vector3.zero; // Stick directly to player center
            item.transform.localRotation = Quaternion.identity; // Reset rotation

            // 3. Grow player (regular items use base growthFactor)
            transform.localScale += Vector3.one * growthFactor;

            // Play absorb SFX
            bunnyAbsorbSfx.Post(gameObject);

            // Optional: If you want papers to be destroyed instead of absorbed
            // Uncomment the following block:
            /*
            if (paper != null && paper.destroyAfterReading)
            {
                Destroy(item);
                return;
            }
            */

            Debug.Log("Absorbed: " + item.name);
        }
        else
        {
            Debug.Log("Too big to eat yet!");
        }
    }
}