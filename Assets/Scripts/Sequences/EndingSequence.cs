using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[System.Serializable]
public class EndingLine
{
    public enum LineType { Text, Image }

    public LineType type;

    [TextArea]
    public string text;

    public Sprite image;

    public Vector2 screenPosition;
}

public class EndingSequence : MonoBehaviour
{
    public TextMeshProUGUI endingText;
    public Image endingImage;

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
            // Reset both
            endingText.gameObject.SetActive(false);
            endingImage.gameObject.SetActive(false);

            if (line.type == EndingLine.LineType.Text)
            {
                endingText.gameObject.SetActive(true);
                endingText.text = line.text;
                endingText.rectTransform.anchoredPosition = line.screenPosition;

                yield return StartCoroutine(FadeGraphic(endingText, 0, 1));
                yield return new WaitForSeconds(displayTime);
                yield return StartCoroutine(FadeGraphic(endingText, 1, 0));
            }
            else if (line.type == EndingLine.LineType.Image)
            {
                endingImage.gameObject.SetActive(true);
                endingImage.sprite = line.image;
                endingImage.rectTransform.anchoredPosition = line.screenPosition;

                yield return StartCoroutine(FadeGraphic(endingImage, 0, 1));
                yield return new WaitForSeconds(displayTime);
                yield return StartCoroutine(FadeGraphic(endingImage, 1, 0));
            }
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

    IEnumerator FadeGraphic(Graphic graphic, float start, float end)
    {
        float t = 0;
        Color color = graphic.color;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(start, end, Mathf.SmoothStep(0, 1, t / fadeTime));
            color.a = alpha;
            graphic.color = color;
            yield return null;
        }

        color.a = end;
        graphic.color = color;
    }
}