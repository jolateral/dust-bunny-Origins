using UnityEngine;

public class AudioZoneAssistant : MonoBehaviour
{
    public bool drawGizmo = true;
    public Color gizmoColor = Color.red;
    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;

        Gizmos.color = gizmoColor;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {

            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            return;
        }
    }
}
