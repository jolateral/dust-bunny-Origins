using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public AK.Wwise.Event mus;
    public AK.Wwise.State state;
    void Start()
    {
        state.SetValue();

        mus.Post(gameObject);
    }
}
