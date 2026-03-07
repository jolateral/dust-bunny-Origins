using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class FadeSequenceManager : MonoBehaviour
{
    public static FadeSequenceManager Instance;

    [Header("Fade Settings")]
    public Image fadeImage;
    public float defaultFadeTime = 1f;

    bool isFading;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (fadeImage != null)
            StartCoroutine(Fade(1, 0, defaultFadeTime));
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Ensure fade image is always on top after scene loads
        if (fadeImage != null)
            fadeImage.transform.SetAsLastSibling();
    }

    public void FadeToScene(string sceneName, float fadeTime = -1)
    {
        if (isFading) return;

        if (fadeTime < 0)
            fadeTime = defaultFadeTime;

        StartCoroutine(FadeAndSwitchScenes(sceneName, fadeTime));
    }

    IEnumerator FadeAndSwitchScenes(string sceneName, float fadeTime)
    {
        isFading = true;

        yield return StartCoroutine(Fade(0, 1, fadeTime));

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);

        // Wait until the scene is fully loaded
        while (!loadOperation.isDone)
            yield return null;

        yield return null; // extra frame to stabilize

        yield return StartCoroutine(Fade(1, 0, fadeTime));

        isFading = false;
    }

    IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        if (fadeImage == null)
            yield break;

        float time = 0;
        Color color = fadeImage.color;

        while (time < duration)
        {
            time += Time.deltaTime;

            float alpha = Mathf.Lerp(startAlpha, endAlpha, time / duration);
            color.a = alpha;
            fadeImage.color = color;

            yield return null;
        }

        color.a = endAlpha;
        fadeImage.color = color;
    }
}