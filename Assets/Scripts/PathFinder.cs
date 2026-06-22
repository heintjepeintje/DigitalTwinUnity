using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using Utils;

public class PathFinder : MonoBehaviour
{
	private List<PathNode> _pathNodes;

	[SerializeField]
	private GameObject startObject;

	[SerializeField]
	private GameObject endObject;

	private void Start()
	{
		_pathNodes = new (GetComponentsInChildren<PathNode>());
		List<PathNode> nodes = FindRoute(startObject.transform.position, endObject.transform.position);

		foreach (PathNode node in nodes)
		{
			node.IsSelected = true;
			Debug.Log($"Node: {node.name}");
		}
	}

	private List<PathNode> FindRoute(Vector3 start, Vector3 target)
	{
		PathNode closestStartNode = null;
		float minStartDistance = float.PositiveInfinity;
		foreach (var node in _pathNodes)
		{
			float distance = Vector3.Distance(start, node.transform.position);
			if (distance < minStartDistance)
			{
				minStartDistance = distance;
				closestStartNode = node;
			}
			node.IsSelected = false;
		}

		PathNode closestEndNode = null;
		float minEndDistance = float.PositiveInfinity;
		foreach (var node in _pathNodes)
		{
			float distance = Vector3.Distance(target, node.transform.position);
			if (distance < minEndDistance)
			{
				minEndDistance = distance;
				closestEndNode = node;
			}
		}

		closestStartNode.IsSelected = true;
		closestEndNode.IsSelected = true;

		List<PathNode> openSet = new();
		HashSet<PathNode> closedSet = new HashSet<PathNode>();
		openSet.Add(closestStartNode);

		Stack<PathNode> stacks = new();

		while (openSet.Count > 0)
		{
			PathNode currentNode = openSet[0];
			foreach (var node in openSet)
			{
				if (node.FCost < currentNode.FCost || node.FCost == currentNode.FCost && node.HCost < currentNode.HCost)
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

			foreach (GameObject neighbor in currentNode.outputNodes)
			{
				PathNode neighborNode = neighbor.GetComponent<PathNode>();
				if (closedSet.Contains(neighborNode))
				{
					continue;
				}

				float cost = currentNode.GCost + Vector3.Distance(currentNode.transform.position, neighbor.transform.position);
				if (cost < neighborNode.GCost || !openSet.Contains(neighborNode))
				{
					neighborNode.GCost = cost;
					neighborNode.HCost = Vector3.Distance(neighbor.transform.position, closestEndNode.transform.position);
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

	List<PathNode> RetracePath(PathNode start, PathNode end)
	{
		List<PathNode> path = new();
		PathNode currentNode = end;

		while (currentNode != start)
		{
			path.Add(currentNode);
			currentNode = currentNode.ParentNode;
		}

		path.Reverse();

		return path;
	}
}
