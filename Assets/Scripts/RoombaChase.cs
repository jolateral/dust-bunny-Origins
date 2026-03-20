using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RoombaChase : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform respawnPoint;

    [Header("Patrol")]
    [SerializeField] private Transform patrolPointA;
    [SerializeField] private Transform patrolPointB;
    [SerializeField] private float patrolStopDistance = 0.2f;

    private DustBunnyController playerController;
    private Rigidbody rb;
    private Transform currentPatrolTarget;

    [Header("Chase Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stopDistance = 1.2f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (player != null)
            playerController = player.GetComponent<DustBunnyController>();

        rb.freezeRotation = true;

        if (patrolPointA != null)
            currentPatrolTarget = patrolPointA;
    }

    private void FixedUpdate()
    {
        if (player == null || playerController == null)
            return;

        if (playerController.isOutOfBounds)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }
    }

    private void ChasePlayer()
    {
        moveSpeed = 100f;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        float distance = toPlayer.magnitude;

        if (distance <= stopDistance)
        {
            StopMoving();
            return;
        }

        Vector3 moveDir = toPlayer.normalized;
        MoveInDirection(moveDir);
    }

    private void Patrol()
    {
        moveSpeed = 30f;
        if (patrolPointA == null || patrolPointB == null)
        {
            StopMoving();
            return;
        }

        if (currentPatrolTarget == null)
            currentPatrolTarget = patrolPointA;

        Vector3 toTarget = currentPatrolTarget.position - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;

        if (distance <= patrolStopDistance)
        {
            currentPatrolTarget = currentPatrolTarget == patrolPointA ? patrolPointB : patrolPointA;
            StopMoving();
            return;
        }

        Vector3 moveDir = toTarget.normalized;
        MoveInDirection(moveDir);
    }

    private void MoveInDirection(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude <= 0.0001f)
        {
            StopMoving();
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector3(
            moveDir.x * moveSpeed,
            rb.linearVelocity.y,
            moveDir.z * moveSpeed
        );
    }

    private void StopMoving()
    {
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryRespawnPlayer(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRespawnPlayer(other);
    }

    void TryRespawnPlayer(Collider other)
    {
        if (other == null || player == null || respawnPoint == null)
            return;

        if (other.transform != player && !other.transform.IsChildOf(player))
            return;

        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
            playerRb.linearVelocity = Vector3.zero;

        player.position = respawnPoint.position;
        player.rotation = respawnPoint.rotation;

        if (playerController != null)
            playerController.isOutOfBounds = false;
    }
}