using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Delivery mission: drop one or two packages into the marked landing zone.</summary>
public sealed class Lvl2MissionController : MonoBehaviour
{
    private enum MissionState { Playing, Paused, Failed, Completed }

    [Header("Миссия")]
    [SerializeField] private Transform drone;
    [SerializeField] private MissionDeliveryPackage firstPackage;
    [SerializeField] private GameObject packagePrefab;
    [SerializeField] private Transform deliveryTarget;
    [SerializeField] private Transform nextDeliveryTarget;
    [SerializeField, Min(0.1f)] private float targetRadius = 4f;
    [SerializeField, Min(0.1f)] private float collisionRadius = 0.55f;

    [Header("Интерфейс")]
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private TMP_Text instructionsLabel;
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
    [SerializeField] private AudioClip packageDropClip;

    private MissionState state = MissionState.Playing;
    private DroneKeyboardController droneController;
    private MissionDeliveryPackage attachedPackage;
    private readonly List<MissionDeliveryPackage> releasedPackages = new List<MissionDeliveryPackage>();
    private GameDifficulty difficulty;
    private int requiredDeliveries;
    private int completedDeliveries;
    private float timeRemaining = 40f;
    private AudioSource effectsSource;
    private bool waitingForCurrentDelivery;

    private void Start()
    {
        Time.timeScale = 1f;
        PlayMissionSound(startMissionClip);
        difficulty = GameSettingsService.Difficulty;
        requiredDeliveries = difficulty == GameDifficulty.Easy ? 1 : 2;
        droneController = drone != null ? drone.GetComponent<DroneKeyboardController>() : null;
        if (droneController != null) droneController.SetFlightInputEnabled(true);

        if (pausePanel != null) pausePanel.SetActive(false);
        if (failedPanel != null) failedPanel.SetActive(false);
        if (completedPanel != null) completedPanel.SetActive(false);
        if (pauseButton != null) pauseButton.onClick.AddListener(Pause);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(RestartMission);
        if (pauseMenuButton != null) pauseMenuButton.onClick.AddListener(ReturnToMenu);
        if (failedMenuButton != null) failedMenuButton.onClick.AddListener(ReturnToMenu);
        if (completedMapButton != null) completedMapButton.onClick.AddListener(ReturnToMap);

        if (deliveryTarget == null)
            deliveryTarget = FindTarget("Delivery Target 1 - Ground Circle");
        if (nextDeliveryTarget == null && requiredDeliveries > 1)
            nextDeliveryTarget = FindTarget("Delivery Target 2 - Ground Circle");

        attachedPackage = firstPackage;
        if (attachedPackage != null) attachedPackage.AttachToDrone(this, drone);
        SetTargetVisible(deliveryTarget, true);
        SetTargetVisible(nextDeliveryTarget, false);
        if (instructionsLabel != null)
            instructionsLabel.text = "Подведите дрон к кольцу на земле и нажмите K, чтобы сбросить коробку.";
        if (timerLabel != null) timerLabel.gameObject.SetActive(difficulty == GameDifficulty.Hard);
        UpdateProgress();
    }

