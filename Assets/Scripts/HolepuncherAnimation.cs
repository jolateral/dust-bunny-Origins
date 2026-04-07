using UnityEngine;

public class HolepuncherAnimation : MonoBehaviour
{
    public string playerTag = "Player";
    private Animator animator;

    public AK.Wwise.Event holePunch;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            holePunch.Post(gameObject);
            animator.SetTrigger("Swing");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            holePunch.Post(gameObject);
            animator.SetTrigger("Swing");
        }
    }
}