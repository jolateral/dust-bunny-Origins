using UnityEngine;

public class ObjectiveTextTrigger : MonoBehaviour
{
    [SerializeField] private GameObject objectiveText;
    [SerializeField] private string playerTag = "Player";

    private void Start()
    {
        if (objectiveText != null)
            objectiveText.SetActive(false); // start hidden
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (objectiveText != null)
            objectiveText.SetActive(true);
    }
}