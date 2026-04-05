using UnityEngine;

public class TutorialTriggerCollision : MonoBehaviour
{
    public enum TutorialDisplayMode
    {
        Text,
        Image
    }

    [Header("Display Mode")]
    public TutorialDisplayMode displayMode = TutorialDisplayMode.Text;

    [Header("Text Tutorial")]
    [TextArea(2, 5)]
    public string tutorialText = "You can move or dash into objects to absorb them.";
    public Color textColor = Color.white;

    [Header("Image Tutorial")]
    public Sprite tutorialImage;

    [Header("Trigger Settings")]
    public bool oneTimeOnly = true;
    public float showDelay = 0.5f;
    public bool hideOnExit = true;

    public AK.Wwise.Event uiTutorialPopup;

    private bool hasBeenTriggered = false;
    private bool isShowing = false;

    private void OnTriggerEnter(Collider other)
    {
        if ((hasBeenTriggered && oneTimeOnly) || isShowing)
            return;

        var player = other.GetComponentInParent<DustBunnyController>();
        if (player == null) return;

        if (uiTutorialPopup != null)
            uiTutorialPopup.Post(gameObject);

        TriggerTutorial();
    }

    void TriggerTutorial()
    {
        if ((hasBeenTriggered && oneTimeOnly) || isShowing)
            return;

        hasBeenTriggered = true;
        isShowing = true;

        if (showDelay > 0f)
            Invoke(nameof(ShowTutorial), showDelay);
        else
            ShowTutorial();

        Invoke(nameof(ResetShowing), showDelay + 3f);
    }

    void ShowTutorial()
    {
        if (MemoryUIManager.Instance == null) return;

        if (displayMode == TutorialDisplayMode.Image && tutorialImage != null)
        {
            MemoryUIManager.Instance.ShowImage(tutorialImage);
        }
        else
        {
            MemoryUIManager.Instance.ShowMemory(tutorialText, textColor);
        }
    }

    void ResetShowing()
    {
        isShowing = false;
    }
}