    private void Update()
    {
        if (state == MissionState.Playing && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Pause();
        if (state != MissionState.Playing) return;

        if (difficulty == GameDifficulty.Hard)
        {
            timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
            if (timerLabel != null) timerLabel.text = $"ОСТАЛОСЬ  {Mathf.CeilToInt(timeRemaining)} СЕК";
            if (timeRemaining <= 0f)
            {
                FailMission("Время доставки вышло.");
                return;
            }
        }

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame && attachedPackage != null)
        {
            if (waitingForCurrentDelivery)
            {
                if (instructionsLabel != null)
                    instructionsLabel.text = "Дождитесь доставки первой посылки: затем появится круг у следующего дома.";
                return;
            }

            MissionDeliveryPackage packageToDrop = attachedPackage;
            attachedPackage = null;
            releasedPackages.Add(packageToDrop);
            packageToDrop.Release(drone);
            waitingForCurrentDelivery = true;
            if (completedDeliveries + releasedPackages.Count < requiredDeliveries)
                SpawnNextPackage();
            PlayMissionSound(packageDropClip);
            if (instructionsLabel != null)
                instructionsLabel.text = attachedPackage != null
                    ? "Новая посылка уже закреплена. Наведите дрон на круг и нажмите K."
                    : "Посылка сброшена. Дождитесь её приземления.";
        }

        if (drone != null && HasHitObstacle())
            FailMission("Дрон столкнулся с объектом.");
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

    public void OnPackageLanded(MissionDeliveryPackage package, Vector3 position, Collider surface)
    {
        if (state != MissionState.Playing || !releasedPackages.Contains(package))
            return;

        if (deliveryTarget == null)
        {
            FailMission("Зона доставки не найдена.");
            return;
        }

        Vector2 offset = new Vector2(position.x - deliveryTarget.position.x, position.z - deliveryTarget.position.z);
        bool hitTarget = offset.magnitude <= targetRadius;
        if (!hitTarget)
        {
            FailMission("Посылка упала вне отмеченной зоны.");
            return;
        }

        releasedPackages.Remove(package);
        completedDeliveries++;
        if (completedDeliveries >= requiredDeliveries)
        {
            CompleteMission();
            return;
        }

        if (nextDeliveryTarget != null)
        {
            SetTargetVisible(deliveryTarget, false);
            deliveryTarget = nextDeliveryTarget;
            nextDeliveryTarget = null;
            SetTargetVisible(deliveryTarget, true);
            waitingForCurrentDelivery = false;
            Debug.Log($"Next delivery zone activated at {deliveryTarget.position}.", this);
        }
        else if (completedDeliveries < requiredDeliveries)
        {
            FailMission("Следующая зона доставки не найдена.");
            return;
        }
        UpdateProgress();
        if (instructionsLabel != null)
            instructionsLabel.text = attachedPackage != null
                ? "Зона сместилась. Доставьте следующую посылку в новый круг клавишей K."
                : releasedPackages.Count > 0 ? "Посылка доставлена. Проверьте приземление следующей."
                : "Доставка принята.";
    }

    private Transform FindTarget(string targetName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform item in transforms)
            if (item.gameObject.scene == gameObject.scene && item.name == targetName)
                return item;
        return null;
    }

    private static void SetTargetVisible(Transform target, bool visible)
    {
        if (target == null) return;
        target.gameObject.SetActive(visible);
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = visible;
    }

    private void SpawnNextPackage()
    {
        if (packagePrefab == null || drone == null) return;
        GameObject instance = Instantiate(packagePrefab);
        instance.name = "Delivery Package 2";
        attachedPackage = instance.GetComponent<MissionDeliveryPackage>();
        if (attachedPackage == null) attachedPackage = instance.AddComponent<MissionDeliveryPackage>();
        attachedPackage.AttachToDrone(this, drone);
    }

    private void UpdateProgress()
    {
        if (progressLabel != null)
            progressLabel.text = $"ДОСТАВКА  {completedDeliveries} / {requiredDeliveries}";
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

    private void Pause()
    {
        if (state != MissionState.Playing) return;
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
        if (state != MissionState.Paused) return;
        state = MissionState.Playing;
        if (droneController != null) droneController.SetFlightInputEnabled(true);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.gameObject.SetActive(true);
    }

    private void FailMission(string reason)
    {
        if (state != MissionState.Playing) return;
        state = MissionState.Failed;
        if (droneController != null) droneController.SetFlightInputEnabled(false);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        if (failedPanel != null)
        {
            foreach (TMP_Text text in failedPanel.GetComponentsInChildren<TMP_Text>(true))
                if (text.gameObject.name == "Reason") { text.text = reason; break; }
            failedPanel.SetActive(true);
        }
    }

    public void FailOutOfBounds(string reason) => FailMission(reason);

    private void CompleteMission()
    {
        state = MissionState.Completed;
        if (droneController != null) droneController.SetFlightInputEnabled(false);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        int previousBest = PlayerPrefs.GetInt("Mission2BestDifficulty", -1);
        if ((int)difficulty > previousBest)
            PlayerPrefs.SetInt("Mission2BestDifficulty", (int)difficulty);
        PlayerPrefs.SetInt("Mission2Completed", 1);
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

    private void OnDestroy() => Time.timeScale = 1f;
}
