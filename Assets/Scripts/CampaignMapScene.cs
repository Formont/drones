using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CampaignMapScene : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.025f, 0.055f, 0.105f);
    private static readonly Color Panel = new Color(0.045f, 0.085f, 0.145f);
    private static readonly Color Blue = new Color(0.10f, 0.68f, 0.92f);
    private static readonly Color Orange = new Color(1f, 0.46f, 0.15f);
    private static readonly Color MutedText = new Color(0.59f, 0.70f, 0.81f);

    private TMP_Text statusLabel;
    private TMP_Text difficultyValueLabel;
    private TMP_Text startLabel;
    private TMP_Text missionTitleLabel;
    private TMP_Text missionDescriptionLabel;
    private TMP_Text missionTimeLabel;
    private TMP_Text missionCodeLabel;
    private TMP_Text panelEyebrowLabel;
    private TMP_Text markerLabel;
    private Button startButton;
    private CampaignMissionDefinition photoMission;
    private CampaignMissionDefinition selectedMission;
    private GameObject[] selectedMissionDetails;
    private RectTransform pulseRing;
    private Image pulseImage;
    private Sprite circleSprite;

    private void Start()
    {
        if (FindCanvasInThisScene() == null)
        {
            BuildInterface();
            BindRuntimeEvents();
        }
        else
            BindRuntimeEvents();
    }

    private void Update()
    {
        if (pulseRing == null || pulseImage == null) return;
        float pulse = (Mathf.Sin(Time.unscaledTime * 2.4f) + 1f) * 0.5f;
        float scale = Mathf.Lerp(1f, 1.42f, pulse);
        pulseRing.localScale = new Vector3(scale, scale, 1f);
        Color color = Blue;
        color.a = Mathf.Lerp(0.42f, 0.03f, pulse);
        pulseImage.color = color;
    }

    private void BuildInterface()
    {
        EnsureEventSystem();
        circleSprite = CreateCircleSprite();

        GameObject canvasObject = new GameObject("CampaignMapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform map = PanelRect("CityMap", canvas.transform, new Vector2(0f, 0f), new Vector2(0.8f, 1f), new Color(0.035f, 0.075f, 0.125f));
        DrawCity(map);
        RectTransform rightPanel = PanelRect("MissionDetailsPanel", canvas.transform, new Vector2(0.8f, 0f), Vector2.one, Panel);
        PanelRect("PanelAccent", rightPanel, new Vector2(0f, 0f), new Vector2(0.008f, 1f), Blue);

        AddLabel(map, "MapHeader", "ГОРОД  /  ЦЕНТРАЛЬНЫЙ РАЙОН", new Vector2(0.26f, 0.925f), new Vector2(0.77f, 0.975f), 27f, Color.white, TextAlignmentOptions.Left, true);
        AddLabel(map, "MapCoordinates", "КАРТА ЗАДАНИЙ     •     01 ДОСТУПНО", new Vector2(0.26f, 0.89f), new Vector2(0.72f, 0.925f), 15f, MutedText, TextAlignmentOptions.Left, false);
        AddButton(map, "BackToMenu", "←  МЕНЮ", new Vector2(0.035f, 0.925f), new Vector2(0.205f, 0.975f), new Color(0.10f, 0.19f, 0.29f), 16f, () => SceneManager.LoadScene("MainMenu"));
        AddLabel(map, "MapScale", "СЕВЕР ↑      •      МАСШТАБ 1:12 000", new Vector2(0.045f, 0.035f), new Vector2(0.34f, 0.075f), 13f, new Color(0.56f, 0.71f, 0.82f), TextAlignmentOptions.Left, false);

        BuildMissionMarker(map);
        BuildMissionPanel(rightPanel);
    }

    private void EnsureEventSystem()
    {
        EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        foreach (EventSystem system in systems)
        {
            if (system.gameObject.scene == gameObject.scene) return;
        }
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    public bool BuildEditorLayout()
    {
        if (FindCanvasInThisScene() != null) return false;
        BuildInterface();
        return true;
    }

    public bool ApplyMissionContentInEditor()
    {
        Canvas canvas = FindCanvasInThisScene();
        if (canvas == null)
            return false;

        bool changed = false;
        CampaignMissionDefinition definition = canvas.transform.Find("CityMap/PhotoMissionMarker")
            ?.GetComponent<CampaignMissionDefinition>();
        if (definition != null)
        {
            const string missionDescription = "Ваш первый самостоятельный вылет. Пройдите шесть учебных колец по очереди, удерживая дрон в воздухе и обходя здания и машины. Любое столкновение завершит тренировку. После успешного полёта на карте откроются новые задания.";
            changed |= definition.MissionTitle != "Школа оператора дрона"
                       || definition.Description != missionDescription
                       || definition.EstimatedTime != "Около 4 минут"
                       || definition.OperationCode != "T-001"
                       || definition.MarkerLabel != "УЧЁБА";
            if (changed)
                definition.SetMissionContent("Школа оператора дрона", missionDescription, "Около 4 минут", "T-001", "УЧЁБА");
        }

        Transform marker = canvas.transform.Find("CityMap/PhotoMissionMarker/PhotoMission");
        if (marker != null)
        {
            RectTransform bodyRect = marker.Find("CameraBody") as RectTransform;
            RectTransform handleRect = marker.Find("CameraTop") as RectTransform;
            RectTransform claspRect = marker.Find("CameraFlash") as RectTransform;
            if (bodyRect != null)
            {
                bodyRect.name = "BriefcaseBody";
                bodyRect.anchorMin = bodyRect.anchorMax = new Vector2(0.5f, 0.48f);
                bodyRect.sizeDelta = new Vector2(38f, 27f);
                changed = true;
            }
            if (handleRect != null)
            {
                handleRect.name = "BriefcaseHandle";
                handleRect.anchorMin = handleRect.anchorMax = new Vector2(0.5f, 0.77f);
                handleRect.sizeDelta = new Vector2(19f, 10f);
                changed = true;
            }
            if (claspRect != null)
            {
                claspRect.name = "BriefcaseClasp";
                claspRect.anchorMin = claspRect.anchorMax = new Vector2(0.5f, 0.48f);
                claspRect.sizeDelta = new Vector2(7f, 9f);
                changed = true;
            }
            foreach (string obsoleteIcon in new[] { "CameraLens", "CameraLensHighlight" })
            {
                Transform old = marker.Find(obsoleteIcon);
                if (old != null && old.gameObject.activeSelf)
                {
                    old.gameObject.SetActive(false);
                    changed = true;
                }
            }
        }

        TMP_Text tag = canvas.transform.Find("CityMap/PhotoMissionMarker/MissionTag")?.GetComponent<TMP_Text>();
        if (tag != null && tag.text != "УЧЁБА")
        {
            tag.text = "УЧЁБА";
            changed = true;
        }

        return changed;
    }

    private Canvas FindCanvasInThisScene()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.gameObject.scene == gameObject.scene && canvas.name == "CampaignMapCanvas")
                return canvas;
        }
        return null;
    }

    private void BindRuntimeEvents()
    {
        Canvas canvas = FindCanvasInThisScene();
        if (canvas == null) return;

        Button marker = canvas.transform.Find("CityMap/PhotoMissionMarker/PhotoMission")?.GetComponent<Button>();
        if (marker != null) marker.onClick.AddListener(SelectPhotoMission);
        photoMission = canvas.transform.Find("CityMap/PhotoMissionMarker")?.GetComponent<CampaignMissionDefinition>();

        Button start = canvas.transform.Find("MissionDetailsPanel/StartMissionButton")?.GetComponent<Button>();
        if (start != null)
        {
            start.onClick.AddListener(StartMission);
            startButton = start;
            startLabel = start.GetComponentInChildren<TMP_Text>();
        }

        Button back = canvas.transform.Find("CityMap/BackToMenu")?.GetComponent<Button>();
        if (back != null) back.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));

        pulseRing = canvas.transform.Find("CityMap/PhotoMissionMarker/PulseRing") as RectTransform;
        pulseImage = pulseRing != null ? pulseRing.GetComponent<Image>() : null;
        statusLabel = canvas.transform.Find("MissionDetailsPanel/MissionStatus")?.GetComponent<TMP_Text>();
        missionTitleLabel = canvas.transform.Find("MissionDetailsPanel/MissionTitle")?.GetComponent<TMP_Text>();
        missionDescriptionLabel = canvas.transform.Find("MissionDetailsPanel/MissionDescription")?.GetComponent<TMP_Text>();
        missionTimeLabel = canvas.transform.Find("MissionDetailsPanel/TimeValue")?.GetComponent<TMP_Text>();
        missionCodeLabel = canvas.transform.Find("MissionDetailsPanel/MissionCode")?.GetComponent<TMP_Text>();
        panelEyebrowLabel = canvas.transform.Find("MissionDetailsPanel/PanelEyebrow")?.GetComponent<TMP_Text>();
        markerLabel = canvas.transform.Find("CityMap/PhotoMissionMarker/MissionTag")?.GetComponent<TMP_Text>();
        selectedMissionDetails = new[]
        {
            canvas.transform.Find("MissionDetailsPanel/TitleAccent")?.gameObject,
            canvas.transform.Find("MissionDetailsPanel/TimeIcon")?.gameObject,
            canvas.transform.Find("MissionDetailsPanel/TimeCaption")?.gameObject,
            canvas.transform.Find("MissionDetailsPanel/TimeValue")?.gameObject,
            canvas.transform.Find("MissionDetailsPanel/DifficultyIcon")?.gameObject,
            canvas.transform.Find("MissionDetailsPanel/DifficultyCaption")?.gameObject,
            canvas.transform.Find("MissionDetailsPanel/DifficultyValue")?.gameObject,
            canvas.transform.Find("MissionDetailsPanel/MissionCode")?.gameObject
        };

        if (photoMission != null && markerLabel != null)
            markerLabel.text = photoMission.MarkerLabel;

        if (PlayerPrefs.GetInt("TrainingMissionCompleted", 0) == 1)
        {
            if (marker != null && marker.image != null)
            {
                Color gold = new Color(1f, 0.68f, 0.24f, 1f);
                marker.image.color = gold;
                ColorBlock markerColors = marker.colors;
                markerColors.normalColor = gold;
                markerColors.highlightedColor = Color.Lerp(gold, Color.white, 0.16f);
                markerColors.pressedColor = Color.Lerp(gold, Color.black, 0.14f);
                marker.colors = markerColors;
            }
            if (pulseRing != null) pulseRing.gameObject.SetActive(false);
            BuildUnlockedMissionBubbles(canvas.transform.Find("CityMap") as RectTransform);
        }

        ShowMissionPrompt();
        difficultyValueLabel = canvas.transform.Find("MissionDetailsPanel/DifficultyValue")?.GetComponent<TMP_Text>();
        GameSettingsService.DifficultyChanged += UpdateDifficultyLabel;
    }

    private void OnDestroy()
    {
        GameSettingsService.DifficultyChanged -= UpdateDifficultyLabel;
    }

    private void UpdateDifficultyLabel(GameDifficulty difficulty)
    {
        if (difficultyValueLabel != null && photoMission != null && startButton != null && startButton.interactable)
            difficultyValueLabel.text = GameSettingsService.GetDifficultyLabel(difficulty);
    }

    private void ShowMissionPrompt()
    {
        if (panelEyebrowLabel != null) panelEyebrowLabel.text = "КАРТА  /  ВЫБОР ЗАДАНИЯ";
        if (missionTitleLabel != null) missionTitleLabel.text = "Выберите\nмиссию";
        if (missionDescriptionLabel != null)
            missionDescriptionLabel.text = "Нажмите на голубой маркер на карте, чтобы прочитать описание задания и подготовить вылет.";
        if (statusLabel != null) statusLabel.text = "На карте доступно одно задание";
        if (startLabel != null) startLabel.text = "НАЧАТЬ МИССИЮ";
        if (startButton != null) startButton.interactable = false;

        if (selectedMissionDetails != null)
            foreach (GameObject detail in selectedMissionDetails)
                if (detail != null) detail.SetActive(false);
    }

    private void DrawCity(RectTransform parent)
    {
        float[] xEdges = { 0.045f, 0.17f, 0.405f, 0.64f, 0.845f, 0.955f };
        float[] yEdges = { 0.105f, 0.23f, 0.465f, 0.70f, 0.895f };
        Color[] blockTones =
        {
            new Color(0.075f, 0.14f, 0.20f), new Color(0.085f, 0.16f, 0.22f),
            new Color(0.09f, 0.17f, 0.23f), new Color(0.07f, 0.14f, 0.20f)
        };

        for (int row = 0; row < yEdges.Length - 1; row++)
        {
            for (int column = 0; column < xEdges.Length - 1; column++)
            {
                float left = xEdges[column] + 0.008f;
                float right = xEdges[column + 1] - 0.008f;
                float bottom = yEdges[row] + 0.008f;
                float top = yEdges[row + 1] - 0.008f;
                PanelRect("District", parent, new Vector2(left, bottom), new Vector2(right, top), blockTones[(row + column) % blockTones.Length]);

                int rows = (row + column) % 2 == 0 ? 3 : 2;
                int columns = column % 2 == 0 ? 3 : 2;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < columns; c++)
                    {
                        float cellW = (right - left) / columns;
                        float cellH = (top - bottom) / rows;
                        float marginX = cellW * 0.14f;
                        float marginY = cellH * 0.16f;
                        float bx0 = left + c * cellW + marginX;
                        float by0 = bottom + r * cellH + marginY;
                        float bx1 = left + (c + 1) * cellW - marginX;
                        float by1 = bottom + (r + 1) * cellH - marginY;
                        Color building = ((r + c + row) % 3 == 0)
                            ? new Color(0.13f, 0.23f, 0.29f)
                            : new Color(0.105f, 0.19f, 0.255f);
                        PanelRect("Building", parent, new Vector2(bx0, by0), new Vector2(bx1, by1), building);
                    }
                }
            }
        }

        float[] verticalRoads = { 0.17f, 0.405f, 0.64f, 0.845f };
        foreach (float x in verticalRoads)
        {
            PanelRect("Street", parent, new Vector2(x - 0.012f, 0.07f), new Vector2(x + 0.012f, 0.93f), new Color(0.035f, 0.085f, 0.135f));
            PanelRect("StreetMark", parent, new Vector2(x - 0.001f, 0.075f), new Vector2(x + 0.001f, 0.925f), new Color(0.11f, 0.22f, 0.29f, 0.8f));
        }
        float[] horizontalRoads = { 0.23f, 0.465f, 0.70f };
        foreach (float y in horizontalRoads)
        {
            PanelRect("Street", parent, new Vector2(0.035f, y - 0.014f), new Vector2(0.965f, y + 0.014f), new Color(0.035f, 0.085f, 0.135f));
            PanelRect("StreetMark", parent, new Vector2(0.04f, y - 0.001f), new Vector2(0.96f, y + 0.001f), new Color(0.11f, 0.22f, 0.29f, 0.8f));
        }

        PanelRect("Park", parent, new Vector2(0.69f, 0.745f), new Vector2(0.83f, 0.88f), new Color(0.08f, 0.22f, 0.20f));
        AddLabel(parent, "ParkLabel", "ПАРК", new Vector2(0.715f, 0.79f), new Vector2(0.805f, 0.83f), 13f, new Color(0.34f, 0.69f, 0.56f), TextAlignmentOptions.Center, false);

        for (int i = 0; i < 10; i++)
        {
            float y = 0.12f + i * 0.075f;
            PanelRect("RouteDash", parent, new Vector2(0.435f, y), new Vector2(0.44f, y + 0.028f), new Color(0.18f, 0.52f, 0.67f, 0.43f));
        }
    }

    private void BuildMissionMarker(RectTransform map)
    {
        RectTransform markerParent = RectTransformObject("PhotoMissionMarker", map);
        markerParent.gameObject.AddComponent<CampaignMissionDefinition>();
        markerParent.anchorMin = markerParent.anchorMax = new Vector2(0.46f, 0.54f);
        markerParent.anchoredPosition = Vector2.zero;
        markerParent.sizeDelta = new Vector2(116f, 116f);

        Image pulse = CreateImage("PulseRing", markerParent, circleSprite, new Color(0.1f, 0.68f, 0.92f, 0.35f));
        pulse.rectTransform.sizeDelta = new Vector2(108f, 108f);
        pulseRing = pulse.rectTransform;
        pulseImage = pulse;

        Image pointer = CreateImage("MarkerTail", markerParent, null, Blue);
        pointer.rectTransform.anchorMin = pointer.rectTransform.anchorMax = new Vector2(0.5f, 0.25f);
        pointer.rectTransform.anchoredPosition = new Vector2(0f, -4f);
        pointer.rectTransform.sizeDelta = new Vector2(22f, 22f);
        pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        Button marker = AddButton(markerParent, "PhotoMission", string.Empty, new Vector2(0.17f, 0.17f), new Vector2(0.83f, 0.83f), Blue, 16f, SelectPhotoMission, circleSprite);
        marker.image.rectTransform.anchorMin = marker.image.rectTransform.anchorMax = new Vector2(0.5f, 0.58f);
        marker.image.rectTransform.anchoredPosition = Vector2.zero;
        marker.image.rectTransform.sizeDelta = new Vector2(76f, 76f);
        DrawBriefcaseIcon(marker.transform);
        AddLabel(markerParent, "MissionTag", "УЧЁБА", new Vector2(0.10f, 0.01f), new Vector2(0.90f, 0.19f), 12f, Color.white, TextAlignmentOptions.Center, true);
    }

    private void DrawBriefcaseIcon(Transform parent)
    {
        Image body = CreateImage("BriefcaseBody", parent, RoundedSprite(), Color.white);
        body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.48f);
        body.rectTransform.anchoredPosition = Vector2.zero;
        body.rectTransform.sizeDelta = new Vector2(38f, 27f);

        Image handle = CreateImage("BriefcaseHandle", parent, RoundedSprite(), Color.white);
        handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0.5f, 0.77f);
        handle.rectTransform.anchoredPosition = Vector2.zero;
        handle.rectTransform.sizeDelta = new Vector2(19f, 10f);

        Image clasp = CreateImage("BriefcaseClasp", parent, RoundedSprite(), new Color(0.035f, 0.30f, 0.46f));
        clasp.rectTransform.anchorMin = clasp.rectTransform.anchorMax = new Vector2(0.5f, 0.48f);
        clasp.rectTransform.anchoredPosition = Vector2.zero;
        clasp.rectTransform.sizeDelta = new Vector2(7f, 9f);
    }

    private void BuildMissionPanel(RectTransform panel)
    {
        AddLabel(panel, "PanelEyebrow", "КАРТА  /  ВЫБОР ЗАДАНИЯ", new Vector2(0.10f, 0.91f), new Vector2(0.9f, 0.96f), 15f, Blue, TextAlignmentOptions.Left, true);
        AddLabel(panel, "MissionTitle", "Выберите\nмиссию", new Vector2(0.10f, 0.78f), new Vector2(0.92f, 0.90f), 31f, Color.white, TextAlignmentOptions.Left, true);
        GameObject titleAccent = PanelRect("TitleAccent", panel, new Vector2(0.10f, 0.755f), new Vector2(0.28f, 0.761f), Blue).gameObject;
        AddLabel(panel, "MissionDescription", "Нажмите на голубой маркер на карте, чтобы прочитать описание задания и подготовить вылет.", new Vector2(0.10f, 0.57f), new Vector2(0.90f, 0.72f), 18f, MutedText, TextAlignmentOptions.TopLeft, false);

        GameObject timeIcon = AddLabel(panel, "TimeIcon", "◷", new Vector2(0.10f, 0.475f), new Vector2(0.19f, 0.535f), 26f, Blue, TextAlignmentOptions.Center, true).gameObject;
        GameObject timeCaption = AddLabel(panel, "TimeCaption", "ВРЕМЯ", new Vector2(0.21f, 0.495f), new Vector2(0.88f, 0.53f), 12f, MutedText, TextAlignmentOptions.Left, true).gameObject;
        GameObject timeValue = AddLabel(panel, "TimeValue", "Около 8 минут", new Vector2(0.21f, 0.455f), new Vector2(0.88f, 0.495f), 17f, Color.white, TextAlignmentOptions.Left, false).gameObject;

        GameObject difficultyIcon = AddLabel(panel, "DifficultyIcon", "◎", new Vector2(0.10f, 0.37f), new Vector2(0.19f, 0.43f), 25f, Blue, TextAlignmentOptions.Center, true).gameObject;
        GameObject difficultyCaption = AddLabel(panel, "DifficultyCaption", "СЛОЖНОСТЬ", new Vector2(0.21f, 0.39f), new Vector2(0.88f, 0.425f), 12f, MutedText, TextAlignmentOptions.Left, true).gameObject;
        GameObject difficultyValue = AddLabel(panel, "DifficultyValue", "Начальная", new Vector2(0.21f, 0.35f), new Vector2(0.88f, 0.39f), 17f, Color.white, TextAlignmentOptions.Left, false).gameObject;

        Button start = AddButton(panel, "StartMissionButton", "НАЧАТЬ МИССИЮ", new Vector2(0.08f, 0.11f), new Vector2(0.92f, 0.20f), Orange, 18f, StartMission);
        start.interactable = false;
        startLabel = start.GetComponentInChildren<TMP_Text>();
        statusLabel = AddLabel(panel, "MissionStatus", "На карте доступно одно задание", new Vector2(0.10f, 0.055f), new Vector2(0.90f, 0.095f), 13f, MutedText, TextAlignmentOptions.Center, false);
        GameObject missionCode = AddLabel(panel, "MissionCode", "КОД ОПЕРАЦИИ   C-001", new Vector2(0.10f, 0.015f), new Vector2(0.90f, 0.045f), 11f, new Color(0.34f, 0.48f, 0.60f), TextAlignmentOptions.Center, false).gameObject;

        titleAccent.SetActive(false);
        timeIcon.SetActive(false);
        timeCaption.SetActive(false);
        timeValue.SetActive(false);
        difficultyIcon.SetActive(false);
        difficultyCaption.SetActive(false);
        difficultyValue.SetActive(false);
        missionCode.SetActive(false);
    }

    private void SelectPhotoMission()
    {
        SelectMission(photoMission);
    }

    private void SelectMission(CampaignMissionDefinition mission)
    {
        if (mission == null) return;
        selectedMission = mission;
        if (panelEyebrowLabel != null) panelEyebrowLabel.text = $"ЗАДАНИЕ  /  {mission.OperationCode}";
        if (missionTitleLabel != null) missionTitleLabel.text = mission.MissionTitle;
        if (missionDescriptionLabel != null) missionDescriptionLabel.text = mission.Description;
        if (missionTimeLabel != null) missionTimeLabel.text = mission.EstimatedTime;
        if (missionCodeLabel != null) missionCodeLabel.text = $"КОД ОПЕРАЦИИ   {mission.OperationCode}";
        if (difficultyValueLabel != null) difficultyValueLabel.text = GameSettingsService.GetDifficultyLabel(GameSettingsService.Difficulty);
        if (statusLabel != null) statusLabel.text = "Задание готово к запуску";
        if (startLabel != null) startLabel.text = "НАЧАТЬ МИССИЮ";
        if (startButton != null) startButton.interactable = true;

        if (selectedMissionDetails != null)
            foreach (GameObject detail in selectedMissionDetails)
                if (detail != null) detail.SetActive(true);
    }

    private void StartMission()
    {
        if (selectedMission == null || startButton == null || !startButton.interactable)
            return;

        SceneManager.LoadScene(selectedMission.SceneName);
    }

    private void BuildUnlockedMissionBubbles(RectTransform map)
    {
        if (map == null || map.Find("UnlockedMissionBubbles") != null)
            return;

        if (circleSprite == null)
            circleSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");

        RectTransform group = RectTransformObject("UnlockedMissionBubbles", map);
        group.anchorMin = Vector2.zero;
        group.anchorMax = Vector2.one;
        group.offsetMin = Vector2.zero;
        group.offsetMax = Vector2.zero;

        Vector2[] positions =
        {
            new Vector2(0.58f, 0.68f),
            new Vector2(0.64f, 0.48f)
        };

        CampaignMissionDefinition delivery = null;
        for (int i = 0; i < positions.Length; i++)
        {
            RectTransform bubble = RectTransformObject($"MissionBubble{i + 2}", group);
            bubble.anchorMin = bubble.anchorMax = positions[i];
            bubble.anchoredPosition = Vector2.zero;
            bubble.sizeDelta = new Vector2(74f, 74f);

            Image halo = CreateImage("BubbleHalo", bubble, circleSprite, new Color(0.10f, 0.68f, 0.92f, 0.20f));
            halo.rectTransform.sizeDelta = new Vector2(74f, 74f);
            int missionNumber = i + 2;
            int bestDifficulty = PlayerPrefs.GetInt($"Mission{missionNumber}BestDifficulty", -1);
            Color medal = bestDifficulty >= (int)GameDifficulty.Hard ? new Color(1f, 0.72f, 0.25f)
                : bestDifficulty == (int)GameDifficulty.Normal ? new Color(0.78f, 0.86f, 0.94f)
                : bestDifficulty == (int)GameDifficulty.Easy ? new Color(0.77f, 0.47f, 0.26f)
                : new Color(0.10f, 0.68f, 0.92f);
            Image ring = CreateImage("MedalRing", bubble, circleSprite, medal);
            ring.rectTransform.sizeDelta = new Vector2(66f, 66f);
            Image marker = CreateImage("Bubble", bubble, circleSprite, new Color(0.08f, 0.42f, 0.63f, 0.98f));
            marker.rectTransform.sizeDelta = new Vector2(56f, 56f);

            bool fireMissionUnlocked = i == 1 && PlayerPrefs.GetInt("Mission2Completed", 0) == 1;
            if (i == 0)
            {
                delivery = bubble.gameObject.AddComponent<CampaignMissionDefinition>();
                delivery.SetMissionContent(
                    "Посылка с воздуха",
                    "Доставьте хрупкую посылку в круг на земле рядом с домом. Нажмите K, чтобы сбросить коробку. После первой доставки круг переместится к другому кварталу. На обычной и сложной настройках нужно доставить две посылки; на сложной на это даётся 40 секунд. Ошибка при сбросе или столкновение завершит вылет.",
                    bestDifficulty == (int)GameDifficulty.Hard ? "До 40 секунд" : "Около 2 минут",
                    "D-002", "ДОСТАВКА", "lvl2");
                Button missionButton = marker.gameObject.AddComponent<Button>();
                missionButton.targetGraphic = marker;
                missionButton.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = missionButton.colors;
                colors.normalColor = marker.color;
                colors.highlightedColor = Color.Lerp(marker.color, Color.white, 0.18f);
                colors.pressedColor = Color.Lerp(marker.color, Color.black, 0.15f);
                missionButton.colors = colors;
                missionButton.onClick.AddListener(() => SelectMission(delivery));
                marker.raycastTarget = true;
                marker.gameObject.AddComponent<ButtonHoverEffect>();
                DrawBoxMissionIcon(marker.transform);
                AddLabel(bubble, "MissionTag", "ПОСЫЛКА", new Vector2(-0.18f, -0.15f), new Vector2(1.18f, 0.12f), 9f, Color.white, TextAlignmentOptions.Center, true);
            }
            else if (i == 1 && fireMissionUnlocked)
            {
                CampaignMissionDefinition fireMission = bubble.gameObject.AddComponent<CampaignMissionDefinition>();
                fireMission.SetMissionContent(
                    "Тушение над кварталом",
                    "На крышах нескольких домов вспыхнул огонь. Используйте дрон BumbleBee с водяной пушкой и потушите три небольших очага. На лёгкой сложности таймера нет, на нормальной даётся 30 секунд, на сложной — 40 секунд. Столкновение или вылет за границы города завершит миссию.",
                    GameSettingsService.Difficulty == GameDifficulty.Easy ? "Без ограничения времени"
                        : GameSettingsService.Difficulty == GameDifficulty.Normal ? "30 секунд" : "40 секунд",
                    "F-003", "ПОЖАР", "lvl3");
                Button missionButton = marker.gameObject.AddComponent<Button>();
                missionButton.targetGraphic = marker;
                missionButton.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = missionButton.colors;
                colors.normalColor = marker.color;
                colors.highlightedColor = Color.Lerp(marker.color, Color.white, 0.18f);
                colors.pressedColor = Color.Lerp(marker.color, Color.black, 0.15f);
                missionButton.colors = colors;
                missionButton.onClick.AddListener(() => SelectMission(fireMission));
                marker.raycastTarget = true;
                marker.gameObject.AddComponent<ButtonHoverEffect>();
                DrawFireMissionIcon(marker.transform);
                AddLabel(bubble, "MissionTag", "ПОЖАР", new Vector2(-0.18f, -0.15f), new Vector2(1.18f, 0.12f), 9f, Color.white, TextAlignmentOptions.Center, true);
            }
            else
            {
                marker.raycastTarget = false;
                AddLabel(bubble, "Number", $"0{i + 2}", new Vector2(0.16f, 0.23f), new Vector2(0.84f, 0.77f), 19f, Color.white, TextAlignmentOptions.Center, true);
                AddLabel(bubble, "ComingSoon", "НОВОЕ", new Vector2(-0.18f, -0.10f), new Vector2(1.18f, 0.18f), 10f, Color.white, TextAlignmentOptions.Center, true);
            }
        }
    }

    private void DrawBoxMissionIcon(Transform parent)
    {
        Image box = CreateImage("BoxIcon", parent, RoundedSprite(), new Color(0.88f, 0.64f, 0.34f));
        box.rectTransform.sizeDelta = new Vector2(28f, 25f);
        box.raycastTarget = false;
        Image tape = CreateImage("BoxTape", parent, null, new Color(0.96f, 0.82f, 0.54f));
        tape.rectTransform.sizeDelta = new Vector2(6f, 25f);
        tape.raycastTarget = false;
        Image flap = CreateImage("BoxFlap", parent, null, new Color(0.76f, 0.49f, 0.24f));
        flap.rectTransform.anchoredPosition = new Vector2(0f, 13f);
        flap.rectTransform.sizeDelta = new Vector2(28f, 5f);
        flap.raycastTarget = false;
    }

    private void DrawFireMissionIcon(Transform parent)
    {
        Image outerFlame = CreateImage("FireIcon", parent, RoundedSprite(), new Color(1f, 0.56f, 0.16f));
        outerFlame.rectTransform.sizeDelta = new Vector2(21f, 31f);
        outerFlame.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -35f);
        outerFlame.raycastTarget = false;
        Image innerFlame = CreateImage("FireCore", parent, RoundedSprite(), new Color(1f, 0.88f, 0.52f));
        innerFlame.rectTransform.sizeDelta = new Vector2(10f, 17f);
        innerFlame.rectTransform.anchoredPosition = new Vector2(1f, -4f);
        innerFlame.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -35f);
        innerFlame.raycastTarget = false;
    }

    private Button AddButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color color, float fontSize, UnityEngine.Events.UnityAction onClick, Sprite sprite = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.17f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        go.AddComponent<ButtonHoverEffect>();

        if (!string.IsNullOrEmpty(label))
            AddLabel(rect, "Label", label, Vector2.zero, Vector2.one, fontSize, Color.white, TextAlignmentOptions.Center, true);
        return button;
    }

    private TMP_Text AddLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize, Color color, TextAlignmentOptions alignment, bool bold)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(4f, 2f);
        rect.offsetMax = new Vector2(-4f, -2f);

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private RectTransform PanelRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        Image image = CreateImage(name, parent, null, color);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(100f, 100f);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private RectTransform RectTransformObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private Sprite RoundedSprite()
    {
        return Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private Sprite CreateCircleSprite()
    {
        return Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
    }
}
