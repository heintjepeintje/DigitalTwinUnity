using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [Header("Buttons")]
    public Button dayNightButton;
    public Button startStopButton;
    public Button pauseButton;

    [Header("Texts")]
    public TMP_Text dayNightButtonText;
    public TMP_Text startStopButtonText;
    public TMP_Text pauseButtonText;
    public TMP_Text speedText;
    public TMP_Text confidenceText;
    public TMP_Text statusText;
    public TMP_Text lapText;
    public TMP_Text waypointText;

    [Header("References")]
    public BusAgentRijden busAgent;
    public DayNightManager dayNightManager;

    private void Start()
    {
        if (dayNightButton != null) dayNightButton.onClick.AddListener(ToggleDayNight);
        if (startStopButton != null) startStopButton.onClick.AddListener(ToggleStartStop);
        if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);

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

            if (lapText != null)
                lapText.text = $"Laps: {busAgent.LapCount}";

            if (waypointText != null)
                waypointText.text = $"Waypoint: {busAgent.CurrentWaypointIndex + 1}/{busAgent.TotalWaypoints}";
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

            if (pauseButtonText != null)
                pauseButtonText.text = busAgent.IsPaused ? "Resume" : "Pauze";
        }
    }
}