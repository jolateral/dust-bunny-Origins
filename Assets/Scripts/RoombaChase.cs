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
    [SerializeField] private float patrolStopDistance = 0.5f;

    [Header("First Chase Delay")]
    [SerializeField] private float firstChaseDelay = 1f;

    private DustBunnyController playerController;
    private Rigidbody rb;
    private Transform currentPatrolTarget;

    [Header("Chase Settings")]
    [SerializeField] private float patrolSpeed = 6f;
    [SerializeField] private float chaseSpeed = 10f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stopDistance = 1.2f;

    [Header("SFX")]
    public AK.Wwise.Event roombaLoop;
    public AK.Wwise.Event roombaImpact;

    private bool hasStartedFirstChase = false;
    private bool firstOutOfBoundsTriggered = false;
    private float firstChaseStartTime = -1f;

    private bool justReachedPatrolPoint = false;

    private void Start()
    {
        roombaLoop.Post(gameObject);

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
            HandleChaseState();
        else
            Patrol();
    }

    private void HandleChaseState()
    {
        justReachedPatrolPoint = false;

        if (!hasStartedFirstChase)
        {
            if (!firstOutOfBoundsTriggered)
            {
                firstOutOfBoundsTriggered = true;
                firstChaseStartTime = Time.time + firstChaseDelay;
            }

            if (Time.time < firstChaseStartTime)
            {
                Patrol();
                return;
            }

            hasStartedFirstChase = true;
        }

        ChasePlayer();
    }

    private void ChasePlayer()
    {
        float moveSpeed = chaseSpeed;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        float distance = toPlayer.magnitude;
        AkUnitySoundEngine.SetRTPCValue("roomba_to_bunny", distance);

        if (distance <= stopDistance)
        {
            StopMoving();
            return;
        }

        Vector3 moveDir = toPlayer.normalized;
        MoveInDirection(moveDir, moveSpeed);
    }

    private void Patrol()
    {
        AkUnitySoundEngine.SetRTPCValue("roomba_to_bunny", 200f);

        if (patrolPointA == null || patrolPointB == null)
        {
            StopMoving();
            return;
        }

        if (currentPatrolTarget == null)
            currentPatrolTarget = patrolPointA;

        Vector3 flatPos = transform.position;
        flatPos.y = 0f;

        Vector3 targetPos = currentPatrolTarget.position;
        targetPos.y = 0f;

        Vector3 toTarget = targetPos - flatPos;
        float distance = toTarget.magnitude;

        if (distance <= patrolStopDistance)
        {
            // Snap cleanly to the patrol point on XZ so it doesn't hover near the threshold
            transform.position = new Vector3(
                currentPatrolTarget.position.x,
                transform.position.y,
                currentPatrolTarget.position.z
            );

            StopMoving();

            if (!justReachedPatrolPoint)
            {
                justReachedPatrolPoint = true;
                currentPatrolTarget = currentPatrolTarget == patrolPointA ? patrolPointB : patrolPointA;
            }

            return;
        }

        justReachedPatrolPoint = false;

        Vector3 moveDir = toTarget.normalized;
        MoveInDirection(moveDir, patrolSpeed);
    }

    private void MoveInDirection(Vector3 moveDir, float moveSpeed)
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

    private void TryRespawnPlayer(Collider other)
    {
        if (other == null || player == null || respawnPoint == null)
            return;

        if (other.transform != player && !other.transform.IsChildOf(player))
            return;

        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            roombaImpact.Post(gameObject);
            playerRb.linearVelocity = Vector3.zero;
        }

        player.position = respawnPoint.position;
        player.rotation = respawnPoint.rotation;

        AkUnitySoundEngine.SetState("mus_state", "zone2");

        if (playerController != null)
            playerController.ClearOutOfBoundsState();
    }
}