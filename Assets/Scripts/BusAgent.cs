using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class BusAgent : Agent
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float turnSpeed = 80f;

    [Header("Movement Randomization")]
    public float minMoveSpeed = 7f;
    public float maxMoveSpeed = 9f;
    public float minTurnSpeed = 70f;
    public float maxTurnSpeed = 90f;

    [Header("Scene References")]
    public Transform startPoint;
    public Transform[] checkpoints;

    [Header("Spawn Randomization")]
    public float spawnPositionRandomRange = 1.0f;
    public float spawnYawRandomRange = 10f;
    public float maxCheckpointDistance = 30f;

    [Header("Rewards")]
    public float checkpointReward = 1.0f;
    public float lapReward = 2.0f;
    public float borderPenalty = -1.0f;
    public float stepPenalty = -0.0005f;
    public float progressRewardMultiplier = 0.01f;
    public float wrongCheckpointPenalty = -0.2f;
    public float stuckPenalty = -0.5f;
    public float sidewaysPenaltyMultiplier = -0.001f;

    //[Header("Stuck Detection")]
    //public float stuckSpeedThreshold = 0.1f;
    //public float stuckTimeLimit = 3f;

    private Rigidbody rb;
    private float moveInput;
    private float turnInput;
    private int currentCheckpointIndex;
    private float previousDistanceToCheckpoint;
    //private float stuckTimer;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnEpisodeBegin()
    {
        moveSpeed = Random.Range(minMoveSpeed, maxMoveSpeed);
        turnSpeed = Random.Range(minTurnSpeed, maxTurnSpeed);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.position = startPoint.position;
        rb.rotation = startPoint.rotation;

        moveInput = 0f;
        turnInput = 0f;
        //stuckTimer = 0f;
        currentCheckpointIndex = 0;

        if (checkpoints != null && checkpoints.Length > 0)
        {
            previousDistanceToCheckpoint = Vector3.Distance(
                rb.position,
                checkpoints[currentCheckpointIndex].position
            );
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        sensor.AddObservation(localVelocity.x / Mathf.Max(moveSpeed, 0.01f));
        sensor.AddObservation(localVelocity.z / Mathf.Max(moveSpeed, 0.01f));
        sensor.AddObservation(rb.angularVelocity.y / 5f);

        if (checkpoints != null && checkpoints.Length > 0)
        {
            Vector3 toCheckpoint = checkpoints[currentCheckpointIndex].position - transform.position;
            Vector3 localDir = transform.InverseTransformDirection(toCheckpoint.normalized);

            float distance = Mathf.Clamp(toCheckpoint.magnitude / maxCheckpointDistance, 0f, 1f);
            float angle = Vector3.SignedAngle(transform.forward, toCheckpoint.normalized, Vector3.up) / 180f;

            sensor.AddObservation(localDir.x);
            sensor.AddObservation(localDir.z);
            sensor.AddObservation(distance);
            sensor.AddObservation(angle);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int steerAction = actions.DiscreteActions[0];
        int driveAction = actions.DiscreteActions[1];

        moveInput = 0f;
        turnInput = 0f;

        if (steerAction == 1) turnInput = -1f;
        else if (steerAction == 2) turnInput = 1f;

        if (driveAction == 1) moveInput = 1f;

        Vector3 move = transform.forward * moveInput * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            Quaternion turn = Quaternion.Euler(0f, turnInput * turnSpeed * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * turn);
        }

        if (checkpoints != null && checkpoints.Length > 0)
        {
            float currentDistanceToCheckpoint = Vector3.Distance(
                transform.position,
                checkpoints[currentCheckpointIndex].position
            );

            float distanceDelta = previousDistanceToCheckpoint - currentDistanceToCheckpoint;

            if (distanceDelta > 0f)
                AddReward(distanceDelta * progressRewardMultiplier);

            previousDistanceToCheckpoint = currentDistanceToCheckpoint;
        }

        float sidewaysSpeed = Mathf.Abs(transform.InverseTransformDirection(rb.linearVelocity).x);
        AddReward(sidewaysSpeed * sidewaysPenaltyMultiplier);

        AddReward(stepPenalty);

        //if (rb.linearVelocity.magnitude < stuckSpeedThreshold)
        //    stuckTimer += Time.fixedDeltaTime;
        //else
        //    stuckTimer = 0f;

        //if (stuckTimer > stuckTimeLimit)
        //{
        //    AddReward(stuckPenalty);
        //    EndEpisode();
        //}
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        discreteActions[0] = 0;
        discreteActions[1] = 0;

        if (Input.GetKey(KeyCode.A)) discreteActions[0] = 1;
        else if (Input.GetKey(KeyCode.D)) discreteActions[0] = 2;

        if (Input.GetKey(KeyCode.W)) discreteActions[1] = 1;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Border"))
        {
            AddReward(borderPenalty);
            EndEpisode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Checkpoint") || checkpoints == null || checkpoints.Length == 0)
            return;

        if (other.transform == checkpoints[currentCheckpointIndex])
        {
            AddReward(checkpointReward);
            currentCheckpointIndex++;

            if (currentCheckpointIndex >= checkpoints.Length)
            {
                AddReward(lapReward);
                EndEpisode();
                return;
            }

            previousDistanceToCheckpoint = Vector3.Distance(
                transform.position,
                checkpoints[currentCheckpointIndex].position
            );
        }
        else
        {
            AddReward(wrongCheckpointPenalty);
        }
    }
}