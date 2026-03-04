using Unity.Cinemachine;
using UnityEngine;

public class CameraScaleDistance : MonoBehaviour
{
    public Transform player;

    [Header("Distance Settings")]
    public float baseRadius = 3f;
    public float distanceMultiplier = 1.05f;
    public float smoothSpeed = 3f;

    private CinemachineOrbitalFollow orbital;
    private float baseScale;

    void Start()
    {
        orbital = GetComponent<CinemachineOrbitalFollow>();

        if (!player && GameObject.FindGameObjectWithTag("Player"))
            player = GameObject.FindGameObjectWithTag("Player").transform;

        baseScale = player.localScale.x;
        orbital.HorizontalAxis.Value = 90f;
    }

    void LateUpdate()
    {
        float scaleRatio = player.localScale.x / baseScale;

        float targetRadius = baseRadius + scaleRatio * distanceMultiplier;

        orbital.Radius = Mathf.Lerp(
            orbital.Radius,
            targetRadius,
            Time.deltaTime * smoothSpeed
        );
    }
}