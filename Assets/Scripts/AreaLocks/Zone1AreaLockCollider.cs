using System;
using UnityEngine;

public class Zone1AreaLockCollider : MonoBehaviour
{
    [SerializeField] private Sprite message;
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
                MemoryUIManager.Instance.ShowImage(message);
            }
        }
    }
}
