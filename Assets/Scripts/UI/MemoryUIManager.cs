using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class MemoryUIManager : MonoBehaviour
{
    public static MemoryUIManager Instance;

    public TextMeshProUGUI displayUI;
    public CanvasGroup uiGroup; // Used for fading

    [Header("Optional TMP Sprites")]
    public TMP_SpriteAsset spriteAsset;
    public Image displayImage;

    void Awake()
    {
        Instance = this;
        if (uiGroup != null) uiGroup.alpha = 0; // Hide at start
    }

    public void ShowMemory(string text, Color color)
    {
        StopAllCoroutines();

        displayImage.gameObject.SetActive(false);
        displayUI.gameObject.SetActive(true);

        StartCoroutine(DisplayRoutine(text, color));
    }

    public void ShowImage(Sprite sprite)
    {
        StopAllCoroutines();

        displayUI.gameObject.SetActive(false);
        displayImage.gameObject.SetActive(true);
        displayImage.sprite = sprite;

        StartCoroutine(DisplayImageRoutine());
    }

    public void Hide()
    {
        StopAllCoroutines();
        if (uiGroup != null)
            uiGroup.alpha = 0;
        if (displayUI != null)
            displayUI.text = "";
    }

    IEnumerator DisplayImageRoutine()
    {
        yield return StartCoroutine(FadeRoutine(6f));
    }

    IEnumerator FadeRoutine(float displayTime)
    {
        // Fade in
        while (uiGroup.alpha < 1)
        {
            uiGroup.alpha += Time.deltaTime * 2f;
            yield return null;
        }

        yield return new WaitForSeconds(displayTime);

        // Fade out
        while (uiGroup.alpha > 0)
        {
            uiGroup.alpha -= Time.deltaTime * 1f;
            yield return null;
        }
    }

    IEnumerator DisplayRoutine(string text, Color color)
    {
        if (displayUI == null || uiGroup == null) yield break;

        if (spriteAsset != null)
            displayUI.spriteAsset = spriteAsset;

        displayUI.richText = true; 
        displayUI.text = text;
        displayUI.color = color;

        // Fade In
        while (uiGroup.alpha < 1)
        {
            uiGroup.alpha += Time.deltaTime * 2f;
            yield return null;
        }

        yield return new WaitForSeconds(6f);

        // Fade Out
        while (uiGroup.alpha > 0)
        {
            uiGroup.alpha -= Time.deltaTime * 1f;
            yield return null;
        }
    }
}