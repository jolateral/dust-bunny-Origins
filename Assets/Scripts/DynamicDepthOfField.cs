using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A standalone script that dynamically adjusts the URP Depth of Field 
/// to keep a specific target (like the Dust Bunny) perfectly in focus.
/// </summary>
public class DynamicDepthOfField : MonoBehaviour
{
    [Header("--- Target Settings ---")]
    [Tooltip("The object to keep in focus (drag your Dust Bunny here).")]
    public Transform focusTarget;

    [Header("--- Camera & Volume ---")]
    [Tooltip("The URP Global Volume that contains the Depth of Field override.")]
    public Volume postProcessVolume;
    
    [Tooltip("The camera calculating the distance (usually your Main Camera).")]
    public Camera mainCamera;

    [Header("--- Focus Settings ---")]
    [Tooltip("Offset added to the calculated distance. Use this to fine-tune if the focus feels slightly off center.")]
    public float focusOffset = 0f;

    [Tooltip("How smoothly the lens adjusts focus. Higher is faster (10 is good for fast gameplay).")]
    public float focusSpeed = 10f;

    private DepthOfField dofComponent;

    void Start()
    {
        // Auto-assign Main Camera if left empty
        if (mainCamera == null) mainCamera = Camera.main;

        // Try to safely extract the Depth of Field component from the Volume Profile
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out dofComponent);
        }

        if (dofComponent == null)
        {
            Debug.LogWarning("Auto-Focus Error: No Depth of Field override found in the assigned Volume!");
        }
    }

    void Update()
    {
        // Safety check
        if (dofComponent == null || focusTarget == null || mainCamera == null) return;

        // Calculate the physical distance from the camera lens to the bunny
        float targetDistance = Vector3.Distance(mainCamera.transform.position, focusTarget.position) + focusOffset;

        // Smoothly adjust the focus distance (creating a cinematic "focus pull" effect)
        dofComponent.focusDistance.value = Mathf.Lerp(
            dofComponent.focusDistance.value, 
            targetDistance, 
            Time.deltaTime * focusSpeed
        );
    }
}