using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// ArtKitUIManager.cs
///
/// Handles a full-screen Art Kit popup.
/// Similar to DiaryUIManager, but without any key/unlock logic.
/// The Art Kit opens immediately when the player collides with it,
/// and closes when the player presses Space or controller X.
/// </summary>
public class ArtKitUIManager : MonoBehaviour
{
    public static ArtKitUIManager Instance;

    // Inspector References

    [Header("Background Overlay")]
    [Tooltip("CanvasGroup on the dark background panel that dims the rest of the screen.")]
    public CanvasGroup backgroundGroup;

    [Header("Art Kit Panel")]
    [Tooltip("CanvasGroup on the panel that contains the image and optional text.")]
    public CanvasGroup artKitPanelGroup;

    [Tooltip("The Image component that displays the Art Kit sprite.")]
    public Image artKitImage;

    [Tooltip("(Optional) TextMeshProUGUI for text shown over the image. Leave empty for image-only.")]
    public TextMeshProUGUI artKitText;

    [Header("Instruction Image")]
    [Tooltip("UI Image that blinks to show the close instruction.")]
    public Image instructionImage;

    [Header("Settings")]
    [Tooltip("How fast the UI fades in and out.")]
    public float fadeSpeed = 3f;

    [Tooltip("Background overlay color.")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 1f);

    [Header("Player Reference")]
    [Tooltip("Drag the Player GameObject here, or leave empty to auto-find.")]
    public DustBunnyController playerController;

    [Header("SFX")]
    public AK.Wwise.Event uiNext;


    private bool isArtKitShowing = false;
    private bool waitingForInput = false;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<DustBunnyController>();
            if (playerController == null)
                Debug.LogWarning("[ArtKitUIManager] No DustBunnyController found in scene!");
        }

        SetGroupAlpha(backgroundGroup, 0f, false);
        SetGroupAlpha(artKitPanelGroup, 0f, false);

        if (instructionImage != null)
            instructionImage.gameObject.SetActive(false);

        if (backgroundGroup != null)
        {
            Image bgImage = backgroundGroup.GetComponent<Image>();
            if (bgImage != null)
                bgImage.color = backgroundColor;
        }
    }

    private void Update()
    {
        if (isArtKitShowing && waitingForInput)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                uiNext.Post(gameObject);

                StartCoroutine(HideArtKit());
            }
        }
    }

    // Public API

    public void ShowArtKit(string text, Sprite sprite)
    {
        if (isArtKitShowing) return;

        StopAllCoroutines();
        StartCoroutine(DisplayArtKit(text, sprite));
    }

    public void DismissArtKit()
    {
        if (isArtKitShowing)
            StartCoroutine(HideArtKit());
    }

    public bool IsArtKitShowing() => isArtKitShowing;

    // Private Coroutines

    private IEnumerator DisplayArtKit(string text, Sprite sprite)
    {
        AkUnitySoundEngine.SetState("player_state", "memory");

        isArtKitShowing = true;
        waitingForInput = false;

        if (playerController != null)
            playerController.enabled = false;

        if (artKitImage != null && sprite != null)
        {
            artKitImage.sprite = sprite;
            artKitImage.preserveAspect = true;
        }

        if (artKitText != null)
        {
            if (!string.IsNullOrEmpty(text))
            {
                artKitText.text = text;
                artKitText.gameObject.SetActive(true);
            }
            else
            {
                artKitText.gameObject.SetActive(false);
            }
        }

        if (backgroundGroup != null)
        {
            backgroundGroup.blocksRaycasts = true;
            backgroundGroup.interactable = true;
        }

        if (artKitPanelGroup != null)
        {
            artKitPanelGroup.blocksRaycasts = true;
            artKitPanelGroup.interactable = true;
        }

        yield return StartCoroutine(FadeGroup(backgroundGroup, 0f, 1f));
        yield return StartCoroutine(FadeGroup(artKitPanelGroup, 0f, 1f));

        yield return new WaitForSeconds(0.3f);

        waitingForInput = true;

        if (instructionImage != null)
        {
            instructionImage.gameObject.SetActive(true);

            Color c = instructionImage.color;
            c.a = 1f;
            instructionImage.color = c;

            StartCoroutine(BlinkInstructionImage());
        }
    }

    private IEnumerator HideArtKit()
    {
        AkUnitySoundEngine.SetState("player_state", "None");
        AkUnitySoundEngine.SetState("mus_zone2", "mus_zone2_2");

        Debug.Log("Music State set to: mus_zone2, mus_zone2_2");

        waitingForInput = false;

        yield return StartCoroutine(FadeGroup(artKitPanelGroup, 1f, 0f));
        yield return StartCoroutine(FadeGroup(backgroundGroup, 1f, 0f));

        if (backgroundGroup != null)
        {
            backgroundGroup.blocksRaycasts = false;
            backgroundGroup.interactable = false;
        }

        if (artKitPanelGroup != null)
        {
            artKitPanelGroup.blocksRaycasts = false;
            artKitPanelGroup.interactable = false;
        }

        if (instructionImage != null)
            instructionImage.gameObject.SetActive(false);


        if (playerController != null)
        {
            playerController.SuppressJumpForSeconds(0.5f);
            playerController.enabled = true;
        }

        isArtKitShowing = false;
    }

    // Utility

    private IEnumerator FadeGroup(CanvasGroup group, float startAlpha, float endAlpha)
    {
        if (group == null) yield break;

        group.alpha = startAlpha;

        while (!Mathf.Approximately(group.alpha, endAlpha))
        {
            group.alpha = Mathf.MoveTowards(group.alpha, endAlpha, Time.deltaTime * fadeSpeed);
            yield return null;
        }

        group.alpha = endAlpha;
    }

    private void SetGroupAlpha(CanvasGroup group, float alpha, bool blocksRaycasts)
    {
        if (group == null) return;

        group.alpha = alpha;
        group.blocksRaycasts = blocksRaycasts;
        group.interactable = blocksRaycasts;
    }

    private IEnumerator BlinkInstructionImage()
    {
        if (instructionImage == null) yield break;

        while (waitingForInput)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime * 2f)
            {
                if (!waitingForInput) yield break;

                Color c = instructionImage.color;
                c.a = Mathf.Lerp(1f, 0.3f, t);
                instructionImage.color = c;
                yield return null;
            }

            for (float t = 0; t < 1f; t += Time.deltaTime * 2f)
            {
                if (!waitingForInput) yield break;

                Color c = instructionImage.color;
                c.a = Mathf.Lerp(0.3f, 1f, t);
                instructionImage.color = c;
                yield return null;
            }
        }

        Color final = instructionImage.color;
        final.a = 1f;
        instructionImage.color = final;
    }
}