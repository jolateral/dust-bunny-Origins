using UnityEngine;

public class PhysicsBasedAudio : MonoBehaviour
{
    public AK.Wwise.Event physicsImpact;
    public bool debug;

    void OnCollisionEnter(Collision collisionInfo)
    {
        // Get the relative linear velocity of the two colliding objects.
        Vector3 relativeVelocity = collisionInfo.relativeVelocity;

        float objectMass = GetComponent<Rigidbody>().mass;

        AkUnitySoundEngine.SetRTPCValue("object_mass", objectMass);

        if (debug)
        {
            Debug.Log("Relative Velocity Magnitude: " + relativeVelocity.magnitude);
            Debug.Log("Object Mass: " + objectMass);
        }

        // Example: Play a sound if the impact velocity is high
        if (relativeVelocity.magnitude > 1f)
        {
            AkUnitySoundEngine.SetRTPCValue("velocity", relativeVelocity.magnitude, gameObject);
            physicsImpact.Post(gameObject);
        }
    }
}