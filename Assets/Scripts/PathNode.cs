using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;

public class PathNode : MonoBehaviour
{
	public float GCost = 0.0f;
	public float HCost = 0.0f;
	public float FCost { get { return GCost + HCost; } set { } }
	public bool IsSelected = false;

	public PathNode ParentNode { get; set; }

	[SerializeField]
	public List<GameObject> outputNodes = new List<GameObject>();

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.red;
		if (IsSelected)
		{
			Gizmos.color = Color.green;
		}
		Gizmos.DrawSphere(transform.position, 0.5f);

		Gizmos.color = Color.red;
		foreach (GameObject node in outputNodes) {
			PathNode pathNode = null;
			if (!node.TryGetComponent(out pathNode))
			{
				Debug.Log($"{node.name}");
				continue;
			}

			if (IsSelected && pathNode.IsSelected)
			{
				Gizmos.color = Color.green;
			} else
			{
				Gizmos.color = Color.red;
			}

			Gizmos.DrawLine(transform.position, node.transform.position);
		}
	}

}
