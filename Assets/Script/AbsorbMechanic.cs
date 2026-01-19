using UnityEngine;

public class AbsorbMechanic : MonoBehaviour
{
    [Header("Growth Settings")]
    public float growthFactor = 0.05f; // How much to grow per item
    public float sizeTolerance = 1.2f; // Player must be this much bigger than target

    private DustBunnyController controller;

    void Start()
    {
        controller = GetComponent<DustBunnyController>();
    }

    void OnCollisionEnter(Collision collision)
    {
        // Only absorb if we are in Rolling Mode (Shift held down)
        if (controller.isRolling && collision.gameObject.CompareTag("StickyObject"))
        {
            AttemptAbsorb(collision.gameObject);
        }
    }

    void AttemptAbsorb(GameObject item)
    {
        // Check size: Use rough bounds magnitude to compare
        float mySize = GetComponent<Collider>().bounds.extents.magnitude;
        float itemSize = item.GetComponent<Collider>().bounds.extents.magnitude;

        if (mySize >= itemSize * sizeTolerance)
        {
            // 1. Disable physics on the item
            Destroy(item.GetComponent<Rigidbody>());
            Destroy(item.GetComponent<Collider>());

            // 2. Attach to player
            item.transform.SetParent(this.transform);

            // 3. Grow player
            transform.localScale += Vector3.one * growthFactor;
            
            // Optional: Increase camera distance logic would go here
            Debug.Log("Absorbed: " + item.name);
        }
        else
        {
            Debug.Log("Too big to eat yet!");
        }
    }
}