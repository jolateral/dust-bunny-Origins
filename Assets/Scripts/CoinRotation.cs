using UnityEngine;

/// <summary>
/// CoinRotation.cs
/// 
/// Rotates a coin object continuously around a specified axis.
/// Optionally includes a floating/bobbing motion for visual appeal.
/// 
/// Usage:
/// 1. Attach this script to your coin GameObject
/// 2. Set the rotation axis (default: Y-axis for horizontal spinning)
/// 3. Adjust rotation speed as needed
/// 4. Enable floating motion if desired
/// </summary>
public class CoinRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Speed of rotation in degrees per second")]
    public float rotationSpeed = 90f;
    
    [Tooltip("Axis to rotate around (X, Y, or Z)")]
    public RotationAxis rotationAxis = RotationAxis.Y;
    
    [Header("Floating Motion (Optional)")]
    [Tooltip("Enable vertical floating/bobbing motion")]
    public bool enableFloating = false;
    
    [Tooltip("Amplitude of floating motion")]
    public float floatAmplitude = 0.5f;
    
    [Tooltip("Speed of floating motion")]
    public float floatSpeed = 2f;
    
    [Tooltip("Starting offset for floating motion (prevents all coins from syncing)")]
    public float floatOffset = 0f;
    
    // Private variables
    private Vector3 initialPosition;
    private float floatTimer = 0f;
    
    /// <summary>
    /// Enum for rotation axis selection
    /// </summary>
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }
    
    /// <summary>
    /// Initialize the coin
    /// </summary>
    void Start()
    {
        // Store initial position for floating motion
        initialPosition = transform.position;
        
        // Randomize float offset if not set manually
        if (floatOffset == 0f && enableFloating)
        {
            floatOffset = Random.Range(0f, Mathf.PI * 2f);
        }
    }
    
    /// <summary>
    /// Update rotation and floating motion each frame
    /// </summary>
    void Update()
    {
        // Rotate the coin
        RotateCoin();
        
        // Apply floating motion if enabled
        if (enableFloating)
        {
            ApplyFloatingMotion();
        }
    }
    
    /// <summary>
    /// Rotate the coin around the specified axis
    /// </summary>
    private void RotateCoin()
    {
        Vector3 rotationVector = Vector3.zero;
        
        switch (rotationAxis)
        {
            case RotationAxis.X:
                rotationVector = Vector3.right;
                break;
            case RotationAxis.Y:
                rotationVector = Vector3.up;
                break;
            case RotationAxis.Z:
                rotationVector = Vector3.forward;
                break;
        }
        
        transform.Rotate(rotationVector * rotationSpeed * Time.deltaTime, Space.Self);
    }
    
    /// <summary>
    /// Apply vertical floating/bobbing motion
    /// </summary>
    private void ApplyFloatingMotion()
    {
        floatTimer += Time.deltaTime * floatSpeed;
        float newY = initialPosition.y + Mathf.Sin(floatTimer + floatOffset) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
    
    /// <summary>
    /// Reset the coin to its initial position
    /// Useful for respawning or resetting state
    /// </summary>
    public void ResetCoin()
    {
        transform.position = initialPosition;
        floatTimer = 0f;
    }
}
