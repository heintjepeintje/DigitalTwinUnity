using System.Collections;
using UnityEngine;

public class TrafficLightController : MonoBehaviour
{
    [Header("Mesh Renderers")]
    public MeshRenderer carTrafficRenderer;
    public MeshRenderer pedestrianRenderer;

    [Header("Car material slot indexes")]
    public int carYellowIndex = 0;
    public int carRedIndex = 1;
    public int carGreenIndex = 2;

    [Header("Pedestrian material slot indexes")]
    public int pedestrianRedIndex = 0;
    public int pedestrianGreenIndex = 1;

    [Header("Emission colors")]
    public Color redColor = Color.red;
    public Color yellowColor = new Color(1f, 0.5f, 0f);
    public Color greenColor = Color.green;
    public Color offColor = Color.black;

    [Header("Timing")]
    public float redTime = 6f;
    public float redYellowTime = 1.5f;
    public float greenTime = 8f;
    public float yellowTime = 2f;
    public float allRedBufferTime = 1f;

    [Header("Pedestrian blinking")]
    public int pedestrianBlinkCount = 3;
    public float pedestrianBlinkInterval = 0.5f;

    private Material[] carMaterials;
    private Material[] pedestrianMaterials;

    void Start()
    {
        carMaterials = carTrafficRenderer.materials;
        pedestrianMaterials = pedestrianRenderer.materials;

        StartCoroutine(TrafficCycle());
    }

    IEnumerator TrafficCycle()
    {
        while (true)
        {
            SetCarState(red: true, yellow: false, green: false);
            SetPedestrianState(red: false, green: true);
            yield return new WaitForSeconds(redTime);

            yield return StartCoroutine(BlinkPedestrianGreen());

            SetPedestrianState(red: true, green: false);
            yield return new WaitForSeconds(allRedBufferTime);

            SetCarState(red: true, yellow: true, green: false);
            yield return new WaitForSeconds(redYellowTime);

            SetCarState(red: false, yellow: false, green: true);
            yield return new WaitForSeconds(greenTime);

            SetCarState(red: false, yellow: true, green: false);
            SetPedestrianState(red: true, green: false);
            yield return new WaitForSeconds(yellowTime);
        }
    }

    IEnumerator BlinkPedestrianGreen()
    {
        for (int i = 0; i < pedestrianBlinkCount; i++)
        {
            SetEmission(pedestrianMaterials[pedestrianGreenIndex], false, greenColor);
            yield return new WaitForSeconds(pedestrianBlinkInterval);

            SetEmission(pedestrianMaterials[pedestrianGreenIndex], true, greenColor);
            yield return new WaitForSeconds(pedestrianBlinkInterval);
        }
    }

    void SetCarState(bool red, bool yellow, bool green)
    {
        SetEmission(carMaterials[carRedIndex], red, redColor);
        SetEmission(carMaterials[carYellowIndex], yellow, yellowColor);
        SetEmission(carMaterials[carGreenIndex], green, greenColor);
    }

    void SetPedestrianState(bool red, bool green)
    {
        SetEmission(pedestrianMaterials[pedestrianRedIndex], red, redColor);
        SetEmission(pedestrianMaterials[pedestrianGreenIndex], green, greenColor);
    }

    void SetEmission(Material mat, bool isOn, Color emissionColor)
    {
        if (isOn)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor);
        }
        else
        {
            mat.SetColor("_EmissionColor", offColor);
        }
    }
}