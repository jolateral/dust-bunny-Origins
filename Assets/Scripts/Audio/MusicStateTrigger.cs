using UnityEngine;

public class MusicStateTrigger : MonoBehaviour
{
    public AK.Wwise.State state;
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Collision triggered with " + other.gameObject.name);

        if (hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            state.SetValue();
            hasTriggered = true;

            Debug.Log("Music State set to: " + state.ToString());
        }
    }
}