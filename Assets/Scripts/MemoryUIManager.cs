using UnityEngine;
using TMPro;
using System.Collections;

public class MemoryUIManager : MonoBehaviour
{
    public static MemoryUIManager Instance;

    public TextMeshProUGUI displayUI;
    public CanvasGroup uiGroup; // Used for fading

    [Header("Optional TMP Sprites")]
    public TMP_SpriteAsset spriteAsset; 

    void Awake()
    {
        Instance = this;
        if (uiGroup != null) uiGroup.alpha = 0; // Hide at start
    }

    public void ShowMemory(string text, Color color)
    {
        StopAllCoroutines();
        StartCoroutine(DisplayRoutine(text, color));
    }

    public void Hide()
    {
        StopAllCoroutines();
        if (uiGroup != null)
            uiGroup.alpha = 0;
        if (displayUI != null)
            displayUI.text = "";
    }

    IEnumerator DisplayRoutine(string text, Color color)
    {
        if (displayUI == null || uiGroup == null) yield break;

        if (spriteAsset != null)
            displayUI.spriteAsset = spriteAsset;

        displayUI.richText = true; // should already be true, but safe
        displayUI.text = text;
        displayUI.color = color;

        // Fade In
        while (uiGroup.alpha < 1)
        {
            uiGroup.alpha += Time.deltaTime * 2f;
            yield return null;
        }

        yield return new WaitForSeconds(3f);

        // Fade Out
        while (uiGroup.alpha > 0)
        {
            uiGroup.alpha -= Time.deltaTime * 1f;
            yield return null;
        }
    }
}