using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// AbsorbMechanic.cs (UPDATED VERSION)
/// 
/// This is an updated version of your AbsorbMechanic that now supports:
/// - Shrinking items upon absorption for better visuals
/// - Random surface placement for a better "clump" look
/// - MemoryItem (shows text UI overlay)
/// - PaperItem (shows full-screen paper overlay and freezes player)
/// - FleeingAbsorbable (moving items that grant bonus growth when caught)
/// </summary>
public class AbsorbMechanic : MonoBehaviour
{
    [Header("Growth Settings")]
    public float growthFactor = 0.01f; // How much to grow per item (scale increase per absorb)
    public float sizeTolerance = 1.2f; // Player must be this much bigger than target

    [Header("Visual Settings")]
    [Tooltip("How much the item shrinks after being absorbed (e.g., 0.3 means 30% of original size)")]
    public float absorbedItemScaleMultiplier = 0.3f;
    [Tooltip("How far from the center the item sits. Adjust this based on your bunny's base radius.")]
    public float surfaceStickRadius = 0.5f;

    [Header("Absorb Constraint")]
    public string tooBigMessage = "Too big to absorb yet!";
    public Color tooBigColor = Color.red;
    public float tooBigMessageCooldown = 1.0f;

    private float nextTooBigMessageTime = 0f;


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

    void OnCollisionStay(Collision collision)
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
                Debug.Log($"Absorbed fleeing item! Bonus growth: {actualGrowthFactor} (multiplier: {fleeing.growthMultiplier}x)");
                
                transform.localScale += Vector3.one * actualGrowthFactor;
                
                if (fleeing != null)
                    fleeing.enabled = false;
                
                Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    if (renderer != null)
                        renderer.enabled = false;
                }
                
                item.SetActive(false);
                Destroy(item);
                Debug.Log($"Destroyed fleeing item on collision: {item.name}");
                return;
            }
            else
            {
                if (itemCollider != null)
                    itemCollider.enabled = true;
                Debug.Log("Too small to absorb fleeing item yet!");
                ShowTooBigUI();
                return;
            }
        }
        
        // ===== Regular Size Check =====
        float mySize = GetComponent<Collider>().bounds.extents.magnitude;
        float itemSize = item.GetComponent<Collider>().bounds.extents.magnitude;

        if (mySize >= itemSize * sizeTolerance)
        {
            // Check for paper item
            PaperItem paper = item.GetComponent<PaperItem>();
            if (paper != null)
            {
                paper.OnAbsorbed();
                if (bunnyAbsorbSfx != null) bunnyAbsorbSfx.Post(gameObject);
            }
            
            // Check for memory item
            MemoryItem memory = item.GetComponent<MemoryItem>();
            if (memory != null)
            {
                MemoryUIManager.Instance.ShowMemory(memory.memoryText, memory.textColor);
            }

            // ===== Absorb & Visual Adjustments =====
            // 1. Disable physics on the item
            Destroy(item.GetComponent<Rigidbody>());
            Destroy(item.GetComponent<Collider>());

            // 2. Attach to player
            item.transform.SetParent(this.transform);

            // 3. SHRINK THE ITEM so it looks proportional on the dust bunny
            item.transform.localScale *= absorbedItemScaleMultiplier;

            // 4. RANDOM SURFACE PLACEMENT
            // Instead of Vector3.zero (which puts it inside the player), we place it randomly on the surface
            // This creates a cool, chaotic "Katamari" cluster effect
            item.transform.localPosition = Random.onUnitSphere * surfaceStickRadius; 
            
            // Randomize rotation so items look messily stuck together
            item.transform.localRotation = Random.rotation; 

            // 5. Grow player
            transform.localScale += Vector3.one * growthFactor;

            // Play absorb SFX
            if (bunnyAbsorbSfx != null) bunnyAbsorbSfx.Post(gameObject);

            Debug.Log("Absorbed: " + item.name);
            ObjectiveUI.Instance.SetObjective();
        }
        else
        {
            Debug.Log("Too big to absorb!");
            ShowTooBigUI();
        }
    }
    void ShowTooBigUI()
    {
        if (Time.time < nextTooBigMessageTime) return;
        nextTooBigMessageTime = Time.time + tooBigMessageCooldown;

        if (MemoryUIManager.Instance != null)
            MemoryUIManager.Instance.ShowMemory(tooBigMessage, tooBigColor);
    }
}