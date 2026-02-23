using UnityEngine;

/// <summary>
/// Place this on a trigger collider at the top of a bookshelf (or any high spot).
/// When the player stands in the zone, they can press the Glide button (F / R1) to jump off and glide.
/// Tutorial text is shown via TutorialTrigger (assign one here, or on a child/sibling).
/// </summary>
[RequireComponent(typeof(Collider))]
public class GlideLaunchSpot : MonoBehaviour
{
    [Header("Tutorial prompt (TutorialTrigger holds the text)")]
    [Tooltip("Optional. If set, this TutorialTrigger is shown when player enters and hidden when they leave. Put your prompt text on the TutorialTrigger.")]
    [SerializeField] private TutorialTrigger tutorialTrigger;

    [Header("Leap of Faith (Design Doc: minimum mass to take the leap)")]
    [Tooltip("Minimum scale (average of x,y,z) required to glide. 0 = no minimum.")]
    [SerializeField] private float minScaleToGlide = 0f;
    [Tooltip("Prompt when player doesn't have enough mass yet.")]
    [SerializeField] private string notEnoughMassText = "Gather more dust to take the leap";

    [Header("Launch")]
    [Tooltip("Optional. If set, player auto-moves to this position (and rotation) when they press F, then glides. Place at the ledge edge.")]
    [SerializeField] private Transform launchPoint;
    [Tooltip("Forward direction for the initial glide launch (world space). If zero, camera forward is used.")]
    [SerializeField] private Vector3 launchDirection = Vector3.zero;

    [Tooltip("If set, only objects with this tag can trigger. Leave empty to use Player tag.")]
    [SerializeField] private string triggerTag = "Player";

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (string.IsNullOrEmpty(triggerTag) ? other.CompareTag("Player") : other.CompareTag(triggerTag))
        {
            var controller = other.GetComponent<DustBunnyController>();
            if (controller != null)
            {
                controller.EnterGlideLaunchZone(this);
                if (tutorialTrigger != null)
                    tutorialTrigger.ShowTutorial();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (string.IsNullOrEmpty(triggerTag) ? other.CompareTag("Player") : other.CompareTag(triggerTag))
        {
            var controller = other.GetComponent<DustBunnyController>();
            if (controller != null)
                controller.ExitGlideLaunchZone(this);
            if (tutorialTrigger != null)
                tutorialTrigger.HideTutorial();
        }
    }

    /// <summary> If set, player is moved here when they press F before gliding. </summary>
    public Transform GetLaunchPoint() => launchPoint;

    /// <summary> Launch direction for this spot (world space). If zero, controller uses camera forward. </summary>
    public Vector3 GetLaunchDirection() => launchDirection;
    public string GetPromptText() => tutorialTrigger != null ? tutorialTrigger.tutorialText : "Press  <sprite=1>to glide";

    /// <summary> Minimum scale required to take the leap (design doc: "accumulate a certain amount of mass"). 0 = no requirement. </summary>
    public float GetMinScaleToGlide() => minScaleToGlide;
    public string GetNotEnoughMassText() => notEnoughMassText;
}
