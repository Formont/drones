using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>Objective flow, collision failure, checkpoint progression, and mission controls for lvl 1.</summary>
public sealed class Lvl1MissionController : MonoBehaviour
{
    private enum MissionState { Playing, Paused, Failed, Completed }

    [Header("Миссия")]
    [SerializeField] private Transform drone;
    [SerializeField] private Transform[] checkpoints;
    [SerializeField] private GameObject[] checkpointVisuals;
    [SerializeField, Min(0.1f)] private float checkpointRadius = 3.25f;
    [SerializeField, Min(0.1f)] private float collisionRadius = 0.55f;

    [Header("Интерфейс")]
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text arrowLabel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject failedPanel;
    [SerializeField] private GameObject completedPanel;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button pauseMenuButton;
    [SerializeField] private Button failedMenuButton;
    [SerializeField] private Button completedMapButton;
    [SerializeField] private AudioClip startMissionClip;
    [SerializeField] private AudioClip checkpointClip;

    private int checkpointIndex;
    private MissionState state = MissionState.Playing;
    private RectTransform arrowRect;
    private DroneKeyboardController droneController;
    private AudioSource effectsSource;

    private void Start()
    {
        Time.timeScale = 1f;
        PlayMissionSound(startMissionClip);
        droneController = drone != null ? drone.GetComponent<DroneKeyboardController>() : null;
        if (droneController != null) droneController.SetFlightInputEnabled(true);
        EnsureUiInput();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (arrowLabel != null)
            arrowRect = arrowLabel.rectTransform;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (failedPanel != null) failedPanel.SetActive(false);
        if (completedPanel != null) completedPanel.SetActive(false);
        if (pauseButton != null) pauseButton.onClick.AddListener(Pause);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(RestartMission);
        if (pauseMenuButton != null) pauseMenuButton.onClick.AddListener(ReturnToMenu);
        if (failedMenuButton != null) failedMenuButton.onClick.AddListener(ReturnToMenu);
        if (completedMapButton != null) completedMapButton.onClick.AddListener(ReturnToMap);

        SetCheckpointVisibility();
        UpdateProgressText();
    }

    private static void EnsureUiInput()
    {
        EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = go.GetComponent<EventSystem>();
        }
        eventSystem.sendNavigationEvents = true;
        InputSystemUIInputModule input = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (input == null) input = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        if (input.actionsAsset == null || input.point == null || input.leftClick == null)
            input.AssignDefaultActions();
    }

    private void Update()
    {
        if (state == MissionState.Playing && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
            Pause();

        if (state != MissionState.Playing || drone == null)
            return;

        if (HasHitObstacle())
        {
            FailMission();
            return;
        }

        if (checkpointIndex < checkpoints.Length && checkpoints[checkpointIndex] != null)
        {
            Transform target = checkpoints[checkpointIndex];
            UpdateArrow(target);
            if (Vector3.Distance(drone.position, target.position) <= checkpointRadius)
                AdvanceCheckpoint();
        }
    }

    private bool HasHitObstacle()
    {
        Collider[] overlaps = Physics.OverlapSphere(drone.position, collisionRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            if (overlap == null || overlap.transform == drone || overlap.transform.IsChildOf(drone))
                continue;

            if (overlap.GetComponentInParent<MissionObstacle>() != null)
                return true;
        }

        return false;
    }

    private void AdvanceCheckpoint()
    {
        PlayMissionSound(checkpointClip);
        if (checkpointIndex < checkpointVisuals.Length && checkpointVisuals[checkpointIndex] != null)
            checkpointVisuals[checkpointIndex].SetActive(false);

        checkpointIndex++;
        if (checkpointIndex >= checkpoints.Length)
        {
            CompleteMission();
            return;
        }

        SetCheckpointVisibility();
        UpdateProgressText();
    }

    private void PlayMissionSound(AudioClip clip)
    {
        if (clip == null || !GameSettingsService.SoundEnabled) return;
        if (effectsSource == null)
        {
            effectsSource = GetComponent<AudioSource>();
            if (effectsSource == null) effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.spatialBlend = 0f;
            if (GetComponent<ProjectAudioChannel>() == null) gameObject.AddComponent<ProjectAudioChannel>();
            GameSettingsService.ApplyToAudioSource(effectsSource);
        }
        effectsSource.PlayOneShot(clip);
    }

    private void SetCheckpointVisibility()
    {
        if (checkpointVisuals == null)
            return;

        for (int i = 0; i < checkpointVisuals.Length; i++)
            if (checkpointVisuals[i] != null)
                checkpointVisuals[i].SetActive(i == checkpointIndex);
    }

    private void UpdateProgressText()
    {
        if (progressLabel != null)
            progressLabel.text = $"УЧЕБНЫЙ МАРШРУТ   {Mathf.Min(checkpointIndex + 1, checkpoints.Length)} / {checkpoints.Length}";
    }

    private void UpdateArrow(Transform target)
    {
        Camera camera = Camera.main;
        if (camera == null || arrowRect == null || target == null)
            return;

        Vector3 screen = camera.WorldToScreenPoint(target.position);
        if (screen.z < 0f)
        {
            screen.x = Screen.width - screen.x;
            screen.y = Screen.height - screen.y;
        }

        Vector2 direction = new Vector2(screen.x - Screen.width * 0.5f, screen.y - Screen.height * 0.5f);
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.up;
        direction.Normalize();

        const float edge = 64f;
        float halfWidth = Screen.width * 0.5f - edge;
        float halfHeight = Screen.height * 0.5f - edge;
        float scale = Mathf.Min(halfWidth / Mathf.Max(Mathf.Abs(direction.x), 0.001f),
                                halfHeight / Mathf.Max(Mathf.Abs(direction.y), 0.001f));
        arrowRect.position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + direction * scale;
        arrowRect.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
    }

    private void Pause()
    {
        if (state != MissionState.Playing)
            return;

        state = MissionState.Paused;
        if (droneController != null) droneController.SetFlightInputEnabled(false);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (pausePanel != null) pausePanel.SetActive(true);
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
    }

    private void Resume()
    {
        if (state != MissionState.Paused)
            return;

        state = MissionState.Playing;
        if (droneController != null) droneController.SetFlightInputEnabled(true);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.gameObject.SetActive(true);
    }

    private void FailMission()
    {
        state = MissionState.Failed;
        if (droneController != null) droneController.SetFlightInputEnabled(false);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        if (failedPanel != null) failedPanel.SetActive(true);
    }

    public void FailOutOfBounds(string reason)
    {
        if (state != MissionState.Playing)
            return;

        if (failedPanel != null)
        {
            foreach (TMP_Text text in failedPanel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.gameObject.name == "Reason")
                {
                    text.text = reason;
                    break;
                }
            }
        }

        FailMission();
    }

    private void CompleteMission()
    {
        state = MissionState.Completed;
        if (droneController != null) droneController.SetFlightInputEnabled(false);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PlayerPrefs.SetInt("TrainingMissionCompleted", 1);
        PlayerPrefs.Save();
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        if (completedPanel != null) completedPanel.SetActive(true);
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

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
