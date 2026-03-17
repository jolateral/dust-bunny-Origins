using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class IndoorAmbientEmitter : MonoBehaviour
{
    [Header("Path Points")]
    public List<Transform> points;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float waitAtPointMin = 1f;
    public float waitAtPointMax = 5f;

    [Header("Sound")]
    public AK.Wwise.Event playEvent;
    public bool playWhileMoving = true;
    public float playIntervalMin = 2f;
    public float playIntervalMax = 30f;

    private Transform currentTarget;

    void Start()
    {
        PickNewTarget();
        StartCoroutine(MoveLoop());
        StartCoroutine(SoundLoop());
    }

    void PickNewTarget()
    {
        if (points.Count == 0) return;
        currentTarget = points[Random.Range(0, points.Count)];
    }

    IEnumerator MoveLoop()
    {
        while (true)
        {
            if (currentTarget == null)
                yield break;

            while (Vector3.Distance(transform.position, currentTarget.position) > 0.1f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    currentTarget.position,
                    moveSpeed * Time.deltaTime
                );

                yield return null;
            }

            // Wait at point
            float waitTime = Random.Range(waitAtPointMin, waitAtPointMax);
            yield return new WaitForSeconds(waitTime);

            PickNewTarget();
        }
    }

    IEnumerator SoundLoop()
    {
        while (true)
        {
            float delay = Random.Range(playIntervalMin, playIntervalMax);
            yield return new WaitForSeconds(delay);

            if (playWhileMoving || Vector3.Distance(transform.position, currentTarget.position) < 0.2f)
            {
                playEvent.Post(gameObject);
                Debug.Log("Played " + playEvent);
            }
        }
    }
}
