using UnityEngine;

public class BunnyAudioEvent : MonoBehaviour
{
    public AK.Wwise.Event bunnyMoveSfx;
    public AK.Wwise.Event bunnyRollSfx;

    public void PlayBunnyMove()
    {
        bunnyMoveSfx.Post(gameObject);
    }

    public void PlayBunnyRoll()
    {
        bunnyRollSfx.Post(gameObject);
    }
}
