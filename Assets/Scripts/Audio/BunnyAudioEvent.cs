using UnityEngine;

public class BunnyAudioEvent : MonoBehaviour
{
    public AK.Wwise.Event bunnyMoveSfx;
    public AK.Wwise.Event bunnyRollSfx;
    public AK.Wwise.Event bunnyJumpSfx;
    public AK.Wwise.Event bunnyLandSfx;
    public AK.Wwise.Event bunnyImpactSfx;

    public void PlayBunnyMove()
    {
        bunnyMoveSfx.Post(gameObject);
    }

    public void PlayBunnyRoll()
    {
        bunnyRollSfx.Post(gameObject);
    }

    public void PlayBunnyJump()
    {
        bunnyJumpSfx.Post(gameObject);
    }

    public void PlayBunnyLand()
    {
        bunnyLandSfx.Post(gameObject);
    }

    public void PlayBunnyImpactSfx()
    {
        bunnyImpactSfx.Post(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 relativeVelocity = collision.relativeVelocity;
        if (relativeVelocity.magnitude > 1f)
        {
            AkUnitySoundEngine.SetRTPCValue("velocity", relativeVelocity.magnitude, gameObject);
            bunnyImpactSfx.Post(gameObject);
        }
    }
}
