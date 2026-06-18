using System.Collections.Generic;
using UnityEngine;

public class TrafficLightTriggerZone : MonoBehaviour
{
    [SerializeField] private TrafficLightController trafficLight;

    private readonly HashSet<GameObject> carsInZone = new();

    private void OnEnable()
    {
        if (trafficLight != null)
            trafficLight.OnStateChanged += OnLightStateChanged;
    }

    private void OnDisable()
    {
        if (trafficLight != null)
            trafficLight.OnStateChanged -= OnLightStateChanged;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Car")) return;

        carsInZone.Add(other.gameObject);
        Debug.Log($"[TriggerZone] Auto '{other.name}' reed de zone in. Stoplicht is: {trafficLight.CurrentState}");
        LogStateForCar(other.name, trafficLight.CurrentState);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Car")) return;

        carsInZone.Remove(other.gameObject);
        Debug.Log($"[TriggerZone] Auto '{other.name}' verliet de zone.");
    }

    private void OnLightStateChanged(TrafficLightController.LightState newState)
    {
        foreach (var car in carsInZone)
        {
            if (car == null) continue; // auto kan vernietigd zijn
            Debug.Log($"[TriggerZone] Stoplicht veranderd terwijl '{car.name}' in de zone staat.");
            LogStateForCar(car.name, newState);
        }
    }

    private void LogStateForCar(string carName, TrafficLightController.LightState state)
    {
        if (state == TrafficLightController.LightState.Red)
            Debug.Log($"[TriggerZone] → '{carName}': Stoplicht is ROOD.");
        else
            Debug.Log($"[TriggerZone] → '{carName}': Stoplicht is GROEN.");
    }
}
