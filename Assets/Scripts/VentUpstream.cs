using UnityEngine;

public class VentUpstream : MonoBehaviour
{
    [Header("Vent Force")]
    public float upwardForce = 20f;

    private void OnTriggerStay(Collider other)
    {
        DustBunnyController player = other.GetComponent<DustBunnyController>();
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.AddForce(Vector3.up * upwardForce, ForceMode.Acceleration);
    }
}