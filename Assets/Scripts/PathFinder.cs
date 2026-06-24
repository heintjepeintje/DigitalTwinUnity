using System.Collections.Generic;
using UnityEngine;

public class PathFinder : MonoBehaviour
{
    [SerializeField] private GameObject startObject;
    [SerializeField] private GameObject endObject;

    private List<PathNode> pathNodes = new List<PathNode>();

    public List<PathNode> CurrentRoute { get; private set; } = new List<PathNode>();
    public bool HasRoute => CurrentRoute != null && CurrentRoute.Count > 0;

    private void Start()
    {
        BuildRoute();
    }

    public void RebuildRoute()
    {
        BuildRoute();
    }

    public void BuildRoute()
    {
        pathNodes = new List<PathNode>(GetComponentsInChildren<PathNode>());

        foreach (PathNode node in pathNodes)
        {
            node.IsSelected = false;
            node.GCost = float.PositiveInfinity;
            node.HCost = 0f;
            node.ParentNode = null;
        }

        if (startObject == null || endObject == null)
        {
            CurrentRoute = new List<PathNode>();
            Debug.LogWarning("PathFinder: startObject of endObject ontbreekt.");
            return;
        }

        CurrentRoute = FindRoute(startObject.transform.position, endObject.transform.position);

        foreach (PathNode node in CurrentRoute)
        {
            if (node != null)
            {
                node.IsSelected = true;
                Debug.Log("Route node: " + node.name);
            }
        }

        Debug.Log("PathFinder route count: " + CurrentRoute.Count);
    }

    private List<PathNode> FindRoute(Vector3 start, Vector3 target)
    {
        PathNode closestStartNode = GetClosestNode(start);
        PathNode closestEndNode = GetClosestNode(target);

        if (closestStartNode == null || closestEndNode == null)
            return new List<PathNode>();

        List<PathNode> openSet = new List<PathNode>();
        HashSet<PathNode> closedSet = new HashSet<PathNode>();

        closestStartNode.GCost = 0f;
        closestStartNode.HCost = Vector3.Distance(
            closestStartNode.transform.position,
            closestEndNode.transform.position
        );

        openSet.Add(closestStartNode);

        while (openSet.Count > 0)
        {
            PathNode currentNode = openSet[0];

            for (int i = 1; i < openSet.Count; i++)
            {
                PathNode node = openSet[i];
                if (node.FCost < currentNode.FCost ||
                    (Mathf.Approximately(node.FCost, currentNode.FCost) && node.HCost < currentNode.HCost))
                {
                    currentNode = node;
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == closestEndNode)
            {
                return RetracePath(closestStartNode, closestEndNode);
            }

            foreach (GameObject neighborObject in currentNode.outputNodes)
            {
                if (neighborObject == null)
                    continue;

                PathNode neighborNode = neighborObject.GetComponent<PathNode>();
                if (neighborNode == null || closedSet.Contains(neighborNode))
                    continue;

                float tentativeCost = currentNode.GCost +
                                      Vector3.Distance(currentNode.transform.position, neighborNode.transform.position);

                if (tentativeCost < neighborNode.GCost || !openSet.Contains(neighborNode))
                {
                    neighborNode.GCost = tentativeCost;
                    neighborNode.HCost = Vector3.Distance(
                        neighborNode.transform.position,
                        closestEndNode.transform.position
                    );
                    neighborNode.ParentNode = currentNode;

                    if (!openSet.Contains(neighborNode))
                    {
                        openSet.Add(neighborNode);
                    }
                }
            }
        }

        return new List<PathNode>();
    }

    private PathNode GetClosestNode(Vector3 position)
    {
        PathNode closestNode = null;
        float minDistance = float.PositiveInfinity;

        foreach (PathNode node in pathNodes)
        {
            float distance = Vector3.Distance(position, node.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestNode = node;
            }
        }

        return closestNode;
    }

    private List<PathNode> RetracePath(PathNode start, PathNode end)
    {
        List<PathNode> path = new List<PathNode>();
        PathNode currentNode = end;

        while (currentNode != null && currentNode != start)
        {
            path.Add(currentNode);
            currentNode = currentNode.ParentNode;
        }

        if (start != null)
        {
            path.Add(start);
        }

        path.Reverse();
        return path;
    }

    public void SetRouteEndpointsAndRebuild(Transform start, Transform end)
    {
        if (start != null)
            startObject = start.gameObject;

        if (end != null)
            endObject = end.gameObject;

        RebuildRoute();
    }
}