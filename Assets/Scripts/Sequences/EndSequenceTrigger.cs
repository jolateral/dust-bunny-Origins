using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

public class EndSequenceTrigger : MonoBehaviour
{
    [Header("Scene")]
    public string endingScene = "EndSeq";

    [Header("Timing")]
    public float delayBeforeFade = 2f;
    public float fadeDuration = 2f;

    public AK.Wwise.Event stopMusic;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            stopMusic.Post(gameObject);

            triggered = true;
            StartCoroutine(EndSequence());
        }
    }

    IEnumerator EndSequence()
    {
        // Wait before fading
        yield return new WaitForSeconds(delayBeforeFade);

        // Fade to ending scene
        FadeSequenceManager.Instance.FadeToScene(endingScene, fadeDuration);
    }
}