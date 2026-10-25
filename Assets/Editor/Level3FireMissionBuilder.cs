using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors lvl3 as ordinary editable Unity scene objects from the lvl2 city scene.</summary>
[InitializeOnLoad]
public static class Level3FireMissionBuilder
{
    private const string SourceScene = "Assets/Scenes/lvl2.unity";
    private const string ScenePath = "Assets/Scenes/lvl3.unity";
    private const string RepairMarkerPath = "Assets/Scenes/.lvl3-layout-repair";
    private const string DronePrefabPath = "Assets/Prefabs/_Drone [BumbleBee].prefab";
    private const string FirePrefabPath = "Assets/Prefabs/VFX_Fire_01_Big_Smoke Variant.prefab";
    private const string WaterMaterialPath = "Assets/Scenes/Lvl3WaterStream.mat";
    private const string DroneSoundPath = "Assets/sounds/drone sound.mp3";

    private static readonly Color Navy = new Color(0.025f, 0.055f, 0.10f, 0.96f);
    private static readonly Color PanelColor = new Color(0.035f, 0.085f, 0.14f, 0.98f);
    private static readonly Color Blue = new Color(0.10f, 0.68f, 0.92f, 1f);
    private static readonly Color Orange = new Color(1f, 0.46f, 0.15f, 1f);
    private static readonly Color White = new Color(0.94f, 0.97f, 1f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.72f, 0.81f, 1f);

    static Level3FireMissionBuilder()
    {
        EditorApplication.delayCall += BuildWhenMissing;
    }

