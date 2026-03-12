using UnityEngine;

public class HolepuncherAnimation : MonoBehaviour
{
    public string playerTag = "Player";
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            animator.SetTrigger("Swing");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            animator.SetTrigger("Swing");
        }
    }
}