using UnityEngine;
using TMPro;
using System.Collections;

[System.Serializable]
public class EndingLine
{
    [TextArea]
    public string text;

    public Vector2 screenPosition;
}

public class EndingSequence : MonoBehaviour
{
    public TextMeshProUGUI endingText;

    public EndingLine[] lines;

    public float fadeTime = 2f;
    public float displayTime = 4f;

    public string returnScene = "StartMenu";
    public float delayBeforeReturn = 7f;

    public AK.Wwise.Event endSequence;

    void Start()
    {
        endSequence.Post(gameObject);
        StartCoroutine(PlaySequence());
    }
    IEnumerator PlaySequence()
    {
        foreach (EndingLine line in lines)
        {
            endingText.text = line.text;

            // move text to desired screen position
            endingText.rectTransform.anchoredPosition = line.screenPosition;

            yield return StartCoroutine(FadeText(0, 1));
            yield return new WaitForSeconds(displayTime);
            yield return StartCoroutine(FadeText(1, 0));
        }

        yield return new WaitForSeconds(delayBeforeReturn);

        FadeSequenceManager.Instance.FadeToScene(returnScene, fadeTime);
    }
    IEnumerator FadeText(float start, float end)
    {
        float t = 0;
        Color color = endingText.color;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(start, end, Mathf.SmoothStep(0, 1, t / fadeTime));
            color.a = alpha;
            endingText.color = color;
            yield return null;
        }

        color.a = end;
        endingText.color = color;
    }
}