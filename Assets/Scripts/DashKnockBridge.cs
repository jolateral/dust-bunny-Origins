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
    [SerializeField] private float unlockDelay = 0f;
    [SerializeField] private float extraFallAcceleration = 30f;

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

    private void OnCollisionStay(Collision col)
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

        if (unlockDelay > 0f)
            yield return new WaitForSeconds(unlockDelay);
        else
            yield return new WaitForFixedUpdate();

        // Fall naturally due to its tilt
        rb.isKinematic = false;
        rb.useGravity = true;

        // Remove any leftover impulse 
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (!rb.isKinematic && triggered)
        {
            rb.AddForce(Vector3.down * extraFallAcceleration, ForceMode.Acceleration);
        }
    }
}
