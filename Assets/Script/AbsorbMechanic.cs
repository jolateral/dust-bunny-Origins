using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// AbsorbMechanic.cs (UPDATED VERSION)
/// 
/// This is an updated version of your AbsorbMechanic that now supports:
/// - MemoryItem (shows text UI overlay)
/// - PaperItem (shows full-screen paper overlay and freezes player)
/// 
/// Changes from original:
/// - Added check for PaperItem component
/// - Calls PaperItem.OnAbsorbed() when a paper is picked up
/// </summary>
public class AbsorbMechanic : MonoBehaviour
{
    [Header("Growth Settings")]
    public float growthFactor = 0.05f; // How much to grow per item
    public float sizeTolerance = 1.2f; // Player must be this much bigger than target

    [Header("Audio Sources")]
    public AudioSource absorbSource;

    [Header("Audio Resources")]
    public AudioResource bunnyAbsorb;

    private DustBunnyController controller;

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
        // Check size: Use rough bounds magnitude to compare
        float mySize = GetComponent<Collider>().bounds.extents.magnitude;
        float itemSize = item.GetComponent<Collider>().bounds.extents.magnitude;

        if (mySize >= itemSize * sizeTolerance)
        {
            // ===== NEW: Check for paper item FIRST =====
            // We check paper before memory because paper needs special handling
            PaperItem paper = item.GetComponent<PaperItem>();
            if (paper != null)
            {
                // Notify the paper that it's been absorbed
                // This will trigger the paper UI to show
                paper.OnAbsorbed();
                
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

            // ===== EXISTING: Absorb the item =====
            // 1. Disable physics on the item
            Destroy(item.GetComponent<Rigidbody>());
            Destroy(item.GetComponent<Collider>());

            // 2. Attach to player
            item.transform.SetParent(this.transform);

            // 3. Grow player
            transform.localScale += Vector3.one * growthFactor;

            absorbSource.resource = bunnyAbsorb;
            absorbSource.Play();
            
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