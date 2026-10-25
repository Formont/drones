using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Campaign mission 3: use the BumbleBee's water cannon to extinguish three roof fires.</summary>
public sealed class Lvl3FireMissionController : MonoBehaviour
{
    private enum MissionState { Playing, Paused, Failed, Completed }

    [Header("Миссия")]
    [SerializeField] private Transform drone;
    [SerializeField] private FireController[] fires;
    [SerializeField, Min(0.1f)] private float sprayRange = 9f;
    [SerializeField, Min(1f)] private float timeLimitNormal = 30f;
    [SerializeField, Min(1f)] private float timeLimitHard = 40f;
    [SerializeField, Min(0.1f)] private float collisionRadius = 0.55f;

    [Header("Интерфейс миссии")]
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private TMP_Text instructionsLabel;
    [SerializeField] private LineRenderer waterStream;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject failedPanel;
    [SerializeField] private GameObject completedPanel;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button pauseMenuButton;
    [SerializeField] private Button failedMenuButton;
    [SerializeField] private Button failedRestartButton;
    [SerializeField] private Button completedMapButton;

    private MissionState state = MissionState.Playing;
    private DroneKeyboardController droneController;
    private GameDifficulty difficulty;
    private float timeRemaining;
    private int extinguishedCount;
    private readonly HashSet<FireController> countedFires = new HashSet<FireController>();

    private void Start()
    {
        Time.timeScale = 1f;
        difficulty = GameSettingsService.Difficulty;
        timeRemaining = difficulty == GameDifficulty.Hard ? timeLimitHard : timeLimitNormal;
        droneController = drone != null ? drone.GetComponent<DroneKeyboardController>() : null;
        if (droneController != null) droneController.SetFlightInputEnabled(true);

        SetActive(pausePanel, false);
        SetActive(failedPanel, false);
        SetActive(completedPanel, false);
        if (failedRestartButton == null) failedRestartButton = FindButton("FailedRestartButton");
        if (pauseButton != null) pauseButton.onClick.AddListener(Pause);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(RestartMission);
        if (pauseMenuButton != null) pauseMenuButton.onClick.AddListener(ReturnToMenu);
        if (failedMenuButton != null) failedMenuButton.onClick.AddListener(ReturnToMenu);
        if (failedRestartButton != null) failedRestartButton.onClick.AddListener(RestartMission);
        if (completedMapButton != null) completedMapButton.onClick.AddListener(ReturnToMap);
        if (waterStream != null) waterStream.enabled = false;
        if (timerLabel != null) timerLabel.gameObject.SetActive(difficulty != GameDifficulty.Easy);

        if (instructionsLabel != null)
            instructionsLabel.text = "Удерживайте K рядом с очагом, чтобы направить на него воду. Потушите все очаги.";
        UpdateProgress();
    }

    private void Update()
    {
        if (state == MissionState.Playing && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Pause();
        if (state != MissionState.Playing) return;

        if (difficulty != GameDifficulty.Easy)
        {
            timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
            if (timerLabel != null) timerLabel.text = $"ОСТАЛОСЬ  {Mathf.CeilToInt(timeRemaining)} СЕК";
            if (timeRemaining <= 0f)
            {
                FailMission("Время вышло: не все очаги потушены.");
                return;
            }
        }

        if (drone != null && HasHitObstacle())
        {
            FailMission("Дрон столкнулся с объектом.");
            return;
        }

        bool spraying = Keyboard.current != null && Keyboard.current.kKey.isPressed;
        FireController activeFire = spraying ? FindFireInRange() : null;
        if (waterStream != null)
        {
            waterStream.enabled = activeFire != null;
            if (activeFire != null)
            {
                waterStream.SetPosition(0, drone.position);
                waterStream.SetPosition(1, activeFire.transform.position);
            }
        }

        if (activeFire != null)
        {
            activeFire.LessIntensity();
            if (activeFire.isLiquidated && countedFires.Add(activeFire))
            {
                extinguishedCount++;
                UpdateProgress();
                if (extinguishedCount >= fires.Length)
                {
                    CompleteMission();
                    return;
                }
                if (instructionsLabel != null)
                    instructionsLabel.text = "Очаг устранён. Перелетите к следующему дому и продолжайте удерживать K.";
            }
        }
        else if (spraying && instructionsLabel != null)
        {
            instructionsLabel.text = "Подлетите ближе к горящему дому, затем удерживайте K.";
        }
    }

    private FireController FindFireInRange()
    {
        if (drone == null || fires == null) return null;
        FireController closest = null;
        float closestDistance = sprayRange;
        foreach (FireController fire in fires)
        {
            if (fire == null || fire.isLiquidated) continue;
            float distance = Vector3.Distance(drone.position, fire.transform.position);
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closest = fire;
            }
        }
        return closest;
    }

    private bool HasHitObstacle()
    {
        Collider[] overlaps = Physics.OverlapSphere(drone.position, collisionRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            if (overlap == null || overlap.transform == drone || overlap.transform.IsChildOf(drone)) continue;
            if (overlap.GetComponentInParent<MissionObstacle>() != null) return true;
        }
        return false;
    }

    public void FailOutOfBounds(string reason) => FailMission(reason);

    private void UpdateProgress()
    {
        if (progressLabel != null)
            progressLabel.text = $"ТУШЕНИЕ   {extinguishedCount} / {fires.Length}";
    }

    private void Pause()
    {
        if (state != MissionState.Playing) return;
        state = MissionState.Paused;
        if (droneController != null) droneController.SetFlightInputEnabled(false);
        if (waterStream != null) waterStream.enabled = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetActive(pausePanel, true);
        SetActive(pauseButton != null ? pauseButton.gameObject : null, false);
    }

    private void Resume()
    {
        if (state != MissionState.Paused) return;
        state = MissionState.Playing;
        if (droneController != null) droneController.SetFlightInputEnabled(true);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetActive(pausePanel, false);
        SetActive(pauseButton != null ? pauseButton.gameObject : null, true);
    }

    private void FailMission(string reason)
    {
        if (state != MissionState.Playing) return;
        state = MissionState.Failed;
        if (droneController != null) droneController.SetFlightInputEnabled(false);
        if (waterStream != null) waterStream.enabled = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetActive(pauseButton != null ? pauseButton.gameObject : null, false);
        if (failedPanel != null)
        {
            foreach (TMP_Text text in failedPanel.GetComponentsInChildren<TMP_Text>(true))
                if (text.gameObject.name == "Reason") { text.text = reason; break; }
            failedPanel.SetActive(true);
        }
    }

    private void CompleteMission()
    {
        state = MissionState.Completed;
        if (droneController != null) droneController.SetFlightInputEnabled(false);
        if (waterStream != null) waterStream.enabled = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        const string bestKey = "Mission3BestDifficulty";
        int previousBest = PlayerPrefs.GetInt(bestKey, -1);
        if ((int)difficulty > previousBest) PlayerPrefs.SetInt(bestKey, (int)difficulty);
        PlayerPrefs.SetInt("Mission3Completed", 1);
        PlayerPrefs.Save();
        SetActive(pauseButton != null ? pauseButton.gameObject : null, false);
        SetActive(completedPanel, true);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }

    private static Button FindButton(string objectName)
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (button.gameObject.name == objectName) return button;
        return null;
    }

    private void RestartMission()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void ReturnToMap()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("CampaignMap");
    }

    private void OnDestroy() => Time.timeScale = 1f;
}
