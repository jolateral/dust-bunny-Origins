using UnityEngine;
using TMPro;
using System.Collections;

public class MemoryUIManager : MonoBehaviour
{
    public static MemoryUIManager Instance;

    public TextMeshProUGUI displayUI;
    public CanvasGroup uiGroup; // Used for fading

    void Awake()
    {
        Instance = this;
        uiGroup.alpha = 0; // Hide at start
    }

    public void ShowMemory(string text, Color color)
    {
        StopAllCoroutines();
        StartCoroutine(DisplayRoutine(text, color));
    }

    IEnumerator DisplayRoutine(string text, Color color)
    {
        displayUI.text = text;
        displayUI.color = color;

        // Fade In
        while (uiGroup.alpha < 1)
        {
            uiGroup.alpha += Time.deltaTime * 2f;
            yield return null;
        }

        yield return new WaitForSeconds(3f); // Wait 3 seconds

        // Fade Out
        while (uiGroup.alpha > 0)
        {
            uiGroup.alpha -= Time.deltaTime * 1f;
            yield return null;
        }
    }
}