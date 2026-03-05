using UnityEngine;

/// <summary>
/// FloatingKeyBehaviour.cs
///
/// Attach this to the Key prefab.
/// While the key is loose in the world it does nothing.
/// Once the key is absorbed by the player (i.e. parented to the bunny),
/// it floats above the bunny's head and spins instead of sticking to the surface.
///
/// This script is activated by KeyItem.OnAbsorbed().
/// </summary>
public class FloatingKeyBehaviour : MonoBehaviour
{
    [Header("Float Settings")]
    [Tooltip("How high above the bunny's center the key hovers.")]
    public float floatHeight = 1.2f;

    [Tooltip("How far the key bobs up and down from its base height.")]
    public float bobAmplitude = 0.15f;

    [Tooltip("How fast the key bobs up and down (cycles per second).")]
    public float bobSpeed = 2f;

    [Header("Spin Settings")]
    [Tooltip("Degrees per second the key spins on its Y axis.")]
    public float spinSpeed = 180f;

    // Whether we are in floating mode (activated by KeyItem.OnAbsorbed)
    private bool isFloating = false;

    // The bunny transform we are parented to
    private Transform parentTransform;

    /// <summary>
    /// Called by KeyItem.OnAbsorbed() to activate floating mode.
    /// </summary>
    public void StartFloating(Transform bunnyTransform)
    {
        isFloating = true;
        parentTransform = bunnyTransform;
    }

    void Update()
    {
        if (!isFloating || parentTransform == null) return;

        float currentScale = (parentTransform.localScale.x + parentTransform.localScale.y + parentTransform.localScale.z) / 3f;
        float scaledHeight = floatHeight * currentScale;
        float scaledBob = bobAmplitude * currentScale;

        float bob = Mathf.Sin(Time.time * bobSpeed) * scaledBob;

        transform.position = parentTransform.position + Vector3.up * (scaledHeight + bob);

        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }
}