using UnityEngine;
using static ak;

public class VentUpstream : MonoBehaviour
{
    [Header("Vent Force")]
    public float upwardSpeed = 30f;
    public float maxUpwardSpeed = 35f;

    [Header("SFX")]
    public AK.Wwise.Event ventRideStart;
    public AK.Wwise.Event ventRideStop;

    private bool isPlaying;
    private uint playingID;

    private void OnTriggerStay(Collider other)
    {
        DustBunnyController player = other.GetComponent<DustBunnyController>();
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null)
        {
            return;
        }

        if (!isPlaying)
        {
            playingID = ventRideStart.Post(gameObject);
            isPlaying = true;
        }

        Vector3 velocity = rb.linearVelocity;

        if (velocity.y < maxUpwardSpeed)
        {
            velocity.y = upwardSpeed;
            rb.linearVelocity = velocity;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        DustBunnyController player = other.GetComponent<DustBunnyController>();
        if (player == null) return;

        if (isPlaying)
        {
            AkUnitySoundEngine.StopPlayingID(playingID);
            isPlaying = false;
        }
    }
}