    private static void BuildWhenMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (System.IO.File.Exists(RepairMarkerPath))
        {
            RepairLayoutAndCamera();
            return;
        }
        if (System.IO.File.Exists(ScenePath)) return;
        BuildLevel3FireScene();
    }

    [MenuItem("Tools/Missions/Repair lvl 3 HUD And Camera")]
    public static void RepairLayoutAndCamera()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !System.IO.File.Exists(ScenePath)) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RepairHud(scene);

        AudioClip droneSound = AssetDatabase.LoadAssetAtPath<AudioClip>(DroneSoundPath);
        foreach (DroneKeyboardController controller in Object.FindObjectsByType<DroneKeyboardController>(FindObjectsSortMode.None))
        {
            if (controller.gameObject.scene != scene) continue;
            SerializedObject controllerData = new SerializedObject(controller);
            SerializedProperty motorClip = controllerData.FindProperty("motorLoopClip");
            if (motorClip != null) motorClip.objectReferenceValue = droneSound;
            controllerData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        foreach (FireController fire in Object.FindObjectsByType<FireController>(FindObjectsSortMode.None))
        {
            if (fire.gameObject.scene != scene) continue;
            MissionObstacle nearestBuilding = null;
            float nearestDistance = float.MaxValue;
            Bounds nearestBounds = default;
            foreach (MissionObstacle obstacle in Object.FindObjectsByType<MissionObstacle>(FindObjectsSortMode.None))
            {
                if (obstacle.gameObject.scene != scene) continue;
                Renderer[] renderers = obstacle.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                float distance = Vector2.Distance(
                    new Vector2(bounds.center.x, bounds.center.z),
                    new Vector2(fire.transform.position.x, fire.transform.position.z));
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestBuilding = obstacle;
                    nearestBounds = bounds;
                }
            }
            if (nearestBuilding != null)
            {
                Vector3 position = fire.transform.position;
                position.y = nearestBounds.max.y - 0.35f;
                fire.transform.position = position;
                EditorUtility.SetDirty(fire.transform);
            }
        }

        foreach (string name in new[] { "Mission HUD", "PauseButton", "FireMissionProgress", "FireMissionTimer", "FireMissionInstructions" })
        {
            Transform item = FindTransform(scene, name);
            RectTransform rect = item as RectTransform;
            if (rect != null)
            {
                rect.pivot = new Vector2(0f, 1f);
                EditorUtility.SetDirty(rect);
            }
        }

        foreach (DroneCameraFollow follow in Object.FindObjectsByType<DroneCameraFollow>(FindObjectsSortMode.None))
        {
            if (follow.gameObject.scene != scene) continue;
            SerializedObject cameraData = new SerializedObject(follow);
            cameraData.FindProperty("followOffset").vector3Value = new Vector3(0f, 2.1f, -3.8f);
            cameraData.FindProperty("lookHeight").floatValue = 0.65f;
            cameraData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(follow);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (System.IO.File.Exists(RepairMarkerPath)) System.IO.File.Delete(RepairMarkerPath);
        AssetDatabase.Refresh();
        Debug.Log("Repaired lvl3 HUD, fire positions, drone motor audio, and chase camera.");
    }

    [MenuItem("Tools/Missions/Build lvl 3 Firefighting Scene")]
    public static void BuildLevel3FireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!System.IO.File.Exists(SourceScene))
            throw new System.IO.FileNotFoundException("Build lvl2 first so lvl3 can reuse its authored city.", SourceScene);
        if (!AssetDatabase.CopyAsset(SourceScene, ScenePath) && !System.IO.File.Exists(ScenePath))
            throw new System.InvalidOperationException("Could not copy the authored lvl2 city into lvl3.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (Lvl2MissionController controller in Object.FindObjectsByType<Lvl2MissionController>(FindObjectsSortMode.None))
            Object.DestroyImmediate(controller.gameObject);
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Lvl2 Mission Canvas") Object.DestroyImmediate(root);

        Transform city = FindTransform(scene, "POLYGON City - Training District");
        if (city == null) throw new System.InvalidOperationException("The authored campaign city root was not found in lvl2.");
        Transform oldDrone = null;
        foreach (DroneKeyboardController controller in Object.FindObjectsByType<DroneKeyboardController>(FindObjectsSortMode.None))
        {
            if (controller.gameObject.scene == scene)
            {
                oldDrone = controller.transform;
                Object.DestroyImmediate(controller.gameObject);
                break;
            }
        }

        GameObject dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrefabPath);
        if (dronePrefab == null) throw new System.InvalidOperationException($"BumbleBee prefab was not found: {DronePrefabPath}");
        GameObject playerDrone = (GameObject)PrefabUtility.InstantiatePrefab(dronePrefab, scene);
        playerDrone.name = "Player Drone - BumbleBee";
        playerDrone.transform.SetParent(null);
        playerDrone.transform.localScale = Vector3.one;
        playerDrone.transform.SetPositionAndRotation(city.position + new Vector3(0f, 10f, -14f), Quaternion.identity);
        foreach (MonoBehaviour behaviour in playerDrone.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        DroneKeyboardController flight = playerDrone.AddComponent<DroneKeyboardController>();
        SerializedObject flightData = new SerializedObject(flight);
        Set(flightData, "maxHorizontalDistanceFromCity", 125f);
        Set(flightData, "maxAltitudeAboveCity", 55f);
        flightData.FindProperty("boundsCenter").objectReferenceValue = city;
        flightData.ApplyModifiedPropertiesWithoutUndo();

        DroneCameraFollow cameraFollow = null;
        foreach (DroneCameraFollow follow in Object.FindObjectsByType<DroneCameraFollow>(FindObjectsSortMode.None))
            if (follow.gameObject.scene == scene) { cameraFollow = follow; break; }
        if (cameraFollow == null)
        {
            GameObject cameraObject = new GameObject("Drone Follow Camera", typeof(Camera), typeof(AudioListener), typeof(DroneCameraFollow));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            cameraFollow = cameraObject.GetComponent<DroneCameraFollow>();
        }
        cameraFollow.transform.SetPositionAndRotation(playerDrone.transform.position + new Vector3(0f, 3f, -6f), Quaternion.identity);
        SerializedObject cameraData = new SerializedObject(cameraFollow);
        cameraData.FindProperty("target").objectReferenceValue = playerDrone.transform;
        cameraData.ApplyModifiedPropertiesWithoutUndo();

        List<Vector3> roofPositions = FindRoofPositions(scene, city.position);
        GameObject firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefabPath);
        if (firePrefab == null) throw new System.InvalidOperationException($"Fire VFX prefab was not found: {FirePrefabPath}");
        FireController[] fires = new FireController[roofPositions.Count];
        for (int i = 0; i < roofPositions.Count; i++)
        {
            GameObject fire = (GameObject)PrefabUtility.InstantiatePrefab(firePrefab, scene);
            fire.name = $"Roof Fire {i + 1} - Extinguish Target";
            fire.transform.SetPositionAndRotation(roofPositions[i], Quaternion.identity);
            fire.transform.localScale = Vector3.one * 0.55f;
            fires[i] = fire.GetComponent<FireController>();
            if (fires[i] == null)
                throw new System.InvalidOperationException("The VFX fire prefab does not contain FireController.");
            foreach (AudioSource source in fire.GetComponentsInChildren<AudioSource>(true))
            {
                source.loop = true;
                source.playOnAwake = false;
                if (source.GetComponent<ProjectAudioChannel>() == null)
                    source.gameObject.AddComponent<ProjectAudioChannel>();
                GameSettingsService.ApplyToAudioSource(source);
            }
        }

        CreateMissionUi(scene, playerDrone.transform, fires);
        EnsureMissionInBuildSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Built editable lvl3 firefighting mission with a BumbleBee drone and {fires.Length} rooftop fire targets.");
    }

    private static List<Vector3> FindRoofPositions(Scene scene, Vector3 cityCenter)
    {
        var candidates = new List<(Vector3 position, float score)>();
        foreach (MissionObstacle obstacle in Object.FindObjectsByType<MissionObstacle>(FindObjectsSortMode.None))
        {
            if (obstacle.gameObject.scene != scene) continue;
            Renderer[] renderers = obstacle.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) continue;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.extents.y < 3f || bounds.max.y > cityCenter.y + 55f) continue;
            float horizontal = Vector2.Distance(new Vector2(bounds.center.x, bounds.center.z), new Vector2(cityCenter.x, cityCenter.z));
            if (horizontal > 92f) continue;
            // Prefer large roof surfaces that are spread around the city centre.
            float score = Mathf.Min(bounds.size.x, bounds.size.z) + bounds.size.y * 0.3f - horizontal * 0.035f;
            candidates.Add((new Vector3(bounds.center.x, bounds.max.y - 0.35f, bounds.center.z), score));
        }

        candidates = candidates.OrderByDescending(item => item.score).ToList();
        var selected = new List<Vector3>();
        foreach (var candidate in candidates)
        {
            if (selected.All(position => Vector2.Distance(new Vector2(position.x, position.z), new Vector2(candidate.position.x, candidate.position.z)) >= 22f))
            {
                selected.Add(candidate.position);
                if (selected.Count == 3) break;
            }
        }
        if (selected.Count != 3)
            throw new System.InvalidOperationException($"Could not find three spaced building rooftops for the fires ({selected.Count} found).");
        return selected;
    }

    private static void CreateMissionUi(Scene scene, Transform drone, FireController[] fires)
    {
        GameObject canvasObject = new GameObject("Lvl3 Mission Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        EventSystem events = Object.FindAnyObjectByType<EventSystem>();
        if (events == null)
        {
            GameObject eventsObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventsObject, scene);
            events = eventsObject.GetComponent<EventSystem>();
        }
        if (events.GetComponent<InputSystemUIInputModule>() == null)
            events.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

        RectTransform root = canvas.transform as RectTransform;
        Image hud = CreateImage("Mission HUD", root, Navy, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -28f), new Vector2(620f, 270f));
        hud.raycastTarget = false;
        Button pause = CreateButton("PauseButton", "Ⅱ  ПАУЗА", root, Orange, new Vector2(32f, -28f), new Vector2(190f, 52f), new Vector2(0f, 1f));
        TMP_Text progress = CreateText("FireMissionProgress", "ТУШЕНИЕ   0 / 3", root, 23f, White, new Vector2(32f, -96f), new Vector2(560f, 42f), new Vector2(0f, 1f), TextAlignmentOptions.MidlineLeft);
        TMP_Text timer = CreateText("FireMissionTimer", "ОСТАЛОСЬ  30 СЕК", root, 23f, Orange, new Vector2(32f, -140f), new Vector2(560f, 40f), new Vector2(0f, 1f), TextAlignmentOptions.MidlineLeft);
        TMP_Text instructions = CreateText("FireMissionInstructions", "WASD — полёт  ·  мышь — направление\nK — тушить пожар  ·  E — двигатель", root, 17f, Muted, new Vector2(32f, -184f), new Vector2(560f, 64f), new Vector2(0f, 1f), TextAlignmentOptions.TopLeft);

        var streamObject = new GameObject("Water Stream", typeof(LineRenderer));
        SceneManager.MoveGameObjectToScene(streamObject, scene);
        LineRenderer stream = streamObject.GetComponent<LineRenderer>();
        stream.useWorldSpace = true;
        stream.positionCount = 2;
        stream.startWidth = 0.11f;
        stream.endWidth = 0.035f;
        stream.numCapVertices = 4;
        stream.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        stream.sharedMaterial = GetWaterMaterial(shader);
        stream.startColor = new Color(0.25f, 0.88f, 1f, 0.9f);
        stream.endColor = new Color(0.12f, 0.55f, 1f, 0.45f);
        stream.enabled = false;

        GameObject pauseOverlay = CreateOverlay("PausePanel", "ПОЛЁТ ПРИОСТАНОВЛЕН", root);
        Button resume = CreateButton("ResumeButton", "ПРОДОЛЖИТЬ", pauseOverlay.transform, Blue, new Vector2(0f, -36f), new Vector2(360f, 58f));
        Button pauseMenu = CreateButton("PauseMenuButton", "В ГЛАВНОЕ МЕНЮ", pauseOverlay.transform, PanelColor, new Vector2(0f, -110f), new Vector2(360f, 58f));
        Button restart = CreateButton("RestartButton", "НАЧАТЬ ЗАНОВО", pauseOverlay.transform, PanelColor, new Vector2(0f, -184f), new Vector2(360f, 58f));
        pauseOverlay.SetActive(false);

        GameObject failedOverlay = CreateOverlay("FailedPanel", "МИССИЯ ПРОВАЛЕНА", root);
        CreateText("Reason", "", failedOverlay.transform, 20f, Muted, new Vector2(0f, -55f), new Vector2(520f, 58f));
        Button failedMenu = CreateButton("FailedMenuButton", "В ГЛАВНОЕ МЕНЮ", failedOverlay.transform, PanelColor, new Vector2(0f, -145f), new Vector2(360f, 58f));
        Button failedRestart = CreateButton("FailedRestartButton", "ПОВТОРИТЬ ВЫЛЕТ", failedOverlay.transform, Blue, new Vector2(0f, -218f), new Vector2(360f, 58f));
        failedOverlay.SetActive(false);

        GameObject completeOverlay = CreateOverlay("CompletedPanel", "ГОРОД В БЕЗОПАСНОСТИ", root);
        CreateText("CompletedCaption", "Все очаги потушены. Экипаж возвращается на базу.", completeOverlay.transform, 19f, Muted, new Vector2(0f, -58f), new Vector2(540f, 56f));
        Button completeMap = CreateButton("CompletedMapButton", "ВЕРНУТЬСЯ НА КАРТУ", completeOverlay.transform, Orange, new Vector2(0f, -145f), new Vector2(360f, 58f));
        completeOverlay.SetActive(false);

        Lvl3FireMissionController mission = new GameObject("Lvl3 Fire Mission Controller").AddComponent<Lvl3FireMissionController>();
        SceneManager.MoveGameObjectToScene(mission.gameObject, scene);
        SerializedObject data = new SerializedObject(mission);
        data.FindProperty("drone").objectReferenceValue = drone;
        SerializedProperty fireList = data.FindProperty("fires");
        fireList.arraySize = fires.Length;
        for (int i = 0; i < fires.Length; i++) fireList.GetArrayElementAtIndex(i).objectReferenceValue = fires[i];
        data.FindProperty("progressLabel").objectReferenceValue = progress;
        data.FindProperty("timerLabel").objectReferenceValue = timer;
        data.FindProperty("instructionsLabel").objectReferenceValue = instructions;
        data.FindProperty("waterStream").objectReferenceValue = stream;
        data.FindProperty("pausePanel").objectReferenceValue = pauseOverlay;
        data.FindProperty("failedPanel").objectReferenceValue = failedOverlay;
        data.FindProperty("completedPanel").objectReferenceValue = completeOverlay;
        data.FindProperty("pauseButton").objectReferenceValue = pause;
        data.FindProperty("resumeButton").objectReferenceValue = resume;
        data.FindProperty("restartButton").objectReferenceValue = restart;
        data.FindProperty("pauseMenuButton").objectReferenceValue = pauseMenu;
        data.FindProperty("failedMenuButton").objectReferenceValue = failedMenu;
        data.FindProperty("completedMapButton").objectReferenceValue = completeMap;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RepairHud(Scene scene)
    {
        RectTransform root = FindTransform(scene, "Lvl3 Mission Canvas") as RectTransform;
        if (root == null) return;

        SetHudRect(FindTransform(scene, "Mission HUD") as RectTransform, new Vector2(32f, -28f), new Vector2(620f, 270f));
        SetHudRect(FindTransform(scene, "PauseButton") as RectTransform, new Vector2(32f, -28f), new Vector2(190f, 52f));
        SetHudRect(FindTransform(scene, "FireMissionProgress") as RectTransform, new Vector2(32f, -96f), new Vector2(560f, 42f));
        SetHudRect(FindTransform(scene, "FireMissionTimer") as RectTransform, new Vector2(32f, -140f), new Vector2(560f, 40f));
        SetHudRect(FindTransform(scene, "FireMissionInstructions") as RectTransform, new Vector2(32f, -184f), new Vector2(560f, 64f));

        Button pause = FindTransform(scene, "PauseButton")?.GetComponent<Button>();
        if (pause != null)
        {
            Image image = pause.GetComponent<Image>();
            if (image != null) image.color = Orange;
            ColorBlock colors = pause.colors;
            colors.normalColor = Orange;
            colors.highlightedColor = Color.Lerp(Orange, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(Orange, Color.black, 0.16f);
            pause.colors = colors;
            TMP_Text label = pause.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "Ⅱ  ПАУЗА";
        }

        TMP_Text instructions = FindTransform(scene, "FireMissionInstructions")?.GetComponent<TMP_Text>();
        if (instructions != null) instructions.text = "WASD — полёт  ·  мышь — направление\nK — тушить пожар  ·  E — двигатель";
    }

    private static void SetHudRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        EditorUtility.SetDirty(rect);
    }

    private static GameObject CreateOverlay(string name, string title, Transform parent)
    {
        Image backdrop = CreateImage(name, parent, new Color(0.01f, 0.025f, 0.05f, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        backdrop.rectTransform.offsetMin = Vector2.zero;
        backdrop.rectTransform.offsetMax = Vector2.zero;
        backdrop.raycastTarget = true;
        Image card = CreateImage("Card", backdrop.transform, PanelColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 560f));
        card.raycastTarget = false;
        CreateText("Title", title, backdrop.transform, 32f, White, new Vector2(0f, 170f), new Vector2(590f, 64f));
        return backdrop.gameObject;
    }

    private static Button CreateButton(string name, string label, Transform parent, Color color, Vector2 anchoredPosition, Vector2 size, Vector2? anchor = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Vector2 targetAnchor = anchor ?? new Vector2(0.5f, 0.5f);
        rect.anchorMin = rect.anchorMax = targetAnchor;
        SetTopLeftPivotWhenNeeded(rect, targetAnchor);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.16f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        go.AddComponent<ButtonHoverEffect>();
        CreateText("Label", label, rect, 17f, White, Vector2.zero, size, alignment: TextAlignmentOptions.Center);
        return button;
    }

    private static TMP_Text CreateText(string name, string value, Transform parent, float size, Color color, Vector2 anchoredPosition, Vector2 dimensions, Vector2? anchor = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Vector2 targetAnchor = anchor ?? new Vector2(0.5f, 0.5f);
        rect.anchorMin = rect.anchorMax = targetAnchor;
        SetTopLeftPivotWhenNeeded(rect, targetAnchor);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        if (anchorMin == new Vector2(0f, 1f) && anchorMax == anchorMin) rect.pivot = anchorMin;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Transform FindTransform(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform;
        return null;
    }

    private static void Set(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.floatValue = value;
    }

    private static void SetTopLeftPivotWhenNeeded(RectTransform rect, Vector2 anchor)
    {
        if (anchor == new Vector2(0f, 1f)) rect.pivot = anchor;
    }

    private static void EnsureMissionInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(scene => scene.path != ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static Material GetWaterMaterial(Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "lvl3 Water Stream Material" };
            AssetDatabase.CreateAsset(material, WaterMaterialPath);
        }
        else material.shader = shader;
        material.color = new Color(0.22f, 0.83f, 1f, 0.82f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
