using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[System.Serializable]
public class EndingLine
{
    [TextArea]
    public string text;

    public Sprite image;

    public Vector2 textPosition;
    public Vector2 imagePosition;
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
            // TEXT
            if (!string.IsNullOrEmpty(line.text))
            {
                endingText.gameObject.SetActive(true);
                endingText.text = line.text;
                endingText.rectTransform.anchoredPosition = line.textPosition;
            }
            else
            {
                endingText.gameObject.SetActive(false);
            }

            // IMAGE
            if (line.image != null)
            {
                endingImage.gameObject.SetActive(true);
                endingImage.sprite = line.image;
                endingImage.rectTransform.anchoredPosition = line.imagePosition;
            }
            else
            {
                endingImage.gameObject.SetActive(false);
            }

            // Fade BOTH in
            yield return StartCoroutine(FadeInCurrent(line));

            yield return new WaitForSeconds(displayTime);

            // Fade BOTH out
            yield return StartCoroutine(FadeOutCurrent(line));
        }

        yield return new WaitForSeconds(delayBeforeReturn);
        FadeSequenceManager.Instance.FadeToScene(returnScene, fadeTime);
    }

    IEnumerator FadeInCurrent(EndingLine line)
    {
        float t = 0;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float alpha = Mathf.SmoothStep(0, 1, t / fadeTime);

            if (!string.IsNullOrEmpty(line.text))
                SetAlpha(endingText, alpha);

            if (line.image != null)
                SetAlpha(endingImage, alpha);

            yield return null;
        }
    }

    IEnumerator FadeOutCurrent(EndingLine line)
    {
        float t = 0;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float alpha = Mathf.SmoothStep(1, 0, t / fadeTime);

            if (!string.IsNullOrEmpty(line.text))
                SetAlpha(endingText, alpha);

            if (line.image != null)
                SetAlpha(endingImage, alpha);

            yield return null;
        }
    }

    void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }
}