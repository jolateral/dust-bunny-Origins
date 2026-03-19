// =============================================================================
// FadeSequenceManager.cs
// -----------------------------------------------------------------------------
// Handles scene transitions with a fade in/out effect.
// Persists across scenes via DontDestroyOnLoad.
//
// CHANGES FROM ORIGINAL:
//   - OnSceneLoaded now calls ResetAllManagers() when the Level scene loads.
//     This ensures all UI managers reset their state cleanly every time the
//     level is entered, whether coming from the main menu or the pause screen.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class FadeSequenceManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------

    public static FadeSequenceManager Instance;

    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Fade Settings")]
    public Image fadeImage;
    public float defaultFadeTime = 1f;

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    bool isFading;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

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

    void OnDestroy()
    {
        // Always unsubscribe to prevent memory leaks when the object is destroyed.
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // -------------------------------------------------------------------------
    // Scene Loaded Callback
    // -------------------------------------------------------------------------

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Keep the fade image on top of everything in the new scene.
        if (fadeImage != null)
            fadeImage.transform.SetAsLastSibling();

        // Reset all UI managers whenever the Level scene is loaded.
        // This covers both cases: coming from the main menu AND coming from
        // the pause screen's "Main Menu → Play Again" flow.
        if (scene.name == "Level")
        {
            // Wait one frame before resetting so all Awake() and Start() calls
            // in the new scene have finished initialising first.
            StartCoroutine(ResetAllManagersNextFrame());
        }
    }

    /// <summary>
    /// Waits one frame then resets all UI managers.
    /// The one-frame delay ensures every manager's Awake() has run before we
    /// try to call Reset on them.
    /// </summary>
    private IEnumerator ResetAllManagersNextFrame()
    {
        yield return null; // wait one frame

        ResetAllManagers();
    }

    /// <summary>
    /// Calls ResetState() on every UI manager that needs to be cleaned up
    /// when the level reloads. Add more managers here if needed in future.
    /// </summary>
    private void ResetAllManagers()
    {
        if (PaperUIManager.Instance != null)
        {
            PaperUIManager.Instance.ResetState();
            Debug.Log("[FadeSequenceManager] PaperUIManager reset.");
        }

        if (DiaryUIManager.Instance != null)
        {
            DiaryUIManager.Instance.ResetState();
            Debug.Log("[FadeSequenceManager] DiaryUIManager reset.");
        }

        if (ObjectiveUI.Instance != null)
        {
            ObjectiveUI.Instance.ResetState();
            Debug.Log("[FadeSequenceManager] ObjectiveUI reset.");
        }

        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.ResetState();
            Debug.Log("[FadeSequenceManager] PauseManager reset.");
        }

        // Reset all MultiPiecePaperData ScriptableObjects.
        // ScriptableObjects persist their runtime data between scene loads even
        // when marked [System.NonSerialized] — we must clear them manually here.
        MultiPiecePaperData[] allPaperData = Resources.FindObjectsOfTypeAll<MultiPiecePaperData>();
        foreach (MultiPiecePaperData paperData in allPaperData)
        {
            paperData.ResetProgress();
            Debug.Log($"[FadeSequenceManager] Reset MultiPiecePaperData: {paperData.paperID}");
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void FadeToScene(string sceneName, float fadeTime = -1)
    {
        if (isFading) return;
        if (fadeTime < 0) fadeTime = defaultFadeTime;
        StartCoroutine(FadeAndSwitchScenes(sceneName, fadeTime));
    }

    // -------------------------------------------------------------------------
    // Private Coroutines
    // -------------------------------------------------------------------------

    IEnumerator FadeAndSwitchScenes(string sceneName, float fadeTime)
    {
        isFading = true;
        yield return StartCoroutine(Fade(0, 1, fadeTime));

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);

        while (!loadOperation.isDone)
            yield return null;

        yield return null; // extra frame to stabilize

        yield return StartCoroutine(Fade(1, 0, fadeTime));
        isFading = false;
    }

    IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        if (fadeImage == null) yield break;

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