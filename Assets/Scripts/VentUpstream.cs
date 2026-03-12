using UnityEngine;

public class VentUpstream : MonoBehaviour
{
    [Header("Vent Force")]
    public float upwardSpeed = 30f;
    public float maxUpwardSpeed = 35f;

    private void OnTriggerStay(Collider other)
    {
        DustBunnyController player = other.GetComponent<DustBunnyController>();
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;

        if (velocity.y < maxUpwardSpeed)
        {
            velocity.y = upwardSpeed;
            rb.linearVelocity = velocity;
        }
    }
}