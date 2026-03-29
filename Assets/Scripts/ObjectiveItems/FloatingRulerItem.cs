using System.Collections;
using UnityEngine;

/// <summary>
/// FloatingArtKitBadge.cs  (NEW)
///
/// Attach this to the Badge prefab that floats above the player after
/// they view the art kit. The behaviour is identical to FloatingKeyBehaviour
/// (bob + spin above the bunny's head) with one addition: a FadeOutAndDestroy()
/// coroutine that BridgeUnlockTrigger calls when the player reaches the bridge.
///
/// SETUP:
///   1. Create a small 3D GameObject (paint palette, star, icon, etc.)
///   2. Make sure it has a Renderer (MeshRenderer or SpriteRenderer).
///   3. Add this script.
///   4. Make sure the material uses a shader that supports alpha (e.g. Universal
///      Render Pipeline / Lit with Surface Type = Transparent, or Standard / Fade).
///   5. Assign this prefab to ArtKitItem.floatingBadgePrefab in the Inspector.
///
/// IMPORTANT — Physics:
///   The badge has NO Collider and NO Rigidbody by design so it never
///   interferes with the player's movement or gets caught on geometry.
///   It is purely visual.
/// </summary>
public class FloatingRulerItem : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector Fields
    // -----------------------------------------------------------------------

    [Header("Float Settings")]
    [Tooltip("How high above the bunny's center the badge hovers (world units).")]
    public float floatHeight = 1.5f;

    [Tooltip("How far the badge bobs up and down from its base height.")]
    public float bobAmplitude = 0.12f;

    [Tooltip("How fast the badge bobs (cycles per second).")]
    public float bobSpeed = 2f;

    [Header("Spin Settings")]
    [Tooltip("Degrees per second the badge spins on its Y axis.")]
    public float spinSpeed = 120f;

    [Header("Fade Out Settings")]
    [Tooltip("How many seconds the badge takes to fade to invisible before it is destroyed.")]
    public float fadeOutDuration = 0.8f;

    [Tooltip("How far upward (world units) the badge drifts while fading out.")]
    public float fadeRiseDistance = 0.5f;

    // -----------------------------------------------------------------------
    // Private State
    // -----------------------------------------------------------------------

    /// <summary>True once StartFloating() has been called.</summary>
    private bool isFloating = false;

    /// <summary>The player's transform — the badge positions itself relative to this.</summary>
    private Transform parentTransform;

    /// <summary>Cached renderers used during the fade-out.</summary>
    private Renderer[] renderers;

    /// <summary>True once FadeOutAndDestroy() has started (prevents Update repositioning fighting the fade).</summary>
    private bool isFadingOut = false;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    private void Awake()
    {
        // Cache all renderers up front so the fade coroutine doesn't need to search
        renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    private void Update()
    {
        // Do nothing until StartFloating() is called, and stop moving once fading begins
        if (!isFloating || isFadingOut || parentTransform == null) return;

        // Scale the height and bob with the bunny's scale so it stays proportional
        // as the bunny grows from absorbing objects
        float avgScale = (parentTransform.localScale.x
                        + parentTransform.localScale.y
                        + parentTransform.localScale.z) / 3f;

        float scaledHeight = floatHeight * avgScale;
        float scaledBob    = bobAmplitude * avgScale;

        // Sinusoidal bob
        float bob = Mathf.Sin(Time.time * bobSpeed) * scaledBob;

        // Position directly above the player's pivot
        transform.position = parentTransform.position + Vector3.up * (scaledHeight + bob);

        // Spin on the Y axis (world space so it doesn't inherit bunny rotation quirks)
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called by ArtKitItem after the badge is instantiated and parented to the player.
    /// Activates the float/bob/spin loop.
    /// </summary>
    public void StartFloating(Transform bunnyTransform)
    {
        isFloating      = true;
        parentTransform = bunnyTransform;
    }

    /// <summary>
    /// Called by BridgeUnlockTrigger when the player enters the unlock zone.
    /// The badge rises and fades to transparent, then destroys itself.
    /// The bridge fade-in is handled independently by BridgeUnlockTrigger.
    /// </summary>
    public void FadeOutAndDestroy()
    {
        if (isFadingOut) return;   // Guard against double-calls

        isFadingOut = true;
        StartCoroutine(FadeOutRoutine());
    }

    // -----------------------------------------------------------------------
    // Private Coroutines
    // -----------------------------------------------------------------------

    /// <summary>
    /// Smoothly reduces all renderer material alphas to 0 while drifting the
    /// badge upward, then destroys the GameObject.
    /// </summary>
    private IEnumerator FadeOutRoutine()
    {
        // Detach from the player so the badge stays put while it fades
        // (the player can keep moving; the badge hangs in the air and dissolves)
        transform.SetParent(null);

        Vector3 startPos = transform.position;
        Vector3 endPos   = startPos + Vector3.up * fadeRiseDistance;

        float elapsed = 0f;

        // Collect all materials we need to modify
        // We use MaterialPropertyBlock to avoid permanently editing shared materials
        Material[] mats = GatherMaterials();

        // Store each material's original color so we can lerp the alpha
        Color[] startColors = new Color[mats.Length];
        for (int i = 0; i < mats.Length; i++)
        {
            // Try common color property names (works for URP Lit, Standard, Unlit)
            if (mats[i].HasProperty("_BaseColor"))
                startColors[i] = mats[i].GetColor("_BaseColor");
            else if (mats[i].HasProperty("_Color"))
                startColors[i] = mats[i].GetColor("_Color");
            else
                startColors[i] = Color.white;
        }

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);

            // Ease-out so it disappears quickly at the start and softly at the end
            float alpha = Mathf.Lerp(1f, 0f, t * t);

            // Rise smoothly
            transform.position = Vector3.Lerp(startPos, endPos, t);

            // Apply the new alpha to every material
            for (int i = 0; i < mats.Length; i++)
            {
                Color c = startColors[i];
                c.a = alpha;

                if (mats[i].HasProperty("_BaseColor"))
                    mats[i].SetColor("_BaseColor", c);
                else if (mats[i].HasProperty("_Color"))
                    mats[i].SetColor("_Color", c);
            }

            yield return null;
        }

        // Clean up — destroy the whole badge GameObject
        Destroy(gameObject);
    }

    /// <summary>
    /// Collects all unique material instances from every renderer in the hierarchy.
    /// Using renderer.material (not sharedMaterial) so we get per-instance copies
    /// and never corrupt the original shared material asset.
    /// </summary>
    private Material[] GatherMaterials()
    {
        System.Collections.Generic.List<Material> list =
            new System.Collections.Generic.List<Material>();

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            // renderer.materials returns per-instance copies (safe to modify)
            foreach (Material m in r.materials)
            {
                if (m != null && !list.Contains(m))
                    list.Add(m);
            }
        }

        return list.ToArray();
    }
}