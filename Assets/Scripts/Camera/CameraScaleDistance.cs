using Unity.Cinemachine;
using UnityEngine;

public class CameraScaleDistance : MonoBehaviour
{
    public Transform player;

    [Header("Distance Settings")]
    public float baseRadius = 3f;
    public float distanceMultiplier = 1.05f;
    public float smoothSpeed = 3f;

    [Header("FOV Settings")]
    [Tooltip("Base FOV at smallest bunny size (degrees).")]
    public float baseFov = 60f;
    [Tooltip("Extra FOV added per scale ratio. Larger bunny = wider view. e.g. 8 means +8° FOV when bunny is 2x base size.")]
    public float fovScaleMultiplier = 12f;
    [Tooltip("Max FOV cap (degrees) to prevent extreme wide-angle distortion.")]
    public float maxFov = 85f;

    [Header("Katamari-Style Framing")]
    [Range(-0.5f, 0.5f)]
    public float bunnyScreenPositionY = -5f;
    [Tooltip("Look-at target offset above player. Higher = camera tilts down more, showing more of the world ahead.")]
    public float lookAheadOffsetY = 2.5f;

    private CinemachineOrbitalFollow orbital;
    private CinemachineCamera vcam;
    private CinemachineRotationComposer composer;
    private float baseScale;

    void Start()
    {
        orbital = GetComponent<CinemachineOrbitalFollow>();
        vcam = GetComponent<CinemachineCamera>();
        composer = GetComponent<CinemachineRotationComposer>();

        if (!player && GameObject.FindGameObjectWithTag("Player"))
            player = GameObject.FindGameObjectWithTag("Player").transform;

        baseScale = player.localScale.x;
        orbital.HorizontalAxis.Value = -90f;

        ApplyKatamariFraming();
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

        // Scale FOV with bunny size — larger bunny gets wider view
        if (vcam != null)
        {
            float targetFov = baseFov + (scaleRatio - 1f) * fovScaleMultiplier;
            targetFov = Mathf.Clamp(targetFov, baseFov, maxFov);

            var lens = vcam.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, Time.deltaTime * smoothSpeed);
            vcam.Lens = lens;
        }

        ApplyKatamariFraming();
    }

    void ApplyKatamariFraming()
    {
        if (orbital != null)
        {
            var offset = orbital.TargetOffset;
            offset.y = lookAheadOffsetY;
            orbital.TargetOffset = offset;
        }

        if (composer != null)
        {
            var comp = composer.Composition;
            comp.ScreenPosition.y = bunnyScreenPositionY;
            composer.Composition = comp;
        }
    }
}