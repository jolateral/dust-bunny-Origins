using UnityEngine;

public class RespawnBarrier : MonoBehaviour
{
    [SerializeField] private Vector3 teleportPosition;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.position = teleportPosition;
        }
    }
}
