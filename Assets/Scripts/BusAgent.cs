using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class TrainingBusAgent : Agent
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float turnSpeed = 80f;

    [Header("Scene References")]
    public Transform startPoint;
    public PathFinder pathFinder;

    [Header("Route Settings")]
    public float maxNodeDistance = 30f;
    public float nodeReachDistance = 3.0f;
    public int routeLookAhead = 2;
    public bool requireTriggerToAdvanceNode = false;

    [Header("Shared Route Training")]
    public Transform[] busStops;
    public bool avoidChoosingSameStopTwice = true;
    public int completionsBeforeRouteChange = 10;
    public bool synchronizeRouteAcrossEnvironments = true;

    [Header("Rewards")]
    public float nodeReward = 2f;
    public float routeCompleteReward = 5f;
    public float borderPenalty = -1.0f;
    public float stepPenalty = -0.005f;
    public float progressRewardMultiplier = 0.02f;
    public float regressPenaltyMultiplier = -0.025f;
    public float headingRewardMultiplier = 0.0025f;
    public float wrongDirectionPenaltyMultiplier = -0.004f;
    public float wrongNodeTriggerPenalty = -0.1f;
    public float stuckPenalty = -0.5f;
    public float sidewaysPenaltyMultiplier = -0.001f;

    [Header("Stuck Detection")]
    public bool enableStuckDetection = true;
    public float stuckMovementThreshold = 0.02f;
    public float stuckTimeLimit = 3f;
    public float stuckCheckGracePeriod = 1f;
    public float minDriveInputForStuckCheck = 0.5f;

    [Header("Node Stall Detection")]
    public bool enableNodeStallDetection = true;
    public float nodeProgressEpsilon = 0.15f;
    public float nodeStallTimeLimit = 2.5f;
    public float nodeStallPenalty = -0.75f;

    private static int sharedBusStopIndex = -1;
    private static int sharedPreviousBusStopIndex = -1;
    private static int sharedRouteCompletionCount = 0;

    private Transform currentBusStopTarget;
    private int currentBusStopIndex = -1;

    private float bestDistanceToCurrentNode;
    private float nodeStallTimer;

    private Rigidbody rb;
    private float moveInput;
    private float turnInput;
    private float previousDistanceToNode;
    private float stuckTimer;
    private float episodeTimer;
    private Vector3 previousPosition;
    private bool episodeEnding;

    private List<PathNode> currentRoute = new List<PathNode>();
    private int currentRouteIndex = 0;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnEpisodeBegin()
    {
        episodeEnding = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.position = startPoint.position;
        rb.rotation = startPoint.rotation;

        Physics.SyncTransforms();

        moveInput = 0f;
        turnInput = 0f;
        stuckTimer = 0f;
        episodeTimer = 0f;
        previousPosition = rb.position;
        currentRouteIndex = 0;

        SelectSharedTrainingRoute();
        BuildRouteToCurrentBusStop();

        if (HasValidCurrentNode())
        {
            previousDistanceToNode = Vector3.Distance(rb.position, GetCurrentNodePosition());
            bestDistanceToCurrentNode = previousDistanceToNode;
            nodeStallTimer = 0f;
        }
        else
        {
            previousDistanceToNode = 0f;
            bestDistanceToCurrentNode = 0f;
            nodeStallTimer = 0f;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        sensor.AddObservation(localVelocity.x / Mathf.Max(moveSpeed, 0.01f));
        sensor.AddObservation(localVelocity.z / Mathf.Max(moveSpeed, 0.01f));
        sensor.AddObservation(rb.angularVelocity.y / 5f);

        if (!HasValidCurrentNode())
        {
            for (int i = 0; i < routeLookAhead; i++)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
            return;
        }

        for (int i = 0; i < routeLookAhead; i++)
        {
            int nodeIndex = currentRouteIndex + i;

            if (nodeIndex >= currentRoute.Count || currentRoute[nodeIndex] == null)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                continue;
            }

            Vector3 toNode = currentRoute[nodeIndex].transform.position - transform.position;
            float distanceToNode = toNode.magnitude;

            Vector3 direction = distanceToNode > 0.001f
                ? toNode / distanceToNode
                : transform.forward;

            Vector3 localDir = transform.InverseTransformDirection(direction);
            float normalizedDistance = Mathf.Clamp(distanceToNode / Mathf.Max(maxNodeDistance, 0.01f), 0f, 1f);
            float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up) / 180f;

            sensor.AddObservation(localDir.x);
            sensor.AddObservation(localDir.z);
            sensor.AddObservation(normalizedDistance);
            sensor.AddObservation(angle);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (episodeEnding)
            return;

        episodeTimer += Time.fixedDeltaTime;

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

        ApplyRouteRewards();

        float sidewaysSpeed = Mathf.Abs(transform.InverseTransformDirection(rb.linearVelocity).x);
        AddReward(sidewaysSpeed * sidewaysPenaltyMultiplier);
        AddReward(stepPenalty);

        UpdateStuckDetection();
    }

    private void ApplyRouteRewards()
    {
        if (!HasValidCurrentNode())
            return;

        Vector3 currentTarget = GetCurrentNodePosition();
        float currentDistance = Vector3.Distance(transform.position, currentTarget);
        float distanceDelta = previousDistanceToNode - currentDistance;

        UpdateNodeStallDetection(currentDistance);

        if (distanceDelta > 0f)
        {
            AddReward(distanceDelta * progressRewardMultiplier);
        }
        else if (distanceDelta < 0f)
        {
            AddReward(Mathf.Abs(distanceDelta) * regressPenaltyMultiplier);
        }

        Vector3 toNode = currentTarget - transform.position;
        Vector3 toNodeDir = toNode.sqrMagnitude > 0.0001f ? toNode.normalized : transform.forward;
        float headingDot = Vector3.Dot(transform.forward, toNodeDir);

        if (headingDot > 0f)
        {
            AddReward(headingDot * headingRewardMultiplier);
        }
        else
        {
            AddReward(Mathf.Abs(headingDot) * wrongDirectionPenaltyMultiplier);
        }

        previousDistanceToNode = currentDistance;

        if (!requireTriggerToAdvanceNode && currentDistance <= nodeReachDistance)
        {
            AdvanceToNextRouteNode();
        }
    }

    private void UpdateNodeStallDetection(float currentDistance)
    {
        if (!enableNodeStallDetection || episodeEnding || !HasValidCurrentNode())
            return;

        if (currentDistance < bestDistanceToCurrentNode - nodeProgressEpsilon)
        {
            bestDistanceToCurrentNode = currentDistance;
            nodeStallTimer = 0f;
            return;
        }

        if (moveInput > 0.01f || Mathf.Abs(turnInput) > 0.01f)
        {
            nodeStallTimer += Time.fixedDeltaTime;
        }

        if (nodeStallTimer >= nodeStallTimeLimit)
        {
            AddReward(nodeStallPenalty);
            SafeEndEpisode();
        }
    }

    private void AdvanceToNextRouteNode()
    {
        if (!HasValidCurrentNode())
            return;

        AddReward(nodeReward);
        currentRouteIndex++;

        if (currentRouteIndex >= currentRoute.Count)
        {
            AddReward(routeCompleteReward);

            if (synchronizeRouteAcrossEnvironments)
            {
                sharedRouteCompletionCount++;

                if (sharedRouteCompletionCount >= Mathf.Max(1, completionsBeforeRouteChange))
                {
                    sharedRouteCompletionCount = 0;
                    PickNewSharedRoute();
                }
            }

            SafeEndEpisode();
            return;
        }

        previousDistanceToNode = Vector3.Distance(transform.position, GetCurrentNodePosition());
        bestDistanceToCurrentNode = previousDistanceToNode;
        nodeStallTimer = 0f;
    }

    private void UpdateStuckDetection()
    {
        if (!enableStuckDetection || episodeEnding)
            return;

        if (episodeTimer < stuckCheckGracePeriod)
        {
            previousPosition = rb.position;
            stuckTimer = 0f;
            return;
        }

        if (moveInput < minDriveInputForStuckCheck)
        {
            previousPosition = rb.position;
            stuckTimer = 0f;
            return;
        }

        float movedDistance = Vector3.Distance(rb.position, previousPosition);

        if (movedDistance < stuckMovementThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
        }
        else
        {
            stuckTimer = 0f;
        }

        previousPosition = rb.position;

        if (stuckTimer >= stuckTimeLimit)
        {
            AddReward(stuckPenalty);
            SafeEndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (episodeEnding)
            return;

        if (collision.gameObject.CompareTag("Border"))
        {
            AddReward(borderPenalty);
            SafeEndEpisode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (episodeEnding || !requireTriggerToAdvanceNode || !HasValidCurrentNode())
            return;

        Transform expectedNode = GetCurrentNodeTransform();

        if (other.transform == expectedNode)
        {
            AdvanceToNextRouteNode();
        }
        else
        {
            AddReward(wrongNodeTriggerPenalty);
        }
    }

    private void SelectSharedTrainingRoute()
    {
        if (busStops == null || busStops.Length == 0)
        {
            currentBusStopTarget = null;
            currentBusStopIndex = -1;
            return;
        }

        if (!synchronizeRouteAcrossEnvironments)
        {
            if (currentBusStopIndex < 0)
            {
                currentBusStopIndex = GetRandomBusStopIndex(currentBusStopIndex);
            }

            currentBusStopTarget = busStops[currentBusStopIndex];
            return;
        }

        if (sharedBusStopIndex < 0)
        {
            PickNewSharedRoute();
        }

        currentBusStopIndex = sharedBusStopIndex;
        currentBusStopTarget = busStops[currentBusStopIndex];
    }

    private void PickNewSharedRoute()
    {
        sharedBusStopIndex = GetRandomBusStopIndex(sharedPreviousBusStopIndex);
        sharedPreviousBusStopIndex = sharedBusStopIndex;
    }

    private int GetRandomBusStopIndex(int previousIndex)
    {
        int nextIndex = Random.Range(0, busStops.Length);

        if (avoidChoosingSameStopTwice && busStops.Length > 1)
        {
            while (nextIndex == previousIndex)
            {
                nextIndex = Random.Range(0, busStops.Length);
            }
        }

        return nextIndex;
    }

    private void BuildRouteToCurrentBusStop()
    {
        currentRoute.Clear();
        currentRouteIndex = 0;

        if (pathFinder == null || currentBusStopTarget == null || startPoint == null)
            return;

        pathFinder.SetRouteEndpointsAndRebuild(startPoint, currentBusStopTarget);

        if (pathFinder.HasRoute)
        {
            currentRoute = new List<PathNode>(pathFinder.CurrentRoute);
        }
    }

    private bool HasValidCurrentNode()
    {
        return currentRoute != null &&
               currentRoute.Count > 0 &&
               currentRouteIndex >= 0 &&
               currentRouteIndex < currentRoute.Count &&
               currentRoute[currentRouteIndex] != null;
    }

    private Vector3 GetCurrentNodePosition()
    {
        return currentRoute[currentRouteIndex].transform.position;
    }

    private Transform GetCurrentNodeTransform()
    {
        return currentRoute[currentRouteIndex].transform;
    }

    private void SafeEndEpisode()
    {
        if (episodeEnding)
            return;

        episodeEnding = true;
        EndEpisode();
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
}