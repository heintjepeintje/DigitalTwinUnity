using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class BusController : MonoBehaviour
{
	public enum Axle
	{
		All,
		Front,
		Rear
	}

	[Serializable]
	public struct Wheel
	{
		public Axle Axle;
		public GameObject Model;
	}

	[SerializeField]
	public Axle DrivingAxle;

	[SerializeField]
	public List<Wheel> wheels;

	[SerializeField]
	public float torqueMultiplier;

	[SerializeField]
	public float maxSteerAngle;

	private Rigidbody rb;

	[SerializeField]
	private float maxBrakeTorque;

	private InputAction gasAction;
	private InputAction brakeAction;
	private InputAction turnAction;

	[SerializeField]
	private Vector3 _centerOfMass;

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.green;
		Gizmos.DrawSphere(_centerOfMass + transform.position, 0.25f);
	}

	void Start()
    {
        rb = GetComponent<Rigidbody>();

		gasAction = InputSystem.actions.FindAction("Gas");
		brakeAction = InputSystem.actions.FindAction("Brake");
		turnAction = InputSystem.actions.FindAction("Turn");

		rb.centerOfMass = _centerOfMass;
	}

	void FixedUpdate()
    {
		float turnDirection = turnAction.ReadValue<Vector2>().x;

		//rb.AddForce(Vector3.down * (44.496f * rb.linearVelocity).magnitude);

		foreach (Wheel wheel in wheels)
		{
			if (wheel.Axle == DrivingAxle || DrivingAxle == Axle.All)
			{
				float torque = gasAction.ReadValue<float>() * torqueMultiplier;
				wheel.Model.GetComponent<WheelCollider>().motorTorque = torque;
			}

			if (wheel.Axle == Axle.Front)
			{
				float angle = turnDirection * maxSteerAngle;
				wheel.Model.GetComponent<WheelCollider>().steerAngle = angle;
			}

			wheel.Model.GetComponent<WheelCollider>().brakeTorque = brakeAction.ReadValue<float>() * maxBrakeTorque;
		}
    }
}