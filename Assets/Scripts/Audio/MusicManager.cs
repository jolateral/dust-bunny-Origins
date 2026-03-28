using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public AK.Wwise.Event mus;
    void Start()
    {
        mus.Post(gameObject);

        AkUnitySoundEngine.SetState("mus_state", "zone1");
    }
}