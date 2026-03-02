using System;
using UnityEngine;

public class Zone1AreaLockCollider : MonoBehaviour
{
    [SerializeField] private string message = "You need to collect all the memory fragments to pass through here.";
    [SerializeField] private Color restrictionColour = Color.red;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            var paperData = PaperUIManager.Instance != null ? PaperUIManager.Instance.CurrentPaperData : null;
            if (paperData == null) return;

            if (paperData.GetCollectedCount() >= paperData.totalPieces)
            {
                // Destroy self to allow player to pass through
                Destroy(gameObject);
            }
            else
            {
                MemoryUIManager.Instance.ShowMemory(message, restrictionColour);
            }
        }
    }
}
