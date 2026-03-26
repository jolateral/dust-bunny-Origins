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
    public float growthFactor = 0.04f;

    [Tooltip("Player must be this many times bigger than the target to absorb it.")]
    public float sizeTolerance = 1.1f;

    // NEW: Smooth Growth Settings
    [Header("--- Smooth Growth Effects ---")]
    [Tooltip("Particle system to play when growing (Optional).")]
    public ParticleSystem growthParticles;
    
    [Tooltip("How long the Mario-style smooth growth takes.")]
    public float smoothGrowthDuration = 0.4f;

    [Header("Visual Settings (Orbital)")]
    [Tooltip("How much the item shrinks after being absorbed (e.g., 0.05 makes it a tiny floating speck).")]
    public float absorbedItemScaleMultiplier = 0.05f;

    [Tooltip("How far from the center the items orbit.")]
    public float surfaceStickRadius = 1.0f;

    [Tooltip("How fast the tiny items orbit around the bunny.")]
    public float orbitSpeed = 45f;

    // NEW: Orbital Height Offset
    [Tooltip("Adjust the vertical center of the orbit. Positive moves it up (e.g., to the head).")]
    public float orbitHeightOffset = 0f;

    [Header("Absorb Constraint")]
    [Tooltip("Message shown when the player is too small to absorb something.")]
    public string tooBigMessage = "You're not quite big enough yet...";
    public Sprite tooBigMessageImage;

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

    [Tooltip("Spilled items are scaled to this fraction of their original size so the player can always re-absorb them.")]
    [Range(0.3f, 0.95f)]
    public float spilledItemScaleFactor = 0.7f;

    [Header("Minimum Size Recovery")]
    [Tooltip("When at or near minimum size with nothing to absorb, passively regrow at this rate per second.")]
    public float minSizeRecoveryRate = 0.01f;

    [Tooltip("Recovery kicks in when current scale magnitude is within this ratio of the starting minimum (e.g. 1.05 = within 5%).")]
    public float recoveryThresholdRatio = 1.15f;

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

    // NEW: Private variables for smooth growth tracking
    private Vector3 currentTargetScale;
    private Coroutine growthCoroutine;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    void Start()
    {
        startingScaleMagnitude = transform.localScale.magnitude;
        playerCollider = GetComponent<Collider>();
        currentTargetScale = transform.localScale;
    }

    void Update()
    {
        // Safety-net recovery: if the bunny is near minimum size and has nothing
        // left to absorb, passively regrow so the player is never permanently stuck.
        if (droppableItems.Count == 0 &&
            transform.localScale.magnitude < startingScaleMagnitude * recoveryThresholdRatio)
        {
            float recovery = minSizeRecoveryRate * Time.deltaTime;
            transform.localScale += Vector3.one * recovery;
            currentTargetScale = transform.localScale;
        }
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

                // [ORIGINAL CODE COMMENTED OUT TO ALLOW SMOOTH GROWTH]
                // transform.localScale += Vector3.one * actualGrowthFactor;
                
                // NEW: Call the smooth growth function
                TriggerSmoothGrowth(actualGrowthFactor);

                bunnyAbsorbSfx.Post(gameObject);

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

            // Paper fragments should disappear once collected, not orbit/glow on the bunny.
            DisablePickupGlow(item);
            HideItemRenderers(item);
            item.SetActive(false);
            Destroy(item);

            TriggerSmoothGrowth(growthFactor);
            return;
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

            // [ORIGINAL CODE COMMENTED OUT TO ALLOW SMOOTH GROWTH]
            // transform.localScale += Vector3.one * growthFactor;
            
            // NEW: Call the smooth growth function
            TriggerSmoothGrowth(growthFactor);

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
        orbit.extraHeightOffset = orbitHeightOffset; // NEW: Pass the manual height offset to the orbit script

        // Track generic items so they can be spilled later on hit
        bool canDropLater = (paper == null && memory == null && key == null);
        if (canDropLater && !droppableItems.Contains(item))
        {
            droppableItems.Add(item);
            droppableOriginalScales[item] = originalScale;
        }

        // [ORIGINAL CODE COMMENTED OUT TO ALLOW SMOOTH GROWTH]
        // transform.localScale += Vector3.one * growthFactor;

        // NEW: Call the smooth growth function
        TriggerSmoothGrowth(growthFactor);

        // Play absorb sound
        if (bunnyAbsorbSfx != null) bunnyAbsorbSfx.Post(gameObject);

        Debug.Log("Absorbed: " + item.name);

        // Notify the objective UI that something was collected
        ObjectiveUI.Instance.SetObjective();
    }

    // -----------------------------------------------------------------------
    // UI Helpers
    // -----------------------------------------------------------------------

    void ShowTooBigUI()
    {
        if (Time.time < nextTooBigMessageTime) return;
        nextTooBigMessageTime = Time.time + tooBigMessageCooldown;

        if (MemoryUIManager.Instance != null)
            MemoryUIManager.Instance.ShowImage(tooBigMessageImage);
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

            // Restore a reduced version of the item's original size so the player
            // can always re-absorb it after shrinking from the spill.
            if (droppableOriginalScales.TryGetValue(item, out Vector3 originalScale))
            {
                item.transform.localScale = originalScale * spilledItemScaleFactor;
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

        // NEW: Sync the currentTargetScale so future growth starts from the correct spilled size
        currentTargetScale = scale;
    }

    private IEnumerator ReenableCollisionLater(Collider itemCollider)
    {
        yield return new WaitForSeconds(0.4f);

        if (playerCollider != null && itemCollider != null)
        {
            Physics.IgnoreCollision(playerCollider, itemCollider, false);
        }
    }

    /// <summary>
    /// Disables pickup highlight/glow components once an item is collected.
    /// </summary>
    private void DisablePickupGlow(GameObject item)
    {
        if (item == null) return;

        Outline[] outlines = item.GetComponentsInChildren<Outline>(true);
        foreach (Outline outline in outlines)
        {
            if (outline != null) outline.enabled = false;
        }
    }

    /// <summary>
    /// Hides all renderers for an item and its children.
    /// </summary>
    private void HideItemRenderers(GameObject item)
    {
        if (item == null) return;

        Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r != null) r.enabled = false;
        }
    }

    // =======================================================================
    // NEW: SMOOTH GROWTH COROUTINE & LOGIC (Mario Mushroom Effect)
    // =======================================================================

    /// <summary>
    /// Updates the target scale and restarts the smooth scaling coroutine.
    /// Also fires the particle system if assigned.
    /// </summary>
    private void TriggerSmoothGrowth(float addedGrowth)
    {
        currentTargetScale += Vector3.one * addedGrowth;

        // Play particles if assigned
        if (growthParticles != null)
        {
            growthParticles.Play();
        }

        // Restart the scaling coroutine to interpolate to the new target
        if (growthCoroutine != null)
        {
            StopCoroutine(growthCoroutine);
        }
        growthCoroutine = StartCoroutine(SmoothScaleRoutine());
    }

    /// <summary>
    /// Coroutine that smoothly scales the bunny using an Ease-Out-Back formula,
    /// creating a bouncy "Mario Mushroom" effect.
    /// </summary>
    private IEnumerator SmoothScaleRoutine()
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < smoothGrowthDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / smoothGrowthDuration;

            // Classic Ease-Out-Back math formula for bouncy overshoot
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float ease = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

            transform.localScale = Vector3.LerpUnclamped(startScale, currentTargetScale, ease);
            yield return null;
        }

        // Ensure we end exactly on the target scale
        transform.localScale = currentTargetScale;
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
    
    // NEW: Manual offset for orbit height (set via AbsorbMechanic)
    public float extraHeightOffset = 0f; 
    
    // NEW: Physics Wobble Settings for organic floating feedback
    [Header("Physics Wobble")]
    public float springStiffness = 120f;
    public float dampening = 8f;
    public float inertiaFactor = 0.8f; // How much it reacts to the player's movement
    
    // NEW: Absorption Trajectory Settings
    [Header("Absorption Trajectory")]
    public float absorbDuration = 0.4f; // How long it takes to fly into orbit like a vacuum
    
    private Vector3 axis;
    private float angle;
    private float heightOffset;

    // Variables to track target movement and calculate wobble
    private Vector3 lastTargetPos;
    private Vector3 wobbleOffset;
    private Vector3 wobbleVelocity;
    
    // NEW: State variables for the initial "suck-in" flight path
    private Vector3 initialWorldPosition;
    private float absorbTimer = 0f;
    private bool isAbsorbing = true;

    void Start()
    {
        // Give each item a random starting angle and rotation axis
        axis = Random.onUnitSphere;
        angle = Random.Range(0f, 360f);

        if (target != null)
        {
            Collider col = target.GetComponent<Collider>();
            if (col != null) heightOffset = col.bounds.extents.y;
            
            lastTargetPos = target.position;
        }
        
        // Record the exact position where the item was when the player touched it
        initialWorldPosition = transform.position;
    }

    void LateUpdate()
    {
        if (target == null) return;

        angle += speed * Time.deltaTime;
        
        // Calculate the ideal pure-orbit offset
        Vector3 idealOrbitOffset = Quaternion.AngleAxis(angle, axis) * Vector3.forward * radius;
        
        // WOBBLE PHYSICS CALCULATION
        Vector3 currentTargetPos = target.position;
        Vector3 targetMovement = currentTargetPos - lastTargetPos;
        
        // 1. Inertia: when the bunny moves, push the wobble offset in the opposite direction
        wobbleOffset -= targetMovement * inertiaFactor;
        
        // 2. Spring force: pull the wobble offset back to zero (creates the bounce when stopping)
        Vector3 springForce = -springStiffness * wobbleOffset - dampening * wobbleVelocity;
        wobbleVelocity += springForce * Time.deltaTime;
        wobbleOffset += wobbleVelocity * Time.deltaTime;

        // NEW: Apply the extraHeightOffset here to manually raise or lower the entire orbit ring
        Vector3 targetOrbitPos = currentTargetPos + (Vector3.up * (heightOffset + extraHeightOffset)) + idealOrbitOffset + wobbleOffset;

        // Trajectory Logic (Vacuum suck-in effect)
        if (isAbsorbing)
        {
            absorbTimer += Time.deltaTime;
            
            // Normalize time from 0 to 1
            float t = Mathf.Clamp01(absorbTimer / absorbDuration);

            // Use an Ease-In Cubic curve (starts slow, snaps into orbit quickly to simulate a vacuum suck)
            float ease = t * t * t;

            // Smoothly move from its original grounded spot into the moving orbit path
            transform.position = Vector3.LerpUnclamped(initialWorldPosition, targetOrbitPos, ease);

            if (t >= 1f)
            {
                isAbsorbing = false; // Finished flying, lock into permanent orbit
            }
        }
        else
        {
            // Follow the target perfectly (prevents drop desync) + apply orbit + apply wobble
            transform.position = targetOrbitPos;
        }

        // Make the item itself spin slightly while orbiting
        transform.Rotate(axis, speed * 1.5f * Time.deltaTime);
        
        // Update last position for the next frame
        lastTargetPos = currentTargetPos;
    }
}