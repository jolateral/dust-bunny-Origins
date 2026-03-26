using UnityEngine;

public class ObjectiveImageTrigger : MonoBehaviour
{
    [SerializeField] private GameObject objectiveImage;
    [SerializeField] private string playerTag = "Player";

    private void Start()
    {
        if (objectiveImage != null)
            objectiveImage.SetActive(false); // start hidden
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (objectiveImage != null)
            objectiveImage.SetActive(true);
    }
}