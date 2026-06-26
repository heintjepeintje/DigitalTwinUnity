using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [Header("Buttons")]
    public Button dayNightButton;
    public Button startStopButton;
    public Button pauseButton;

    [Header("Slider")]
    public Slider speedSlider;

    [Header("Texts")]
    public TMP_Text dayNightButtonText;
    public TMP_Text startStopButtonText;
    public TMP_Text speedText;
    public TMP_Text confidenceText;
    public TMP_Text statusText;

    [Header("References")]
    public BusAgentRijden busAgent;
    public DayNightManager dayNightManager;

    private void Start()
    {
        if (dayNightButton != null) dayNightButton.onClick.AddListener(ToggleDayNight);
        if (startStopButton != null) startStopButton.onClick.AddListener(ToggleStartStop);
        if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);
        if (speedSlider != null)
        {
            speedSlider.minValue = 1f;
            speedSlider.maxValue = 15f;
            speedSlider.wholeNumbers = true;

            if (busAgent != null)
                speedSlider.value = busAgent.CurrentSliderSpeed;

            speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
        }

        UpdateUI();
    }

    private void Update()
    {
        if (busAgent != null)
        {
            if (speedText != null)
                speedText.text = $"Speed: {busAgent.CurrentSpeedKmh:0.0} km/h";

            if (confidenceText != null)
                confidenceText.text = busAgent.CurrentConfidence >= 0f
                    ? $"Confidence: {busAgent.CurrentConfidence:0.00}"
                    : "Confidence: N/A";

            if (statusText != null)
                statusText.text = $"Status: {busAgent.CurrentStatus}";
        }

        UpdateUI();
    }

    private void ToggleDayNight()
    {
        if (dayNightManager != null)
        {
            dayNightManager.ToggleDayNight();

            if (dayNightButtonText != null)
                dayNightButtonText.text = dayNightManager.IsNight ? "Zet dag" : "Zet nacht";
        }
    }

    private void ToggleStartStop()
    {
        if (busAgent == null)
            return;

        if (busAgent.IsRunning)
            busAgent.StopBus();
        else
            busAgent.StartBus();

        UpdateUI();
    }

    private void TogglePause()
    {
        if (busAgent == null || !busAgent.IsRunning)
            return;

        busAgent.PauseBus(!busAgent.IsPaused);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (busAgent != null)
        {
            if (startStopButtonText != null)
                startStopButtonText.text = busAgent.IsRunning ? "Stop" : "Start";
        }
    }

    private void UpdateRuntimeInfo()
    {
        if (busAgent == null)
            return;

        if (speedText != null)
            speedText.text = $"Speed: {busAgent.CurrentSpeedKmh:0} km/h";

        if (confidenceText != null)
            confidenceText.text = busAgent.CurrentConfidence >= 0f
                ? $"Confidence: {busAgent.CurrentConfidence:0.00}"
                : "Confidence: N/A";

        if (statusText != null)
            statusText.text = $"Status: {busAgent.CurrentStatus}";
    }

    private void OnSpeedSliderChanged(float value)
    {
        if (busAgent == null)
            return;

        busAgent.SetSpeedFromSlider(value);
        UpdateRuntimeInfo();
    }
}