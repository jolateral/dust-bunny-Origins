using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// AbsorbMechanic.cs (UPDATED — now supports KeyItem & Orbital Floating)
/// 
/// This handles all absorption logic when the player touches a "StickyObject" (absorb on contact).
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
    public float growthFactor = 0.02f;

    [Tooltip("Player must be this many times bigger than the target to absorb it.")]
    public float sizeTolerance = 1.1f;

    [Header("Visual Settings (Orbital)")]
    [Tooltip("How much the item shrinks after being absorbed (e.g., 0.05 makes it a tiny floating speck).")]
    public float absorbedItemScaleMultiplier = 0.05f;

    [Tooltip("How far from the center the items orbit.")]
    public float surfaceStickRadius = 1.0f;

    [Tooltip("How fast the tiny items orbit around the bunny.")]
    public float orbitSpeed = 45f;

    [Header("Absorb Constraint")]
    [Tooltip("Message shown when the player is too small to absorb something.")]
    public string tooBigMessage = "You're not quite big enough yet...";

    [Tooltip("Color of the too-small hint message.")]
    public Color tooBigColor = Color.red;

    [Tooltip("How many seconds before the too-small hint can show again.")]
    public float tooBigMessageCooldown = .075f;

    [Header("Spill Settings")]
    [Tooltip("How many generic absorbed items can be lost in a single hit at most.")]
    public int maxItemsLostOnHit = 3;

    [Tooltip("Horizontal strength of the force applied to spilled items.")]
    public float spillForce = 3f;

    [Tooltip("Upward strength of the force applied to spilled items.")]
    public float spillUpwardForce = 2f;

    // -----------------------------------------------------------------------
    // Private Fields
    // -----------------------------------------------------------------------

    /// <summary>Timestamp of the last time the too-small message was shown.</summary>
    private float nextTooBigMessageTime = 0f;

    /// <summary>Absorbed items that can be spilled when the player takes a hit.</summary>
    private readonly System.Collections.Generic.List<GameObject> droppableItems = new System.Collections.Generic.List<GameObject>();

    /// <summary>Original (pre-absorption) local scale for each droppable item.</summary>
    private readonly System.Collections.Generic.Dictionary<GameObject, Vector3> droppableOriginalScales =
        new System.Collections.Generic.Dictionary<GameObject, Vector3>();

    /// <summary>Minimum scale we allow the bunny to shrink back to when spilling.</summary>
    private float startingScaleMagnitude;

    /// <summary>Cached reference to the player's collider for spill collision ignore.</summary>
    private Collider playerCollider;

    public AK.Wwise.Event bunnyAbsorbSfx;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    void Start()
    {
        // Assume fairly uniform scaling; track a baseline so we don't shrink below it.
        startingScaleMagnitude = transform.localScale.magnitude;
        playerCollider = GetComponent<Collider>();
    }

    void OnCollisionEnter(Collision collision)
    {
        // Absorb on contact — touching any absorbable (StickyObject) absorbs it
        if (collision.gameObject.CompareTag("StickyObject"))
        {
            AttemptAbsorb(collision.gameObject);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("StickyObject"))
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
        // ===================================================================
        KeyItem key = item.GetComponent<KeyItem>();
        if (key != null)
        {
            key.OnAbsorbed(); // FIXED ERROR CS1061

            // Handle physics and parenting first
            Destroy(item.GetComponent<Rigidbody>());
            Destroy(item.GetComponent<Collider>());
            item.transform.SetParent(this.transform);

            // NOW activate floating — parent is valid at this point
            FloatingKeyBehaviour floater = item.GetComponent<FloatingKeyBehaviour>();
            if (floater != null)
                floater.StartFloating(this.transform);

            // Grow bunny and play sound
            transform.localScale += Vector3.one * growthFactor;
            if (bunnyAbsorbSfx != null) bunnyAbsorbSfx.Post(gameObject);
            ObjectiveUI.Instance.SetObjective();
            return;
        }

        // ===================================================================
        // 6. VISUAL ABSORPTION — Extreme shrink + Orbital Floating
        // ===================================================================

        // Remove physics so the item doesn't fight the bunny's movement
        Destroy(item.GetComponent<Rigidbody>());
        Destroy(item.GetComponent<Collider>());

        // Parent the item to the player so it travels with the bunny
        item.transform.SetParent(this.transform);

        // Remember the item's original scale before we shrink it
        Vector3 originalScale = item.transform.localScale;

        // Shrink the item drastically so it looks like a tiny floating particle
        item.transform.localScale *= absorbedItemScaleMultiplier;

        // Add the Orbit script so it continuously floats around the bunny
        OrbitBehaviour orbit = item.AddComponent<OrbitBehaviour>();
        orbit.target = this.transform;
        orbit.radius = surfaceStickRadius;
        orbit.speed = orbitSpeed;

        // Track generic items so they can be spilled later on hit
        bool canDropLater = (paper == null && memory == null && key == null);
        if (canDropLater && !droppableItems.Contains(item))
        {
            droppableItems.Add(item);
            droppableOriginalScales[item] = originalScale;
        }

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

    void ShowTooBigUI()
    {
        if (Time.time < nextTooBigMessageTime) return;
        nextTooBigMessageTime = Time.time + tooBigMessageCooldown;

        if (MemoryUIManager.Instance != null)
            MemoryUIManager.Instance.ShowMemory(tooBigMessage, tooBigColor);
    }

    // -----------------------------------------------------------------------
    // Spill Logic
    // -----------------------------------------------------------------------

    public void SpillAbsorbables(int requestedCount)
    {
        if (requestedCount <= 0 || droppableItems.Count == 0)
            return;

        int maxAllowed = Mathf.Max(1, maxItemsLostOnHit);
        int spillCount = Mathf.Min(requestedCount, maxAllowed);
        spillCount = Mathf.Min(spillCount, droppableItems.Count);

        int actuallySpilled = 0;

        for (int i = 0; i < spillCount; i++)
        {
            if (droppableItems.Count == 0)
                break;

            int idx = Random.Range(0, droppableItems.Count);
            GameObject item = droppableItems[idx];
            droppableItems.RemoveAt(idx);

            if (item == null)
                continue;

            // Restore the size the item had out in the world before absorption.
            if (droppableOriginalScales.TryGetValue(item, out Vector3 originalScale))
            {
                item.transform.localScale = originalScale;
                droppableOriginalScales.Remove(item);
            }

            actuallySpilled++;

            // Detach so it no longer follows the bunny.
            item.transform.SetParent(null);

            // REMOVE ORBIT BEHAVIOUR so it stops floating when spilled
            OrbitBehaviour orbit = item.GetComponent<OrbitBehaviour>();
            if (orbit != null) Destroy(orbit);

            // Make sure it can be absorbed again.
            item.tag = "StickyObject";

            // Restore physics: give it a collider and rigidbody if missing.
            Collider col = item.GetComponent<Collider>();
            if (col == null)
                col = item.AddComponent<BoxCollider>();
            col.enabled = true;

            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb == null)
                rb = item.AddComponent<Rigidbody>();
            rb.isKinematic = false;

            if (playerCollider != null && col != null)
            {
                Physics.IgnoreCollision(playerCollider, col, true);
                StartCoroutine(ReenableCollisionLater(col));
            }

            Vector3 fromCenter = (item.transform.position - transform.position);
            fromCenter.y = 0f;
            if (fromCenter.sqrMagnitude < 0.01f)
                fromCenter = Random.insideUnitSphere;

            fromCenter.y = 0f;
            fromCenter.Normalize();

            Vector3 force = fromCenter * spillForce + Vector3.up * spillUpwardForce;
            rb.AddForce(force, ForceMode.Impulse);
        }

        if (actuallySpilled <= 0)
            return;

        float totalLoss = growthFactor * actuallySpilled;
        Vector3 scale = transform.localScale - Vector3.one * totalLoss;

        float minMagnitude = startingScaleMagnitude;
        if (scale.magnitude < minMagnitude)
        {
            if (scale.magnitude > 0f)
            {
                float factor = minMagnitude / scale.magnitude;
                scale *= factor;
            }
            else
            {
                scale = Vector3.one * (minMagnitude / Mathf.Sqrt(3f));
            }
        }

        transform.localScale = scale;
    }

    private IEnumerator ReenableCollisionLater(Collider itemCollider)
    {
        yield return new WaitForSeconds(0.4f);

        if (playerCollider != null && itemCollider != null)
        {
            Physics.IgnoreCollision(playerCollider, itemCollider, false);
        }
    }
}

/// <summary>
/// A lightweight component attached at runtime to make absorbed items orbit the player.
/// </summary>
public class OrbitBehaviour : MonoBehaviour
{
    public Transform target;
    public float radius = 1f;
    public float speed = 45f;
    
    private Vector3 axis;
    private float angle;
    private float heightOffset;

    void Start()
    {
        // Give each item a random starting angle and rotation axis
        axis = Random.onUnitSphere;
        angle = Random.Range(0f, 360f);

        // Calculate the physical center of the target so they orbit the body, not the feet
        if (target != null)
        {
            Collider col = target.GetComponent<Collider>();
            if (col != null) heightOffset = col.bounds.extents.y;
        }
    }

    void Update()
    {
        if (target == null) return;

        angle += speed * Time.deltaTime;
        
        // Calculate orbital position
        Vector3 offset = Quaternion.AngleAxis(angle, axis) * Vector3.forward * radius;
        
        // Follow the target and apply the orbit
        transform.position = target.position + (Vector3.up * heightOffset) + offset;

        // Make the item itself spin slightly while orbiting
        transform.Rotate(axis, speed * 1.5f * Time.deltaTime);
    }
}