using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DashKnockBridge : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider bridgeCollider;

    [Header("Behavior")]
    [SerializeField] private bool oneTime = true;
    [SerializeField] private float ignorePlayerCollisionTime = 0.5f; // prevents roll impact from pushing it
    [SerializeField] private float unlockDelay = 0f;                  // small delay can help stability (0–0.05)

    [Header("Audio")]
    public AK.Wwise.Event bridgeCreakSfx;

    private bool triggered;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
        bridgeCollider = GetComponent<Collider>();
    }

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (!bridgeCollider) bridgeCollider = GetComponent<Collider>();

        // Starts locked
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnCollisionEnter(Collision col)
    {
        if (triggered && oneTime) return;

        var player = col.collider.GetComponentInParent<DustBunnyController>();
        if (player == null || !player.isRolling) return;

        StartCoroutine(UnlockWithoutImpact(col.collider));
    }

    private IEnumerator UnlockWithoutImpact(Collider playerCollider)
    {
        triggered = true;

        bridgeCreakSfx.Post(gameObject);

        // Temporarily ignore collision so the dash impulse doesn't transfer into the bridge
        if (playerCollider != null)
            Physics.IgnoreCollision(bridgeCollider, playerCollider, true);

        // Ttiny delay so we unlock *after* the impact frame
        if (unlockDelay > 0f)
            yield return new WaitForSeconds(unlockDelay);
        else
            yield return new WaitForFixedUpdate();

        // Fall naturally due to its tilt
        rb.isKinematic = false;
        rb.useGravity = true;

        // Remove any leftover impulse (just in case)
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Re-enable collision after a moment
        yield return new WaitForSeconds(ignorePlayerCollisionTime);


        if (playerCollider != null)
            Physics.IgnoreCollision(bridgeCollider, playerCollider, false);
    }
}
