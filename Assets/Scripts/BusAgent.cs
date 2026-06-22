using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class BusAgent : Agent
{
    public float moveSpeed = 8f;
    public float turnSpeed = 80f;
    public Transform startPoint;
    public Transform[] checkpoints;

    private Rigidbody rb;
    private float moveInput;
    private float turnInput;
    private int currentCheckpointIndex;
    private float previousDistanceToCheckpoint;
    private float stuckTimer;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
    }

    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.position = startPoint.position;
        transform.rotation = startPoint.rotation;

        moveInput = 0f;
        turnInput = 0f;
        stuckTimer = 0f;

        currentCheckpointIndex = 0;
        previousDistanceToCheckpoint = Vector3.Distance(transform.position, checkpoints[currentCheckpointIndex].position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        sensor.AddObservation(localVelocity.x);
        sensor.AddObservation(localVelocity.z);

        Vector3 directionToCheckpoint = checkpoints[currentCheckpointIndex].position - transform.position;
        Vector3 localDirection = transform.InverseTransformDirection(directionToCheckpoint.normalized);

        sensor.AddObservation(localDirection.x);
        sensor.AddObservation(localDirection.z);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int steerAction = actions.DiscreteActions[0];
        int driveAction = actions.DiscreteActions[1];

        moveInput = 0f;
        turnInput = 0f;

        if (driveAction == 1) moveInput = 1f;
        else if (driveAction == 2) moveInput = -1f;

        if (steerAction == 1) turnInput = -1f;
        else if (steerAction == 2) turnInput = 1f;

        Vector3 move = transform.forward * moveInput * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            Quaternion turn = Quaternion.Euler(0f, turnInput * turnSpeed * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * turn);
        }

        float currentDistance = Vector3.Distance(transform.position, checkpoints[currentCheckpointIndex].position);
        float distanceDelta = previousDistanceToCheckpoint - currentDistance;

        AddReward(distanceDelta * 0.01f);
        previousDistanceToCheckpoint = currentDistance;

        AddReward(-0.0005f);

        if (Mathf.Abs(moveInput) < 0.01f)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;

        if (stuckTimer > 3f)
        {
            AddReward(-0.5f);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        discreteActions[0] = 0;
        discreteActions[1] = 0;

        if (Input.GetKey(KeyCode.A)) discreteActions[0] = 1;
        else if (Input.GetKey(KeyCode.D)) discreteActions[0] = 2;

        if (Input.GetKey(KeyCode.W)) discreteActions[1] = 1;
        else if (Input.GetKey(KeyCode.S)) discreteActions[1] = 2;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Border"))
        {
            AddReward(-1f);
            Debug.Log("Collided with border, ending episode.");
            EndEpisode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Checkpoint"))
        {
            if (other.transform == checkpoints[currentCheckpointIndex])
            {
                AddReward(1f);

                currentCheckpointIndex++;

                if (currentCheckpointIndex >= checkpoints.Length)
                {
                    AddReward(2f);
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
                AddReward(-0.2f);
            }
        }
    }
}