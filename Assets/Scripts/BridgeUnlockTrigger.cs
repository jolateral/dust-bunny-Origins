using System.Collections;
using UnityEngine;

/// <summary>
/// BridgeUnlockTrigger.cs  (NEW)
///
/// Place this script on an invisible trigger zone (a GameObject with a Collider
/// set to "Is Trigger = true") that straddles the path to the bridge.
///
/// WHAT IT DOES:
///   When the player walks into the trigger zone:
///     a) If they have NOT viewed the art kit  → show a hint and block passage
///        (no actual physics barrier — the bridge itself is the visual blocker).
///     b) If they HAVE viewed the art kit      → begin the unlock sequence:
///          1. The floating badge above the player fades out (FloatingArtKitBadge.FadeOutAndDestroy)
///          2. The bridge GameObject fades in over 'bridgeFadeInDuration' seconds
///          3. The trigger disables itself so the sequence never fires again.
///
/// SETUP STEPS:
///   1. Create an empty GameObject where the bridge approach is.
///   2. Add a BoxCollider, check "Is Trigger".
///   3. Size the collider to cover the approach path (make it wide enough that
///      the player can't squeeze past without entering it).
///   4. Set the GameObject's Layer to "Ignore Raycast" (or any non-physics layer)
///      so it doesn't interfere with anything.
///   5. Attach this script.
///   6. Assign bridgeObject — the plane/bridge mesh that starts invisible (alpha = 0
///      or inactive depending on your chosen fadeMode).
///   7. (Optional) Assign hintText / hintImage for the "not yet" message.
///
/// BRIDGE FADE MODES:
///   - FadeRenderers : the bridge is ACTIVE but fully transparent; this script
///                     fades its material alpha from 0 → 1. Requires a transparent
///                     material on the bridge (URP Lit / Surface Type = Transparent
///                     or Standard / Rendering Mode = Fade).
///   - ActivateOnly  : the bridge starts as an INACTIVE GameObject; this script
///                     simply calls SetActive(true) immediately (no fade).
///                     Use this if your bridge material doesn't support alpha fading.
/// </summary>
public class BridgeUnlockTrigger : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Enums
    // -----------------------------------------------------------------------

    public enum FadeMode
    {
        FadeRenderers,  // Bridge is active + transparent; fades material alpha 0 → 1
        ActivateOnly    // Bridge starts inactive; just activates it (no alpha fade)
    }

    // -----------------------------------------------------------------------
    // Inspector Fields
    // -----------------------------------------------------------------------

    [Header("Bridge Reference")]
    [Tooltip("The bridge (plane) GameObject.\n" +
             "FadeRenderers mode: must be ACTIVE but with a transparent material at alpha=0.\n" +
             "ActivateOnly mode: must be INACTIVE (SetActive false) in the scene.")]
    public GameObject bridgeObject;

    [Header("Fade Settings")]
    [Tooltip("How the bridge appears.\n" +
             "FadeRenderers = smoothly fades material alpha.\n" +
             "ActivateOnly  = instantly activates (no fade).")]
    public FadeMode fadeMode = FadeMode.FadeRenderers;

    [Tooltip("Seconds the bridge takes to fade from invisible to fully opaque.\n" +
             "Only used in FadeRenderers mode.")]
    public float bridgeFadeInDuration = 1.5f;

    [Header("Hint (player not ready)")]
    [Tooltip("Message shown via MemoryUIManager when the player enters without having viewed the art kit.\n" +
             "Leave empty to show no hint.")]
    public string lockedHintText = "I need to check the art kit first...";

    [Tooltip("Optional sprite shown alongside the hint text.")]
    public Sprite lockedHintSprite;

    [Tooltip("Seconds before the locked hint can show again (prevents spam).")]
    public float hintCooldown = 3f;

    [Header("Audio")]
    [Tooltip("Optional Wwise event played when the bridge successfully unlocks.")]
    public AK.Wwise.Event bridgeUnlockSfx;

    // -----------------------------------------------------------------------
    // Private State
    // -----------------------------------------------------------------------

    /// <summary>Whether the bridge has already been unlocked this session.</summary>
    private bool hasUnlocked = false;

    /// <summary>Timestamp of the last time the locked hint was shown.</summary>
    private float lastHintTime = -99f;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    private void Start()
    {
        ValidateSetup();

        // In FadeRenderers mode the bridge is already active but must start invisible
        if (fadeMode == FadeMode.FadeRenderers && bridgeObject != null)
            SetBridgeAlpha(0f);

        // In ActivateOnly mode, make sure the bridge starts inactive
        if (fadeMode == FadeMode.ActivateOnly && bridgeObject != null)
            bridgeObject.SetActive(false);
    }

    /// <summary>
    /// OnTriggerEnter fires when ANY collider enters this trigger zone.
    /// We only care about the player's collider (identified via DustBunnyController).
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Only react to the player
        DustBunnyController player = other.GetComponent<DustBunnyController>();
        if (player == null) return;

        // Don't fire again once already unlocked
        if (hasUnlocked) return;

        if (ArtKitItem.HasViewedArtKit)
        {
            // Player has earned it — run the unlock sequence
            StartCoroutine(UnlockSequence());
        }
        else
        {
            // Player hasn't viewed the art kit yet — show hint
            ShowLockedHint();
        }
    }

    // -----------------------------------------------------------------------
    // Unlock Sequence
    // -----------------------------------------------------------------------

    /// <summary>
    /// The full unlock sequence:
    ///   1. Mark as unlocked so it can't trigger again.
    ///   2. Fade the floating badge out above the player.
    ///   3. Fade the bridge in.
    ///   4. Play unlock sound.
    ///   5. Disable this trigger collider so it's inert forever.
    /// </summary>
    private IEnumerator UnlockSequence()
    {
        hasUnlocked = true;

        Debug.Log("[BridgeUnlockTrigger] Art kit verified — unlocking bridge!");

        // Step 1: Dismiss the floating badge (it fades + rises + destroys itself)
        if (ArtKitItem.ActiveBadge != null)
        {
            ArtKitItem.ActiveBadge.FadeOutAndDestroy();
            ArtKitItem.ActiveBadge = null;
        }

        // Step 2: Play unlock sound
        if (bridgeUnlockSfx != null)
            bridgeUnlockSfx.Post(gameObject);

        // Step 3: Make the bridge appear
        if (bridgeObject != null)
        {
            if (fadeMode == FadeMode.FadeRenderers)
            {
                // Activate the bridge in case it was inactive, then fade it in
                bridgeObject.SetActive(true);
                yield return StartCoroutine(FadeBridgeIn());
            }
            else // ActivateOnly
            {
                bridgeObject.SetActive(true);
            }
        }
        else
        {
            Debug.LogWarning("[BridgeUnlockTrigger] bridgeObject is not assigned!");
        }

        // Step 4: Disable our trigger collider — the bridge is unlocked for good
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Debug.Log("[BridgeUnlockTrigger] Bridge fully unlocked and trigger disabled.");
    }

    // -----------------------------------------------------------------------
    // Bridge Alpha Fade
    // -----------------------------------------------------------------------

    /// <summary>
    /// Smoothly fades all renderer materials on the bridge from alpha 0 → 1.
    /// Works with URP Lit (Transparent) or Standard (Fade/Transparent) shaders.
    /// </summary>
    private IEnumerator FadeBridgeIn()
    {
        // Gather every material instance on the bridge and its children
        Renderer[] bridgeRenderers = bridgeObject.GetComponentsInChildren<Renderer>(includeInactive: true);
        Material[] mats = GatherBridgeMaterials(bridgeRenderers);

        // Record starting colors (alpha should already be 0 from Start())
        Color[] startColors = new Color[mats.Length];
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i].HasProperty("_BaseColor"))
                startColors[i] = mats[i].GetColor("_BaseColor");
            else if (mats[i].HasProperty("_Color"))
                startColors[i] = mats[i].GetColor("_Color");
            else
                startColors[i] = Color.white;

            // Ensure starting alpha is 0 before the loop begins
            Color c = startColors[i];
            c.a = 0f;
            ApplyColor(mats[i], c);
        }

        float elapsed = 0f;

        while (elapsed < bridgeFadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bridgeFadeInDuration);

            // Ease-in-out for a natural appearance
            float alpha = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < mats.Length; i++)
            {
                Color c = startColors[i];
                c.a = alpha;
                ApplyColor(mats[i], c);
            }

            yield return null;
        }

        // Ensure we land exactly on alpha = 1
        for (int i = 0; i < mats.Length; i++)
        {
            Color c = startColors[i];
            c.a = 1f;
            ApplyColor(mats[i], c);
        }
    }

    /// <summary>
    /// Sets ALL renderer materials on the bridge to a given alpha level.
    /// Used in Start() to initialise the bridge as invisible in FadeRenderers mode.
    /// </summary>
    private void SetBridgeAlpha(float alpha)
    {
        if (bridgeObject == null) return;

        Renderer[] renderers = bridgeObject.GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            foreach (Material m in r.materials)   // per-instance copies, safe to modify
            {
                if (m == null) continue;
                Color c;

                if (m.HasProperty("_BaseColor"))
                {
                    c   = m.GetColor("_BaseColor");
                    c.a = alpha;
                    m.SetColor("_BaseColor", c);
                }
                else if (m.HasProperty("_Color"))
                {
                    c   = m.GetColor("_Color");
                    c.a = alpha;
                    m.SetColor("_Color", c);
                }
            }
        }
    }

    // -----------------------------------------------------------------------
    // Hint
    // -----------------------------------------------------------------------

    /// <summary>Shows the "you need the art kit" hint, respecting the cooldown.</summary>
    private void ShowLockedHint()
    {
        if (Time.time - lastHintTime < hintCooldown) return;
        lastHintTime = Time.time;

        Debug.Log($"[BridgeUnlockTrigger] Player entered without art kit. Hint: '{lockedHintText}'");

        if (MemoryUIManager.Instance != null && !string.IsNullOrEmpty(lockedHintText))
            MemoryUIManager.Instance.ShowMemory(lockedHintText, Color.white);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Collects per-instance material copies from all renderers so we never
    /// modify shared material assets permanently.
    /// </summary>
    private Material[] GatherBridgeMaterials(Renderer[] renderers)
    {
        System.Collections.Generic.List<Material> list =
            new System.Collections.Generic.List<Material>();

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            foreach (Material m in r.materials)  // .materials gives per-instance copies
            {
                if (m != null && !list.Contains(m))
                    list.Add(m);
            }
        }

        return list.ToArray();
    }

    /// <summary>Sets the color on whichever property name the shader uses.</summary>
    private void ApplyColor(Material mat, Color color)
    {
        if (mat == null) return;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    // -----------------------------------------------------------------------
    // Setup Validation
    // -----------------------------------------------------------------------

    private void ValidateSetup()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
            Debug.LogError("[BridgeUnlockTrigger] No Collider found! Add a BoxCollider and enable 'Is Trigger'.");
        else if (!col.isTrigger)
            Debug.LogError("[BridgeUnlockTrigger] Collider is NOT set to 'Is Trigger'. Enable it in the Inspector.");

        if (bridgeObject == null)
            Debug.LogWarning("[BridgeUnlockTrigger] bridgeObject is not assigned. The bridge won't appear on unlock.");
    }

    // -----------------------------------------------------------------------
    // Editor Visualization
    // -----------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        // Yellow = waiting, green = already unlocked
        Gizmos.color = hasUnlocked
            ? new Color(0f, 1f, 0f, 0.25f)
            : new Color(1f, 0.9f, 0f, 0.25f);

        // Draw the trigger volume using the attached collider's bounds
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = prev;
        }
        else
        {
            Gizmos.DrawCube(transform.position, Vector3.one * 2f);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.2f,
            hasUnlocked ? "🌉 Bridge Unlocked!" : "🔒 Bridge Trigger (needs art kit)"
        );
#endif
    }
}