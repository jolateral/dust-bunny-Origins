using UnityEngine;

public class HumanEventTrigger : MonoBehaviour
{
    [Tooltip("Drag the Human GameObject here.")]
    public GiantHumanWalker humanWalker;
    
    [Tooltip("If true, the human will only walk in once. If false, every time you enter Zone 1, they walk in.")]
    public bool triggerOnlyOnce = true;

    private bool hasTriggered = false;

    void OnTriggerEnter(Collider other)
    {
        // Check if the Dust Bunny entered the zone
        if (other.CompareTag("Player"))
        {
            if (triggerOnlyOnce && hasTriggered) return;

            if (humanWalker != null)
            {
                humanWalker.StartWalkingSequence();
                hasTriggered = true;
                Debug.Log("Human event triggered!");
            }
        }
    }
}