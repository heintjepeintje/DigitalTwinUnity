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

    [Header("References")]
    public Rigidbody carRigidbody;
    public DayNightManager dayNightManager;
    public AIVisibilityMonitor aiMonitor;

    private bool isRunning = false;
    private bool isPaused = false;

    void Start()
    {
        dayNightButton.onClick.AddListener(ToggleDayNight);
        startStopButton.onClick.AddListener(ToggleStartStop);
        pauseButton.onClick.AddListener(TogglePause);

        UpdateUI();
    }

    void Update()
    {
        if (carRigidbody != null)
        {
            float speedKmh = carRigidbody.linearVelocity.magnitude * 3.6f;
            speedText.text = $"Snelheid: {speedKmh:0.0} km/h";
        }

        if (aiMonitor != null)
        {
            confidenceText.text = $"Confidence: {aiMonitor.stoplightConfidence:0.00}";
        }
    }

    void ToggleDayNight()
    {
        if (dayNightManager != null)
        {
            dayNightManager.ToggleDayNight();
            dayNightButtonText.text = dayNightManager.IsNight ? "Zet dag" : "Zet nacht";
        }
    }

    void ToggleStartStop()
    {
        isRunning = !isRunning;

        if (!isRunning)
        {
            isPaused = false;
            Time.timeScale = 1f;
        }

        UpdateUI();
    }

    void TogglePause()
    {
        if (!isRunning) return;

        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;

        UpdateUI();
    }

    void UpdateUI()
    {
        startStopButtonText.text = isRunning ? "Stop" : "Start";
        pauseButtonText.text = isPaused ? "Resume" : "Pauze";

        if (!isRunning)
            statusText.text = "Status: Gestopt";
        else if (isPaused)
            statusText.text = "Status: Gepauzeerd";
        else
            statusText.text = "Status: Actief";
    }
}