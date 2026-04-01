using UnityEngine;

public class VentUpstream : MonoBehaviour
{
    [Header("Vent Force")]
    public float upwardSpeed = 30f;
    public float maxUpwardSpeed = 35f;

    [Header("SFX")]
    public AK.Wwise.Event ventRideStop;

    private void OnTriggerStay(Collider other)
    {
        DustBunnyController player = other.GetComponent<DustBunnyController>();
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null)
        {
            ventRideStop.Post(gameObject);
            return;
        }

        Vector3 velocity = rb.linearVelocity;

        if (velocity.y < maxUpwardSpeed)
        {
            velocity.y = upwardSpeed;
            rb.linearVelocity = velocity;
        }
    }
}