using UnityEngine;

using UnityEngine;
using System.Collections;

public class RandomAmbientEmitter : MonoBehaviour
{
    public AK.Wwise.Event playEvent;

    [Header("Timing")]
    public float minDelay = 5f;
    public float maxDelay = 20f;

    [Header("Chance to Play")]
    [Range(0f, 1f)]
    public float playChance = 0.8f;

    private void Start()
    {
        StartCoroutine(PlayLoop());
    }

    IEnumerator PlayLoop()
    {
        while (true)
        {
            float delay = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(delay);

            if (Random.value <= playChance)
            {
                playEvent.Post(gameObject);
                Debug.Log("Played " + playEvent);
            }
        }
    }
}