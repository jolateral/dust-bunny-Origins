using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PaperUIManager : MonoBehaviour
{
    public static PaperUIManager Instance;

    [Header("Single-Piece UI References")]
    public CanvasGroup backgroundGroup;
    public CanvasGroup singlePieceGroup;
    public Image singlePieceImage;
    public TextMeshProUGUI singlePieceText;

    [Header("Multi-Piece UI References")]
    public CanvasGroup multiPieceGroup;
    public Image blackBackgroundPanel;
    public RectTransform pieceContainer;
    public GameObject piecePrefab;
    public TextMeshProUGUI progressText;

    [Header("Shared UI References")]
    public Image instructionImage;

    [Header("Settings")]
    public Color backgroundColor = Color.black;
    public float fadeSpeed = 3f;

    [Header("Player Reference")]
    public DustBunnyController playerController;

    [Header("Audio")]
    public AK.Wwise.Event uiSelect;

    private bool isPaperShowing = false;
    private bool waitingForInput = false;
    private bool isMultiPieceMode = false;

    private MultiPiecePaperData currentPaperData;
    public MultiPiecePaperData CurrentPaperData => currentPaperData;

    void Awake()
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
            playerController = FindObjectOfType<DustBunnyController>();

        if (blackBackgroundPanel != null)
            blackBackgroundPanel.color = backgroundColor;

        if (backgroundGroup != null) backgroundGroup.alpha = 0;
        if (singlePieceGroup != null) singlePieceGroup.alpha = 0;
        if (multiPieceGroup != null) multiPieceGroup.alpha = 0;

        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = false;
        if (singlePieceGroup != null) singlePieceGroup.blocksRaycasts = false;
        if (multiPieceGroup != null) multiPieceGroup.blocksRaycasts = false;

        if (instructionImage != null)
            instructionImage.gameObject.SetActive(false);
    }

    void Start()
    {
        ResetUI();
    }

    void ResetUI()
    {
        isPaperShowing = false;
        waitingForInput = false;
        isMultiPieceMode = false;
        currentPaperData = null;

        if (backgroundGroup != null) backgroundGroup.alpha = 0;
        if (singlePieceGroup != null) singlePieceGroup.alpha = 0;
        if (multiPieceGroup != null) multiPieceGroup.alpha = 0;

        if (instructionImage != null)
            instructionImage.gameObject.SetActive(false);
    }

    void Update()
    {
        if (isPaperShowing && waitingForInput)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                StartCoroutine(HidePaper());
            }
        }
    }

    // SINGLE PIECE

    public void ShowPaper(string text)
    {
        if (isPaperShowing) return;

        if (ObjectiveUI.Instance != null)
            ObjectiveUI.Instance.HideForPaper();

        isMultiPieceMode = false;
        StopAllCoroutines();
        StartCoroutine(DisplaySinglePiece(text, null));
    }

    public void ShowPaper(string text, Sprite customSprite)
    {
        if (isPaperShowing) return;

        if (ObjectiveUI.Instance != null)
            ObjectiveUI.Instance.HideForPaper();

        isMultiPieceMode = false;
        StopAllCoroutines();
        StartCoroutine(DisplaySinglePiece(text, customSprite));
    }

    private IEnumerator DisplaySinglePiece(string text, Sprite sprite)
    {
        isPaperShowing = true;
        waitingForInput = false;

        if (playerController != null)
            playerController.enabled = false;

        if (sprite != null && singlePieceImage != null)
            singlePieceImage.sprite = sprite;

        if (singlePieceText != null && !string.IsNullOrEmpty(text))
        {
            singlePieceText.text = text;
            singlePieceText.gameObject.SetActive(true);
        }
        else if (singlePieceText != null)
        {
            singlePieceText.gameObject.SetActive(false);
        }

        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = true;
        if (singlePieceGroup != null) singlePieceGroup.blocksRaycasts = true;

        while (backgroundGroup != null && backgroundGroup.alpha < 1f)
        {
            backgroundGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        while (singlePieceGroup != null && singlePieceGroup.alpha < 1f)
        {
            singlePieceGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);

        waitingForInput = true;

        if (instructionImage != null)
        {
            instructionImage.color = Color.white;
            instructionImage.gameObject.SetActive(true);
            StartCoroutine(BlinkInstructionImage());
        }
    }

    // MULTI PIECE

    public void ShowMultiPiecePaper(MultiPiecePaperData paperData, Sprite[] collectedSprites)
    {
        if (isPaperShowing) return;

        currentPaperData = paperData;

        if (ObjectiveUI.Instance != null)
            ObjectiveUI.Instance.HideForPaper();

        if (ObjectiveUI.Instance != null)
            ObjectiveUI.Instance.SetObjective();

        isMultiPieceMode = true;
        StopAllCoroutines();
        StartCoroutine(DisplayMultiPiece(paperData, collectedSprites));
    }

    private IEnumerator DisplayMultiPiece(MultiPiecePaperData paperData, Sprite[] collectedSprites)
    {
        isPaperShowing = true;
        waitingForInput = false;

        if (playerController != null)
            playerController.enabled = false;

        ClearPieceContainer();

        if (pieceContainer != null && piecePrefab != null)
        {
            for (int i = 0; i < collectedSprites.Length; i++)
            {
                if (collectedSprites[i] != null)
                    CreatePieceImage(collectedSprites[i]);
            }
        }

        if (progressText != null)
        {
            int collected = paperData.GetCollectedCount();
            int total = paperData.totalPieces;

            progressText.text = $"{collected}/{total} pieces collected";

            if (paperData.IsComplete())
                progressText.text += "\n<color=yellow>COMPLETE!</color>";
        }

        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = true;
        if (multiPieceGroup != null) multiPieceGroup.blocksRaycasts = true;

        while (backgroundGroup != null && backgroundGroup.alpha < 1f)
        {
            backgroundGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        while (multiPieceGroup != null && multiPieceGroup.alpha < 1f)
        {
            multiPieceGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);

        waitingForInput = true;

        if (instructionImage != null)
        {
            instructionImage.color = Color.white;
            instructionImage.gameObject.SetActive(true);
            StartCoroutine(BlinkInstructionImage());
        }
    }

    private void ClearPieceContainer()
    {
        if (pieceContainer == null) return;

        foreach (Transform child in pieceContainer)
            Destroy(child.gameObject);
    }

    private void CreatePieceImage(Sprite pieceSprite)
    {
        if (piecePrefab == null || pieceContainer == null) return;

        GameObject pieceObj = Instantiate(piecePrefab, pieceContainer);
        Image pieceImage = pieceObj.GetComponent<Image>();

        if (pieceImage != null)
        {
            pieceImage.sprite = pieceSprite;
            pieceImage.preserveAspect = true;

            RectTransform rectTransform = pieceObj.GetComponent<RectTransform>();

            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }

    // SHARED

    private IEnumerator HidePaper()
    {
        waitingForInput = false;

        CanvasGroup activeGroup = isMultiPieceMode ? multiPieceGroup : singlePieceGroup;

        while (activeGroup != null && activeGroup.alpha > 0f)
        {
            activeGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }

        while (backgroundGroup != null && backgroundGroup.alpha > 0f)
        {
            backgroundGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }

        if (backgroundGroup != null) backgroundGroup.blocksRaycasts = false;
        if (singlePieceGroup != null) singlePieceGroup.blocksRaycasts = false;
        if (multiPieceGroup != null) multiPieceGroup.blocksRaycasts = false;

        isPaperShowing = false;

        if (instructionImage != null)
            instructionImage.gameObject.SetActive(false);

        AkUnitySoundEngine.SetState("player_state", "None");

        if (playerController != null)
        {
            playerController.SuppressJumpForSeconds(0.2f);
            playerController.enabled = true;
        }

        if (ObjectiveUI.Instance != null)
            ObjectiveUI.Instance.ShowAfterPaper();
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

    public bool IsPaperShowing() => isPaperShowing;

    public void DismissPaper()
    {
        if (isPaperShowing)
        {
            uiSelect.Post(gameObject);
            StartCoroutine(HidePaper());
        }
    }
}