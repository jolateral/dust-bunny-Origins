using UnityEngine;

public class MusicStateCollisionTrigger : MonoBehaviour
{
    public AK.Wwise.State state;
    private bool hasTriggered = false;

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collision triggered with " + collision.gameObject.name);

        if (hasTriggered) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            state.SetValue();
            hasTriggered = true;

            Debug.Log("Music State set to: " + state.ToString());
        }
    }
}