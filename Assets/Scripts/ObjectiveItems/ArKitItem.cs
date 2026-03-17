using UnityEngine;

/// <summary>
/// ArtKitItem.cs
///
/// Attach this to an Art Kit GameObject in the world.
/// When the player collides with it, the Art Kit UI appears immediately.
/// No key mechanic is required.
/// </summary>
public class ArtKitItem : MonoBehaviour
{
    // Inspector Fields

    [Header("Art Kit Content")]
    [Tooltip("The full-screen image shown when the Art Kit is opened.")]
    public Sprite artKitSprite;

    [Tooltip("Optional text shown over the Art Kit image.")]
    [TextArea(5, 15)]
    public string artKitText = "";

    [Header("Behaviour")]
    [Tooltip("If true, the player can only open this once.")]
    public bool openOnlyOnce = false;

    [Header("Audio")]
    [Tooltip("Sound played when the Art Kit is opened.")]
    public AK.Wwise.Event openSfx;

    private bool hasBeenOpened = false;

    private void Start()
    {
        if (artKitSprite == null)
        {
            Debug.LogWarning($"[ArtKitItem] '{name}' has no artKitSprite assigned.");
        }

        if (ArtKitUIManager.Instance == null)
        {
            Debug.LogWarning($"[ArtKitItem] No ArtKitUIManager found in scene! Make sure one exists.");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        DustBunnyController player = collision.gameObject.GetComponent<DustBunnyController>();
        if (player == null) return;

        if (openOnlyOnce && hasBeenOpened) return;

        ForcePlayerIdle(player);
        OpenArtKit();
    }

    // Private Methods
    private void OpenArtKit()
    {
        hasBeenOpened = true;

        if (openSfx != null)
            openSfx.Post(gameObject);

        if (ArtKitUIManager.Instance != null)
        {
            ArtKitUIManager.Instance.ShowArtKit(artKitText, artKitSprite);
        }
        else
        {
            Debug.LogError("[ArtKitItem] ArtKitUIManager.Instance is null! Can't show Art Kit.");
        }
    }

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

    private void OnDrawGizmos()
    {
        Gizmos.color = hasBeenOpened
            ? new Color(0f, 1f, 0f, 0.4f)
            : new Color(0.2f, 0.6f, 1f, 0.6f);

        Gizmos.DrawCube(transform.position, transform.localScale);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.7f,
            "🎨 Art Kit"
        );
#endif
    }
}