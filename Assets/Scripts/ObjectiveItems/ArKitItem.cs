using UnityEngine;

/// <summary>
/// ArtKitItem.cs  (UPDATED — Bridge Unlock Support)
///
/// Attach this to the Art Kit GameObject in the world.
/// When the player collides with it, the Art Kit UI popup appears.
///
/// NEW BEHAVIOUR (Bridge Unlock):
///   After the popup closes, a floating "badge" GameObject is spawned above
///   the player's head (exactly like the floating key after diary pickup).
///   A static flag (ArtKitItem.HasViewedArtKit) is set to true so that
///   BridgeUnlockTrigger knows the player has earned passage.
///
/// SETUP STEPS:
///   1. Assign artKitSprite (the full-screen image for the popup).
///   2. Optionally assign artKitText for text shown over the image.
///   3. Assign FloatingRulerPrefab — a small visual (e.g. a paint palette mesh)
///      that will float above the bunny until the bridge trigger is reached.
///      That prefab MUST have a FloatingRulerItem component on it.
///   4. Keep openOnlyOnce = true so the popup only fires once per session.
///
/// DEPENDENCIES:
///   - ArtKitUIManager  (existing, but needs the ArtKitClosed event added — see that file)
///   - FloatingRulerItem   (new script, attach to the badge prefab)
///   - BridgeUnlockTrigger   (new script, place on the invisible trigger zone)
///   - DustBunnyController   (existing player script)
/// </summary>
public class ArtKitItem : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector Fields
    // -----------------------------------------------------------------------

    [Header("Art Kit Content")]
    [Tooltip("The full-screen image shown when the Art Kit popup opens.")]
    public Sprite artKitSprite;

    [Tooltip("Optional text shown over the image. Leave empty for image-only.")]
    [TextArea(5, 15)]
    public string artKitText = "";

    [Header("Behaviour")]
    [Tooltip("Recommended: true so the popup only fires on the first touch.")]
    public bool openOnlyOnce = true;

    [Header("Bridge Unlock — Floating Badge")]
    [Tooltip("Prefab spawned above the player after they close the popup.\n" +
             "Must have a FloatingRulerItem component on it.\n" +
             "Leave empty if you don't want a floating badge visual.")]
    public GameObject FloatingRulerPrefab;

    [Header("Audio")]
    [Tooltip("Optional Wwise event played when the Art Kit is opened.")]
    public AK.Wwise.Event openSfx;

    // -----------------------------------------------------------------------
    // Static State  (read by BridgeUnlockTrigger and FloatingRulerItem)
    // -----------------------------------------------------------------------

    /// <summary>
    /// True once the player has dismissed the art kit popup.
    /// BridgeUnlockTrigger checks this flag when the player enters the trigger zone.
    /// Call ArtKitItem.ResetAll() on a new game / scene reload to clear it.
    /// </summary>
    public static bool HasViewedArtKit = false;

    /// <summary>
    /// Live reference to the floating badge currently above the player.
    /// BridgeUnlockTrigger uses this to tell the badge to fade away.
    /// Null when no badge is active.
    /// </summary>
    public static FloatingRulerItem ActiveBadge = null;

    // -----------------------------------------------------------------------
    // Private State
    // -----------------------------------------------------------------------

    /// <summary>Whether the popup has already been opened this session.</summary>
    private bool hasBeenOpened = false;

    /// <summary>Cached reference to the player so we can parent the badge to them.</summary>
    private DustBunnyController cachedPlayer = null;

    // -----------------------------------------------------------------------
    // Unity Messages
    // -----------------------------------------------------------------------

    private void Start()
    {
        // Warn designers early if required references are missing
        if (artKitSprite == null)
            Debug.LogWarning($"[ArtKitItem] '{name}' has no artKitSprite assigned.");

        if (ArtKitUIManager.Instance == null)
            Debug.LogWarning($"[ArtKitItem] No ArtKitUIManager found in scene.");
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only react to the player
        DustBunnyController player = collision.gameObject.GetComponent<DustBunnyController>();
        if (player == null) return;

        // Respect the "only open once" setting
        if (openOnlyOnce && hasBeenOpened) return;

        cachedPlayer = player;
        ForcePlayerIdle(player);
        OpenArtKit();
    }

    // -----------------------------------------------------------------------
    // Private Methods
    // -----------------------------------------------------------------------

    /// <summary>Shows the popup and subscribes to its closed callback.</summary>
    private void OpenArtKit()
    {
        hasBeenOpened = true;

        if (openSfx != null)
            openSfx.Post(gameObject);

        if (ArtKitUIManager.Instance != null)
        {
            // Subscribe BEFORE calling ShowArtKit so we never miss the event
            ArtKitUIManager.Instance.ArtKitClosed -= OnArtKitPopupClosed;
            ArtKitUIManager.Instance.ArtKitClosed += OnArtKitPopupClosed;

            ArtKitUIManager.Instance.ShowArtKit(artKitText, artKitSprite);
        }
        else
        {
            Debug.LogError("[ArtKitItem] ArtKitUIManager.Instance is null! Falling back.");
            // Proceed with unlock even if UI is unavailable
            OnArtKitPopupClosed();
        }
    }

    /// <summary>
    /// Callback fired by ArtKitUIManager the moment the player dismisses the popup.
    /// Sets the bridge-unlock flag and spawns the floating badge above the player.
    /// </summary>
    private void OnArtKitPopupClosed()
    {
        // Unsubscribe immediately so this only fires once
        if (ArtKitUIManager.Instance != null)
            ArtKitUIManager.Instance.ArtKitClosed -= OnArtKitPopupClosed;

        // Set the unlock flag — BridgeUnlockTrigger now knows the player can pass
        HasViewedArtKit = true;
        Debug.Log("[ArtKitItem] Art kit viewed. Bridge unlock flag = true.");

        // Spawn the floating badge above the player (mirrors the key after absorption)
        SpawnFloatingBadge();
    }

    /// <summary>
    /// Instantiates the badge prefab and tells FloatingRulerItem to start floating.
    /// The badge is parented to the player so it travels with them until the trigger zone.
    /// </summary>
    private void SpawnFloatingBadge()
    {
        if (FloatingRulerPrefab == null)
        {
            Debug.Log("[ArtKitItem] No FloatingRulerPrefab assigned — skipping badge spawn.");
            return;
        }

        if (cachedPlayer == null)
        {
            Debug.LogWarning("[ArtKitItem] No cached player reference — cannot spawn badge.");
            return;
        }

        // Spawn at the player's world position; FloatingRulerItem will offset it upward
        GameObject badgeGO = Instantiate(
            FloatingRulerPrefab,
            cachedPlayer.transform.position,
            Quaternion.Euler(-90, 0, 0)
        );

        // Parent to player so the badge moves with the bunny automatically
        badgeGO.transform.SetParent(cachedPlayer.transform);

        // Activate the floating behaviour
        FloatingRulerItem badge = badgeGO.GetComponent<FloatingRulerItem>();
        if (badge != null)
        {
            badge.StartFloating(cachedPlayer.transform);
            ActiveBadge = badge;   // Store globally so BridgeUnlockTrigger can find it
        }
        else
        {
            Debug.LogWarning("[ArtKitItem] FloatingRulerPrefab is missing a FloatingRulerItem component!");
        }
    }

    // -----------------------------------------------------------------------
    // Utility
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resets all static state. Call this on a new game start or scene reload
    /// so the flag and badge reference are clean.
    /// </summary>
    public static void ResetAll()
    {
        HasViewedArtKit = false;
        ActiveBadge = null;
    }

    /// <summary>Stops the player moving when the popup opens, mirroring DiaryItem behaviour.</summary>
    private void ForcePlayerIdle(DustBunnyController player)
    {
        if (player == null) return;

        Animator anim = player.GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetBool("isRunning", false);
            anim.SetBool("isRolling", false);
            anim.SetBool("isGliding", false);
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // -----------------------------------------------------------------------
    // Editor Visualization
    // -----------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        // Blue = unopened, green = already opened
        Gizmos.color = hasBeenOpened
            ? new Color(0f, 1f, 0f, 0.4f)
            : new Color(0.2f, 0.6f, 1f, 0.6f);

        Gizmos.DrawCube(transform.position, transform.localScale);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.7f,
            "🎨 Art Kit (Bridge Unlock)"
        );
#endif
    }
}