using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class BusAgentRijden : Agent
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float turnSpeed = 80f;

    [Header("Speed Slider")]
    public float minSliderSpeed = 1f;
    public float maxSliderSpeed = 15f;
    public float minDisplaySpeedKmh = 1f;
    public float maxDisplaySpeedKmh = 45f;

    [SerializeField] private float currentSliderSpeed = 8f;

    [Header("Scene References")]
    public Transform startPoint;
    public PathFinder pathFinder;

    [Header("Route Settings")]
    public float maxNodeDistance = 30f;
    public float nodeReachDistance = 3.0f;
    public int routeLookAhead = 2;
    public bool requireTriggerToAdvanceNode = false;

    [Header("Mode")]
    public bool continuousRuntimeMode = true;

    [Header("Rewards")]
    public float nodeReward = 0.75f;
    public float routeCompleteReward = 2.5f;
    public float borderPenalty = -1.0f;
    public float stepPenalty = -0.0005f;
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

    [Header("Bus Stops")]
    public Transform[] busStops;
    public bool avoidChoosingSameStopTwice = true;

    private Transform currentBusStopTarget;
    private Transform currentRouteStart;
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
    private int lapCount = 0;

    private bool isRunning = true;
    private bool isPaused = false;
    private string currentStatus = "Idle";
    private float currentConfidence = -1f;

    public float CurrentSpeedKmh
    {
        get
        {
            float normalized = Mathf.InverseLerp(minSliderSpeed, maxSliderSpeed, currentSliderSpeed);
            return Mathf.Lerp(minDisplaySpeedKmh, maxDisplaySpeedKmh, normalized);
        }
    }
    public float CurrentSliderSpeed => currentSliderSpeed;
    public float CurrentConfidence => currentConfidence;
    public string CurrentStatus => currentStatus;
    public int LapCount => lapCount;
    public int CurrentWaypointIndex => Mathf.Clamp(currentRouteIndex, 0, Mathf.Max(TotalWaypoints - 1, 0));
    public int TotalWaypoints => currentRoute != null ? currentRoute.Count : 0;
    public bool IsRunning => isRunning;
    public bool IsPaused => isPaused;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        SetSpeedFromSlider(currentSliderSpeed);
        currentStatus = "Initialized";
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
        currentConfidence = -1f;
        currentRouteIndex = 0;

        currentRouteStart = startPoint;

        ChooseNextRandomBusStop();
        BuildRouteToCurrentBusStop();

        if (HasValidCurrentNode())
        {
            previousDistanceToNode = Vector3.Distance(rb.position, GetCurrentNodePosition());
            bestDistanceToCurrentNode = previousDistanceToNode;
            nodeStallTimer = 0f;
            currentStatus = "Driving To " + currentBusStopTarget.name;
        }
        else
        {
            previousDistanceToNode = 0f;
            bestDistanceToCurrentNode = 0f;
            nodeStallTimer = 0f;
            currentStatus = "No Route";
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

            currentConfidence = -1f;
            return;
        }

        Vector3 firstNodeDir = (GetCurrentNodePosition() - transform.position).normalized;
        currentConfidence = Mathf.Clamp01((Vector3.Dot(transform.forward, firstNodeDir) + 1f) * 0.5f);

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

        if (!isRunning)
        {
            moveInput = 0f;
            turnInput = 0f;
            currentStatus = "Stopped";
            return;
        }

        if (isPaused)
        {
            moveInput = 0f;
            turnInput = 0f;
            currentStatus = "Paused";
            return;
        }

        episodeTimer += Time.fixedDeltaTime;
        currentStatus = "Running";

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
            currentStatus = "Node Stall";

            if (continuousRuntimeMode)
            {
                ResetBusToRouteStart();
                return;
            }

            if (continuousRuntimeMode)
            {
                ResetBusToCurrentRouteStart();
                return;
            }
            SafeEndEpisode();
        }
    }

    private void AdvanceToNextRouteNode()
    {
        if (!HasValidCurrentNode())
            return;

        AddReward(nodeReward);
        Debug.Log("Reached route node: " + currentRoute[currentRouteIndex].name);

        currentRouteIndex++;

        if (currentRouteIndex >= currentRoute.Count)
        {
            lapCount++;
            AddReward(routeCompleteReward);

            if (continuousRuntimeMode)
            {
                currentRouteStart = currentBusStopTarget;

                ChooseNextRandomBusStop();
                BuildRouteToCurrentBusStop();

                if (HasValidCurrentNode())
                {
                    previousDistanceToNode = Vector3.Distance(transform.position, GetCurrentNodePosition());
                    bestDistanceToCurrentNode = previousDistanceToNode;
                    nodeStallTimer = 0f;
                    currentStatus = "Driving To " + currentBusStopTarget.name;
                }
                else
                {
                    currentStatus = "No Route Found";
                }
            }
            else
            {
                currentStatus = "Route Complete";
                SafeEndEpisode();
            }

            return;
        }

        previousDistanceToNode = Vector3.Distance(transform.position, GetCurrentNodePosition());
        bestDistanceToCurrentNode = previousDistanceToNode;
        nodeStallTimer = 0f;
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
            currentStatus = "Stuck";

            if (continuousRuntimeMode)
            {
                ResetBusToCurrentRouteStart();
                return;
            }

            SafeEndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        discreteActions[0] = 0;
        discreteActions[1] = 0;

        if (!isRunning || isPaused)
            return;

        if (Input.GetKey(KeyCode.A)) discreteActions[0] = 1;
        else if (Input.GetKey(KeyCode.D)) discreteActions[0] = 2;

        if (Input.GetKey(KeyCode.W)) discreteActions[1] = 1;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (episodeEnding)
            return;

        if (collision.gameObject.CompareTag("Border"))
        {
            AddReward(borderPenalty);
            currentStatus = "Collision";

            if (continuousRuntimeMode)
            {
                ResetBusToCurrentRouteStart();
                return;
            }
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
            Debug.Log("Wrong node trigger: " + other.name + " | expected: " + expectedNode.name);
        }
    }

    private void ChooseNextRandomBusStop()
    {
        if (busStops == null || busStops.Length == 0)
        {
            currentBusStopTarget = null;
            currentBusStopIndex = -1;
            currentStatus = "No Bus Stops";
            return;
        }

        int nextIndex = Random.Range(0, busStops.Length);

        if (avoidChoosingSameStopTwice && busStops.Length > 1)
        {
            while (nextIndex == currentBusStopIndex)
            {
                nextIndex = Random.Range(0, busStops.Length);
            }
        }

        currentBusStopIndex = nextIndex;
        currentBusStopTarget = busStops[currentBusStopIndex];
    }

    private void BuildRouteToCurrentBusStop()
    {
        currentRoute.Clear();
        currentRouteIndex = 0;

        if (pathFinder == null || currentBusStopTarget == null || currentRouteStart == null)
        {
            currentStatus = "Missing Route Target";
            return;
        }

        pathFinder.SetRouteEndpointsAndRebuild(currentRouteStart, currentBusStopTarget);

        if (pathFinder.HasRoute)
        {
            currentRoute = new List<PathNode>(pathFinder.CurrentRoute);
            currentStatus = "Driving To Stop " + currentBusStopTarget.name;
        }
        else
        {
            currentStatus = "No Route Found";
        }
    }

    private void ResetBusToRouteStart()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (currentRouteStart != null)
        {
            rb.position = currentRouteStart.position;
            rb.rotation = currentRouteStart.rotation;
            Physics.SyncTransforms();
        }

        moveInput = 0f;
        turnInput = 0f;
        stuckTimer = 0f;
        episodeTimer = 0f;
        previousPosition = rb.position;
        currentConfidence = -1f;

        BuildRouteToCurrentBusStop();

        if (HasValidCurrentNode())
        {
            previousDistanceToNode = Vector3.Distance(rb.position, GetCurrentNodePosition());
            bestDistanceToCurrentNode = previousDistanceToNode;
            nodeStallTimer = 0f;
            currentStatus = "Driving To " + currentBusStopTarget.name;
        }
        else
        {
            previousDistanceToNode = 0f;
            bestDistanceToCurrentNode = 0f;
            nodeStallTimer = 0f;
            currentStatus = "No Route";
        }
    }

    private void ResetBusToCurrentRouteStart()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (currentRouteStart != null)
        {
            rb.position = currentRouteStart.position;
            rb.rotation = currentRouteStart.rotation;
            Physics.SyncTransforms();
        }

        moveInput = 0f;
        turnInput = 0f;
        stuckTimer = 0f;
        episodeTimer = 0f;
        previousPosition = rb.position;
        currentConfidence = -1f;

        BuildRouteToCurrentBusStop();

        if (HasValidCurrentNode())
        {
            previousDistanceToNode = Vector3.Distance(rb.position, GetCurrentNodePosition());
            bestDistanceToCurrentNode = previousDistanceToNode;
            nodeStallTimer = 0f;
            currentStatus = "Driving To " + currentBusStopTarget.name;
        }
        else
        {
            previousDistanceToNode = 0f;
            bestDistanceToCurrentNode = 0f;
            nodeStallTimer = 0f;
            currentStatus = "No Route";
        }
    }

    public void StartBus()
    {
        isRunning = true;
        isPaused = false;
        currentStatus = "Running";
    }

    public void StopBus()
    {
        isRunning = false;
        isPaused = false;
        moveInput = 0f;
        turnInput = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentStatus = "Stopped";
    }

    public void PauseBus(bool pause)
    {
        if (!isRunning)
            return;

        isPaused = pause;
        moveInput = 0f;
        turnInput = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentStatus = isPaused ? "Paused" : "Running";
    }

    public void SetSpeedFromSlider(float sliderValue)
    {
        currentSliderSpeed = Mathf.Clamp(sliderValue, minSliderSpeed, maxSliderSpeed);
        moveSpeed = currentSliderSpeed;
    }

    public float GetDisplaySpeedFromSlider(float sliderValue)
    {
        float normalized = Mathf.InverseLerp(minSliderSpeed, maxSliderSpeed, sliderValue);
        return Mathf.Lerp(minDisplaySpeedKmh, maxDisplaySpeedKmh, normalized);
    }

    private void SafeEndEpisode()
    {
        if (episodeEnding)
            return;

        episodeEnding = true;
        EndEpisode();
    }
}