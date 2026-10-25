using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Self-contained keyboard flight and motor control for the drone prefab.
/// Uses only the new Input System and does not depend on DronePack scripts.
/// </summary>
public sealed class DroneKeyboardController : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField, Min(0f)] private float moveSpeed = 8f;
    [SerializeField, Min(0f)] private float verticalSpeed = 5f;
    [SerializeField, Min(0f)] private float acceleration = 12f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField, Range(0f, 89f)] private float maxPitch = 35f;
    [SerializeField, Min(0f)] private float gravity = 9.81f;

    [Header("Mission Flight Bounds")]
    [SerializeField, Min(1f)] private float maxHorizontalDistanceFromCity = 125f;
    [SerializeField, Min(1f)] private float maxAltitudeAboveCity = 55f;
    [SerializeField] private Transform boundsCenter;

    [Header("Fuel and Motors")]
    [SerializeField, Min(1f)] private float fuelCapacity = 100f;
    [SerializeField, Min(0f)] private float fuelConsumptionPerSecond = 0.5f;
    [SerializeField] private bool motorsOnAtStart = true;
    [SerializeField] private AudioClip motorLoopClip;

    private Vector3 currentVelocity;
    private float fuel;
    private float pitch;
    private float yaw;
    private bool motorsOn;
    private bool cursorCaptured;
    private bool flightInputEnabled = true;
    private Animator[] propellerAnimators;
    private GUIStyle hudBoxStyle;
    private GUIStyle hudTextStyle;
    private Vector3 boundsCenterPosition;
    private float boundsGroundLevel;
    private bool boundsFailureHandled;
    private bool showFallbackFailure;
    private AudioSource motorAudioSource;

    public bool MotorsRunning => motorsOn && flightInputEnabled;

    private void Awake()
    {
        fuel = fuelCapacity;
        motorsOn = motorsOnAtStart;
        yaw = transform.eulerAngles.y;

        if (boundsCenter == null)
        {
            GameObject city = GameObject.Find("POLYGON City - Training District");
            if (city != null)
                boundsCenter = city.transform;
        }
        boundsCenterPosition = boundsCenter != null ? boundsCenter.position : transform.position;
        boundsGroundLevel = boundsCenter != null ? boundsCenter.position.y : 0f;

        // The imported model has dynamic Rigidbodies on nested variants.
        // Make them kinematic so only this root controller owns flight motion.
        Rigidbody[] childBodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody body in childBodies)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }

        FindPropellerAnimators();
        UpdatePropellers();
    }

    private void Start()
    {
        SetCursorCaptured(true);
        ConfigureMotorAudio();
        UpdateMotorAudio();
    }

    private void Update()
    {
        if (!flightInputEnabled)
            return;

        float deltaTime = Time.deltaTime;
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null)
        {
            if (keyboard.eKey.wasPressedThisFrame)
                motorsOn = !motorsOn && fuel > 0f;

            if (keyboard.escapeKey.wasPressedThisFrame)
                SetCursorCaptured(false);

            if (!cursorCaptured && mouse != null && mouse.leftButton.wasPressedThisFrame)
                SetCursorCaptured(true);

            ReadFlightControls(keyboard, mouse, deltaTime);
        }

        if (motorsOn)
        {
            fuel = Mathf.Max(0f, fuel - fuelConsumptionPerSecond * deltaTime);
            if (fuel <= 0f)
                motorsOn = false;
        }

        ApplyFlight(deltaTime);
        UpdatePropellers();
        UpdateMotorAudio();
        CheckMissionBounds();
    }

    private void CheckMissionBounds()
    {
        if (boundsFailureHandled)
            return;

        Vector3 offset = transform.position - boundsCenterPosition;
        float horizontalDistance = new Vector2(offset.x, offset.z).magnitude;
        float altitude = transform.position.y - boundsGroundLevel;
        if (horizontalDistance <= maxHorizontalDistanceFromCity && altitude <= maxAltitudeAboveCity)
            return;

        boundsFailureHandled = true;
        string reason = horizontalDistance > maxHorizontalDistanceFromCity
            ? "Дрон покинул границы города."
            : "Дрон поднялся выше допустимой высоты.";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Lvl1MissionController levelOne = FindAnyObjectByType<Lvl1MissionController>();
        if (levelOne != null)
        {
            levelOne.FailOutOfBounds(reason);
            return;
        }

        Lvl2MissionController levelTwo = FindAnyObjectByType<Lvl2MissionController>();
        if (levelTwo != null)
        {
            levelTwo.FailOutOfBounds(reason);
            return;
        }

        Lvl3FireMissionController levelThree = FindAnyObjectByType<Lvl3FireMissionController>();
        if (levelThree != null)
        {
            levelThree.FailOutOfBounds(reason);
            return;
        }

        showFallbackFailure = true;
        SetFlightInputEnabled(false);
        Time.timeScale = 0f;
    }

    public void SetFlightInputEnabled(bool enabled)
    {
        flightInputEnabled = enabled;
        if (!enabled)
        {
            SetCursorCaptured(false);
            if (motorAudioSource != null) motorAudioSource.Stop();
        }
        else
        {
            SetCursorCaptured(true);
            UpdateMotorAudio();
        }
    }

    private void ConfigureMotorAudio()
    {
        if (motorLoopClip == null) return;
        motorAudioSource = GetComponent<AudioSource>();
        if (motorAudioSource == null) motorAudioSource = gameObject.AddComponent<AudioSource>();
        motorAudioSource.clip = motorLoopClip;
        motorAudioSource.loop = true;
        motorAudioSource.playOnAwake = false;
        motorAudioSource.spatialBlend = 0.35f;
        motorAudioSource.volume = 0.45f;
        if (GetComponent<ProjectAudioChannel>() == null)
            gameObject.AddComponent<ProjectAudioChannel>();
        GameSettingsService.ApplyToAudioSource(motorAudioSource);
    }

    private void UpdateMotorAudio()
    {
        if (motorAudioSource == null || motorLoopClip == null) return;
        if (motorsOn && flightInputEnabled && GameSettingsService.SoundEnabled)
        {
            if (!motorAudioSource.isPlaying) motorAudioSource.Play();
        }
        else if (motorAudioSource.isPlaying)
            motorAudioSource.Stop();
    }

    private void ReadFlightControls(Keyboard keyboard, Mouse mouse, float deltaTime)
    {
        Vector2 look = cursorCaptured && mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        yaw += look.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, -maxPitch, maxPitch);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector2 planarInput = Vector2.zero;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) planarInput.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) planarInput.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) planarInput.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) planarInput.y += 1f;
        planarInput = Vector2.ClampMagnitude(planarInput, 1f);

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 desiredVelocity = (forward * planarInput.y + right * planarInput.x) * moveSpeed;
        float ascend = keyboard.spaceKey.isPressed ? 1f : 0f;
        bool descendPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed
                              || keyboard.cKey.isPressed || keyboard.leftShiftKey.isPressed;
        if (motorsOn)
            desiredVelocity += Vector3.up * ((ascend - (descendPressed ? 1f : 0f)) * verticalSpeed);
        else
            desiredVelocity.y = currentVelocity.y;

        currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, desiredVelocity.x, acceleration * deltaTime);
        currentVelocity.z = Mathf.MoveTowards(currentVelocity.z, desiredVelocity.z, acceleration * deltaTime);
        if (motorsOn)
            currentVelocity.y = Mathf.MoveTowards(currentVelocity.y, desiredVelocity.y, acceleration * deltaTime);
    }

    private void ApplyFlight(float deltaTime)
    {
        if (!motorsOn)
            currentVelocity.y -= gravity * deltaTime;

        Vector3 movement = currentVelocity * deltaTime;
        if (movement.y < 0f)
        {
            RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 0.15f,
                Vector3.down, Mathf.Abs(movement.y) + 0.2f);
            float nearestGround = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(transform))
                    continue;

                float groundOffset = hit.point.y - transform.position.y + 0.05f;
                if (groundOffset < nearestGround)
                    nearestGround = groundOffset;
            }

            if (!float.IsPositiveInfinity(nearestGround))
            {
                movement.y = Mathf.Max(movement.y, Mathf.Min(0f, nearestGround));
                currentVelocity.y = 0f;
            }
        }

        transform.position += movement;
    }

    private void UpdatePropellers()
    {
        if (propellerAnimators == null)
            return;

        foreach (Animator animator in propellerAnimators)
        {
            if (animator == null || !animator.gameObject.activeInHierarchy)
                continue;

            animator.enabled = motorsOn;
        }
    }

    private void FindPropellerAnimators()
    {
        Animator[] allAnimators = GetComponentsInChildren<Animator>(true);
        List<Animator> found = new List<Animator>();
        foreach (Animator animator in allAnimators)
        {
            if (animator == null || animator.transform == transform)
                continue;

            string objectName = animator.gameObject.name;
            bool isPropeller = objectName.IndexOf("propeller", System.StringComparison.OrdinalIgnoreCase) >= 0
                               || objectName.StartsWith("fan", System.StringComparison.OrdinalIgnoreCase)
                               || objectName.IndexOf("rotor", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (isPropeller)
                found.Add(animator);
        }

        propellerAnimators = found.ToArray();
    }

    private void SetCursorCaptured(bool captured)
    {
        cursorCaptured = captured;
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    private void OnDisable()
    {
        if (motorAudioSource != null) motorAudioSource.Stop();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnGUI()
    {
        if (showFallbackFailure)
        {
            DrawFallbackFailure();
            return;
        }

        if (hudBoxStyle == null)
        {
            hudBoxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                padding = new RectOffset(16, 16, 12, 12)
            };
            hudTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white }
            };
        }

        const float panelWidth = 270f;
        const float panelHeight = 112f;
        Rect panel = new Rect(Screen.width - panelWidth - 24f, 24f, panelWidth, panelHeight);
        GUI.Box(panel, GUIContent.none, hudBoxStyle);
        GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, 235f, 22f),
            $"ТОПЛИВО   {Mathf.CeilToInt(fuel)}%", hudTextStyle);

        Rect bar = new Rect(panel.x + 16f, panel.y + 39f, panelWidth - 32f, 12f);
        GUI.color = new Color(0.08f, 0.12f, 0.17f, 1f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = fuel > 25f ? new Color(0.18f, 0.75f, 0.95f) : new Color(1f, 0.48f, 0.18f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fuel / fuelCapacity), bar.height),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        string status = motorsOn ? "МОТОРЫ ВКЛ.  ·  E — переключить" : "МОТОРЫ ВЫКЛ.  ·  E — включить";
        GUI.Label(new Rect(panel.x + 16f, panel.y + 56f, 240f, 20f), status, hudTextStyle);
        GUI.Label(new Rect(panel.x + 16f, panel.y + 77f, 240f, 20f),
            "Мышь — курс  ·  WASD — движение", hudTextStyle);
        GUI.Label(new Rect(panel.x + 16f, panel.y + 94f, 240f, 18f),
            "Space — вверх  ·  Ctrl — вниз", hudTextStyle);
    }

    private void DrawFallbackFailure()
    {
        const float width = 520f;
        const float height = 300f;
        Rect area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(area, GUIContent.none);
        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.47f, 0.18f) }
        };
        GUIStyle message = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(area.x + 24f, area.y + 28f, width - 48f, 60f), "МИССИЯ ПРОВАЛЕНА", title);
        GUI.Label(new Rect(area.x + 24f, area.y + 91f, width - 48f, 48f), "Дрон покинул границы зоны задания.", message);

        if (GUI.Button(new Rect(area.x + 70f, area.y + 166f, width - 140f, 48f), "ПОВТОРИТЬ МИССИЮ"))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        if (GUI.Button(new Rect(area.x + 70f, area.y + 226f, width - 140f, 48f), "В ГЛАВНОЕ МЕНЮ"))
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
                SceneManager.LoadScene("MainMenu");
            else if (SceneManager.sceneCountInBuildSettings > 0)
                SceneManager.LoadScene(0);
        }
    }
}
