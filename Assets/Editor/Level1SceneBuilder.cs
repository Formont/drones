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

/// <summary>
/// One-time editor authoring utility. It writes ordinary prefab instances and UI objects
/// into lvl 1.unity; the city itself is not created or generated at runtime.
/// </summary>
[InitializeOnLoad]
public static class Level1SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/lvl 1.unity";
    private const string Level2ScenePath = "Assets/Scenes/lvl2.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string MainMenuDroneRenderTexturePath = "Assets/Scenes/MainMenuDrone.renderTexture";
    private const string CityRootName = "POLYGON City - Training District";
    private const string CarMaterialPath = "Assets/Pack_FREE_Cars/Materials/Lvl1CarPaletteLit.mat";
    private const string CheckpointMaterialPath = "Assets/Scenes/Lvl1CheckpointGlow.mat";
    private const string CheckpointMeshPath = "Assets/Scenes/Lvl1CheckpointRing.asset";
    private const string TexturedPalettePath = "Assets/Pack_FREE_Cars/Materials/Textures/ColorPalette_Textured.png";
    private const string GroundMaterialPath = "Assets/Scenes/Lvl1GroundDarkGreen.mat";
    private const string DeliveryRingMeshPath = "Assets/Scenes/Lvl2DeliveryTargetRing.asset";
    private const string DeliveryRingMaterialPath = "Assets/Scenes/Lvl2DeliveryTarget.mat";

    private static readonly Color Navy = new Color(0.025f, 0.055f, 0.10f, 0.96f);
    private static readonly Color PanelColor = new Color(0.035f, 0.085f, 0.14f, 0.97f);
    private static readonly Color Blue = new Color(0.10f, 0.68f, 0.92f, 1f);
    private static readonly Color Orange = new Color(1f, 0.46f, 0.15f, 1f);
    private static readonly Color White = new Color(0.94f, 0.97f, 1f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.72f, 0.81f, 1f);

    static Level1SceneBuilder()
    {
        EditorApplication.delayCall += BuildWhenMissing;
    }

    [MenuItem("Tools/Missions/Build lvl 1 Scene")]
    public static void BuildWhenMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || System.IO.File.Exists(ScenePath))
            return;

        BuildScene();
    }

    [MenuItem("Tools/Missions/Repair lvl 1 Checkpoints And Cars")]
    public static void RepairExistingScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !System.IO.File.Exists(ScenePath))
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Mesh ringMesh = GetOrCreateCheckpointMesh();
        Material ringMaterial = GetOrCreateCheckpointMaterial();
        Material carMaterial = GetOrCreateCarMaterial();
        Material groundMaterial = GetOrCreateGroundMaterial();
        GameObject playerDrone = GameObject.Find("Player Drone");
        if (playerDrone != null)
        {
            DroneKeyboardController controller = playerDrone.GetComponent<DroneKeyboardController>();
            if (controller != null)
            {
                SerializedObject serializedDrone = new SerializedObject(controller);
                serializedDrone.FindProperty("motorLoopClip").objectReferenceValue = LoadMissionSound("drone sound.mp3");
                serializedDrone.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }
        }
        Lvl1MissionController levelOne = Object.FindAnyObjectByType<Lvl1MissionController>();
        if (levelOne != null)
        {
            SerializedObject serializedMission = new SerializedObject(levelOne);
            serializedMission.FindProperty("startMissionClip").objectReferenceValue = LoadMissionSound("start_mission.mp3");
            serializedMission.FindProperty("checkpointClip").objectReferenceValue = LoadMissionSound("checkpoint.mp3");
            serializedMission.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(levelOne);
        }
        EnsureParkVegetation(scene);

        GameObject ground = GameObject.Find("Training district ground");
        if (ground != null)
        {
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer != null) groundRenderer.sharedMaterial = groundMaterial;
            EditorUtility.SetDirty(ground);
        }

        RectTransform pauseRect = FindSceneRect(scene, "PauseButton");
        if (pauseRect != null)
            SetAnchors(pauseRect, new Vector2(0.035f, 0.945f), new Vector2(0.18f, 0.99f));
        RectTransform progressRect = FindSceneRect(scene, "MissionProgress");
        if (progressRect != null)
            SetAnchors(progressRect, new Vector2(0.035f, 0.875f), new Vector2(0.44f, 0.925f));
        int repairedRings = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                if (!item.name.StartsWith("Checkpoint ", System.StringComparison.Ordinal))
                    continue;

                FixCheckpointVisual(item, ringMesh, ringMaterial);
                repairedRings++;
            }
        }

        foreach (MissionTrafficSpawner spawner in Object.FindObjectsByType<MissionTrafficSpawner>(FindObjectsSortMode.None))
        {
            SerializedObject serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("carMaterialOverride").objectReferenceValue = carMaterial;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Repaired lvl 1: added visible ring meshes to {repairedRings} checkpoints, assigned a compatible textured car material, and set the ground dark green.");
    }

    [MenuItem("Tools/Missions/Build lvl 2 Delivery Scene")]
    public static void BuildLevel2Scene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !System.IO.File.Exists(ScenePath)) return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Level 1 Mission Controller" || root.name == "Lvl1 Mission Canvas" || root.name == "EventSystem")
                Object.DestroyImmediate(root);
        }

        GameObject city = GameObject.Find(CityRootName);
        if (city == null) throw new System.InvalidOperationException("The authored POLYGON city root was not found.");
        List<GameObject> oldCheckpoints = new List<GameObject>();
        foreach (Transform item in city.GetComponentsInChildren<Transform>(true))
            if (item.name.StartsWith("Checkpoint ", System.StringComparison.Ordinal)) oldCheckpoints.Add(item.gameObject);
        foreach (GameObject checkpoint in oldCheckpoints) Object.DestroyImmediate(checkpoint);

        GameObject drone = GameObject.Find("Player Drone");
        if (drone == null) throw new System.InvalidOperationException("The drone prefab instance was not found in lvl 1.");
        drone.transform.position = new Vector3(0f, 12f, 36f);
        drone.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        const string boxPath = "Assets/Game Assets/Aegis77/Cardboard Boxes/Prefabs/Box 1.prefab";
        GameObject boxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(boxPath);
        if (boxPrefab == null) throw new System.InvalidOperationException("Box 1 prefab was not found.");
        GameObject firstBox = PrefabUtility.InstantiatePrefab(boxPrefab, scene) as GameObject;
        firstBox.name = "Delivery Package 1";
        firstBox.transform.SetParent(drone.transform, false);
        firstBox.transform.localScale = Vector3.one * 0.42f;
        firstBox.transform.localPosition = new Vector3(0f, -1.05f, 0f);
        MissionDeliveryPackage packageComponent = firstBox.AddComponent<MissionDeliveryPackage>();

        Mesh targetMesh = GetOrCreateDeliveryRingMesh();
        Material targetMaterial = GetOrCreateDeliveryRingMaterial();
        GameObject target = CreateDeliveryTarget(scene, city.transform, "Delivery Target 1 - Ground Circle",
            new Vector3(-28f, 0.12f, 66f), targetMesh, targetMaterial, true);
        GameObject nextTarget = CreateDeliveryTarget(scene, city.transform, "Delivery Target 2 - Ground Circle",
            new Vector3(28f, 0.12f, 66f), targetMesh, targetMaterial, false);

        BuildLevel2UI(scene, drone, packageComponent, boxPrefab, target.transform, nextTarget.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, Level2ScenePath);
        AddSceneToBuildSettings(Level2ScenePath);
        Debug.Log("Created editable lvl2 delivery mission with attached Box 1, a visible ground target, traffic, and pause/failure UI.");
    }

    [MenuItem("Tools/Missions/Move lvl2 Delivery Zones Near Houses")]
    public static void MoveLevel2DeliveryZonesNearHouses()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !System.IO.File.Exists(Level2ScenePath)) return;

        Scene scene = EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);
        Transform first = FindSceneTransform(scene, "Delivery Target 1 - Ground Circle");
        Transform second = FindSceneTransform(scene, "Delivery Target 2 - Ground Circle");
        if (first == null || second == null)
            throw new System.InvalidOperationException("Both delivery zones must exist in lvl2.");

        // Two nearby clear landing spots in front of the motel and neighboring city block.
        first.position = new Vector3(-28f, 0.12f, 66f);
        second.position = new Vector3(28f, 0.12f, 66f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Moved both lvl2 delivery rings onto clear ground beside the city houses.");
    }

    private static Transform FindSceneTransform(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform item in transforms)
                if (item.name == objectName) return item;
        }
        return null;
    }

    [MenuItem("Tools/Main Menu/Place Drone Artwork")]
    public static void PlaceMainMenuDroneArtwork()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !System.IO.File.Exists(MainMenuScenePath)) return;

        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        GameObject previous = GameObject.Find("Main Menu Drone Artwork");
        if (previous != null) Object.DestroyImmediate(previous);
        GameObject previousCamera = GameObject.Find("Main Menu Drone Artwork Camera");
        if (previousCamera != null) Object.DestroyImmediate(previousCamera);
        Transform menuRoot = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == "Menu");
        if (menuRoot == null) throw new System.InvalidOperationException("The MainMenu Canvas was not found.");
        Transform oldArt = menuRoot.Find("Drone Artwork");
        if (oldArt != null) Object.DestroyImmediate(oldArt.gameObject);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/drone.prefab");
        if (prefab == null) throw new System.InvalidOperationException("The drone prefab at Assets/drone.prefab was not found.");
        GameObject artwork = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        artwork.name = "Main Menu Drone Artwork";
        artwork.transform.position = Vector3.zero;
        artwork.transform.rotation = Quaternion.Euler(12f, 145f, -8f);
        artwork.transform.localScale = Vector3.one;

        foreach (MonoBehaviour behaviour in artwork.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (Camera camera in artwork.GetComponentsInChildren<Camera>(true))
            camera.enabled = false;
        foreach (AudioListener listener in artwork.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;
        foreach (Rigidbody body in artwork.GetComponentsInChildren<Rigidbody>(true))
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }
        foreach (Collider collider in artwork.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        const int artworkLayer = 31;
        foreach (Transform item in artwork.GetComponentsInChildren<Transform>(true))
            item.gameObject.layer = artworkLayer;
        foreach (Renderer renderer in artwork.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        Bounds bounds = new Bounds(artwork.transform.position, Vector3.one);
        Renderer[] renderers = artwork.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        }
        RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(MainMenuDroneRenderTexturePath);
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(1200, 1000, 24, RenderTextureFormat.ARGB32)
            {
                name = "MainMenuDrone",
                antiAliasing = 4,
                useMipMap = false,
                autoGenerateMips = false
            };
            AssetDatabase.CreateAsset(renderTexture, MainMenuDroneRenderTexturePath);
        }
        renderTexture.Release();
        renderTexture.Create();

        GameObject artCameraObject = new GameObject("Main Menu Drone Artwork Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(artCameraObject, scene);
        Camera artCamera = artCameraObject.GetComponent<Camera>();
        artCamera.transform.position = bounds.center + new Vector3(0f, bounds.extents.y * 0.35f, -Mathf.Max(8f, bounds.extents.magnitude * 2.5f));
        artCamera.transform.LookAt(bounds.center);
        artCamera.orthographic = true;
        artCamera.orthographicSize = Mathf.Max(2f, bounds.extents.y * 1.55f);
        artCamera.clearFlags = CameraClearFlags.SolidColor;
        artCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        artCamera.cullingMask = 1 << artworkLayer;
        artCamera.targetTexture = renderTexture;
        artCamera.allowHDR = true;
        artCamera.allowMSAA = true;
        artCamera.nearClipPlane = 0.1f;
        artCamera.farClipPlane = 100f;

        GameObject artImageObject = new GameObject("Drone Artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        artImageObject.layer = menuRoot.gameObject.layer;
        artImageObject.transform.SetParent(menuRoot, false);
        artImageObject.transform.SetSiblingIndex(Mathf.Min(1, menuRoot.childCount - 1));
        RectTransform artRect = artImageObject.GetComponent<RectTransform>();
        artRect.anchorMin = new Vector2(0.53f, 0.08f);
        artRect.anchorMax = new Vector2(0.98f, 0.92f);
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;
        RawImage artImage = artImageObject.GetComponent<RawImage>();
        artImage.texture = renderTexture;
        artImage.color = Color.white;
        artImage.raycastTarget = false;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Placed the drone artwork above the MainMenu background on the open right side.");
    }

    private static GameObject CreateDeliveryTarget(Scene scene, Transform city, string name, Vector3 position,
        Mesh mesh, Material material, bool active)
    {
        GameObject target = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        SceneManager.MoveGameObjectToScene(target, scene);
        target.transform.SetParent(city, true);
        target.transform.position = position;
        target.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        target.SetActive(active);
        return target;
    }

    private static void BuildLevel2UI(Scene scene, GameObject drone, MissionDeliveryPackage package, GameObject packagePrefab,
        Transform target, Transform nextTarget)
    {
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(eventSystem, scene);
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        GameObject canvasObject = new GameObject("Lvl2 Mission Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasObject.transform;

        TMP_Text progress = AddText("DeliveryProgress", root, "ДОСТАВКА  0 / 1", 23f, White, TextAlignmentOptions.Left);
        SetAnchors(progress.rectTransform, new Vector2(0.035f, 0.875f), new Vector2(0.38f, 0.925f));
        TMP_Text timer = AddText("DeliveryTimer", root, "ОСТАЛОСЬ  40 СЕК", 23f, Orange, TextAlignmentOptions.Center);
        SetAnchors(timer.rectTransform, new Vector2(0.40f, 0.92f), new Vector2(0.68f, 0.975f));
        TMP_Text instructions = AddText("DeliveryInstructions", root, "Подведите дрон к кольцу и нажмите K, чтобы сбросить посылку.", 18f, White, TextAlignmentOptions.Left);
        SetAnchors(instructions.rectTransform, new Vector2(0.035f, 0.81f), new Vector2(0.62f, 0.865f));
        Button pauseButton = AddButton("PauseButton", root, "Ⅱ  ПАУЗА", Orange, new Vector2(0.035f, 0.945f), new Vector2(0.18f, 0.99f), 17f);

        GameObject pausePanel = AddPanel("PausePanel", root, new Vector2(0.5f, 0.5f), new Vector2(520f, 400f), false);
        AddText("Title", pausePanel.transform, "ПАУЗА", 34f, White, TextAlignmentOptions.Center);
        PositionChild(pausePanel.transform.Find("Title") as RectTransform, new Vector2(0f, 115f), new Vector2(460f, 60f));
        Button resume = AddButton("ResumeButton", pausePanel.transform, "ПРОДОЛЖИТЬ", Blue, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 20f);
        resume.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 58f);
        resume.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 15f);
        Button pauseMenu = AddButton("MenuButton", pausePanel.transform, "В ГЛАВНОЕ МЕНЮ", Navy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 18f);
        pauseMenu.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 58f);
        pauseMenu.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -72f);

        GameObject failedPanel = AddPanel("FailedPanel", root, new Vector2(0.5f, 0.5f), new Vector2(560f, 390f), false);
        AddText("Title", failedPanel.transform, "ДОСТАВКА ПРОВАЛЕНА", 29f, Orange, TextAlignmentOptions.Center);
        PositionChild(failedPanel.transform.Find("Title") as RectTransform, new Vector2(0f, 110f), new Vector2(500f, 60f));
        AddText("Reason", failedPanel.transform, "Посылка упала вне отмеченной зоны.", 18f, Muted, TextAlignmentOptions.Center);
        PositionChild(failedPanel.transform.Find("Reason") as RectTransform, new Vector2(0f, 43f), new Vector2(470f, 56f));
        Button retry = AddButton("RetryButton", failedPanel.transform, "ПОВТОРИТЬ МИССИЮ", Blue, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 19f);
        retry.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 56f);
        retry.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -28f);
        Button failedMenu = AddButton("MenuButton", failedPanel.transform, "В ГЛАВНОЕ МЕНЮ", Navy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 18f);
        failedMenu.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 56f);
        failedMenu.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -102f);

        GameObject completedPanel = AddPanel("CompletedPanel", root, new Vector2(0.5f, 0.5f), new Vector2(560f, 350f), false);
        AddText("Title", completedPanel.transform, "ПОСЫЛКА ДОСТАВЛЕНА", 29f, Blue, TextAlignmentOptions.Center);
        PositionChild(completedPanel.transform.Find("Title") as RectTransform, new Vector2(0f, 85f), new Vector2(500f, 60f));
        AddText("Message", completedPanel.transform, "Зона доставки достигнута. Результат сохранён.", 18f, Muted, TextAlignmentOptions.Center);
        PositionChild(completedPanel.transform.Find("Message") as RectTransform, new Vector2(0f, 22f), new Vector2(470f, 56f));
        Button mapButton = AddButton("MapButton", completedPanel.transform, "К КАРТЕ МИССИЙ", Orange, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 19f);
        mapButton.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 58f);
        mapButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -58f);

        Lvl2MissionController mission = new GameObject("Level 2 Mission Controller").AddComponent<Lvl2MissionController>();
        SceneManager.MoveGameObjectToScene(mission.gameObject, scene);
        SerializedObject serialized = new SerializedObject(mission);
        serialized.FindProperty("drone").objectReferenceValue = drone.transform;
        serialized.FindProperty("firstPackage").objectReferenceValue = package;
        serialized.FindProperty("packagePrefab").objectReferenceValue = packagePrefab;
        serialized.FindProperty("deliveryTarget").objectReferenceValue = target;
        serialized.FindProperty("nextDeliveryTarget").objectReferenceValue = nextTarget;
        serialized.FindProperty("targetRadius").floatValue = 4f;
        serialized.FindProperty("progressLabel").objectReferenceValue = progress;
        serialized.FindProperty("timerLabel").objectReferenceValue = timer;
        serialized.FindProperty("instructionsLabel").objectReferenceValue = instructions;
        serialized.FindProperty("pausePanel").objectReferenceValue = pausePanel;
        serialized.FindProperty("failedPanel").objectReferenceValue = failedPanel;
        serialized.FindProperty("completedPanel").objectReferenceValue = completedPanel;
        serialized.FindProperty("pauseButton").objectReferenceValue = pauseButton;
        serialized.FindProperty("resumeButton").objectReferenceValue = resume;
        serialized.FindProperty("restartButton").objectReferenceValue = retry;
        serialized.FindProperty("pauseMenuButton").objectReferenceValue = pauseMenu;
        serialized.FindProperty("failedMenuButton").objectReferenceValue = failedMenu;
        serialized.FindProperty("completedMapButton").objectReferenceValue = mapButton;
        serialized.FindProperty("startMissionClip").objectReferenceValue = LoadMissionSound("start_mission.mp3");
        serialized.FindProperty("packageDropClip").objectReferenceValue = LoadMissionSound("post.mp3");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Mesh GetOrCreateDeliveryRingMesh()
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(DeliveryRingMeshPath);
        bool createAsset = existing == null;
        const int around = 96, tubeSegments = 12;
        const float radius = 4f, tube = 0.10f;
        Vector3[] vertices = new Vector3[(around + 1) * (tubeSegments + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[around * tubeSegments * 6];
        for (int a = 0; a <= around; a++)
        {
            float theta = a * Mathf.PI * 2f / around;
            for (int b = 0; b <= tubeSegments; b++)
            {
                float phi = b * Mathf.PI * 2f / tubeSegments;
                int index = a * (tubeSegments + 1) + b;
                float ringRadius = radius + Mathf.Cos(phi) * tube;
                vertices[index] = new Vector3(Mathf.Cos(theta) * ringRadius, Mathf.Sin(phi) * tube, Mathf.Sin(theta) * ringRadius);
                uv[index] = new Vector2((float)a / around, (float)b / tubeSegments);
            }
        }
        int t = 0;
        for (int a = 0; a < around; a++)
            for (int b = 0; b < tubeSegments; b++)
            {
                int v = a * (tubeSegments + 1) + b;
                int next = v + tubeSegments + 1;
                triangles[t++] = v; triangles[t++] = v + 1; triangles[t++] = next;
                triangles[t++] = v + 1; triangles[t++] = next + 1; triangles[t++] = next;
            }
        Mesh mesh = existing != null ? existing : new Mesh { name = "lvl2 Ground Delivery Ring" };
        mesh.Clear();
        mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (createAsset) AssetDatabase.CreateAsset(mesh, DeliveryRingMeshPath);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static AudioClip LoadMissionSound(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sounds/" + fileName);
    }

    private static Material GetOrCreateDeliveryRingMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DeliveryRingMaterialPath);
        Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader) { name = "lvl2 Delivery Target Cyan" };
            AssetDatabase.CreateAsset(material, DeliveryRingMaterialPath);
        }
        else material.shader = shader;
        Color cyan = new Color(0.08f, 0.80f, 1f, 1f);
        if (material.HasProperty("_Color")) material.SetColor("_Color", cyan);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", cyan);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static RectTransform FindSceneRect(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = root.transform.Find(objectName);
            if (found != null) return found as RectTransform;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child as RectTransform;
        }
        return null;
    }

    private static void EnsureParkVegetation(Scene scene)
    {
        GameObject city = GameObject.Find(CityRootName);
        if (city == null || city.scene != scene || city.transform.Find("Park Vegetation") != null)
            return;
        GameObject vegetation = new GameObject("Park Vegetation");
        vegetation.transform.SetParent(city.transform, false);
        string props = "Assets/POLYGON city pack/Prefabs/Props/";
        Vector3[] treePositions =
        {
            new Vector3(-43, 0, -32), new Vector3(-43, 0, -16), new Vector3(-43, 0, 0), new Vector3(-43, 0, 16), new Vector3(-43, 0, 32),
            new Vector3(43, 0, -32), new Vector3(43, 0, -16), new Vector3(43, 0, 0), new Vector3(43, 0, 16), new Vector3(43, 0, 32),
            new Vector3(-30, 0, 43), new Vector3(-15, 0, 43), new Vector3(0, 0, 43), new Vector3(15, 0, 43), new Vector3(30, 0, 43),
            new Vector3(-30, 0, -43), new Vector3(-15, 0, -43), new Vector3(0, 0, -43), new Vector3(15, 0, -43), new Vector3(30, 0, -43)
        };
        for (int i = 0; i < treePositions.Length; i++)
            PlaceProp(props + "Tree prefab.prefab", vegetation.transform, treePositions[i], (i * 37) % 360);

        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI * 2f / 12f;
            Vector3 position = new Vector3(Mathf.Cos(angle) * 35f, 0f, Mathf.Sin(angle) * 35f);
            PlaceProp(props + "Bush 2 prefab.prefab", vegetation.transform, position, i * 29f);
        }
        EditorUtility.SetDirty(vegetation);
    }

    private static void BuildScene()
    {
        Scene previousActive = SceneManager.GetActiveScene();
        bool hasSavedSceneToPreserve = previousActive.IsValid() && !string.IsNullOrEmpty(previousActive.path);
        NewSceneMode mode = hasSavedSceneToPreserve ? NewSceneMode.Additive : NewSceneMode.Single;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
        scene.name = "lvl 1";
        GameObject cityRoot = new GameObject(CityRootName);
        SceneManager.MoveGameObjectToScene(cityRoot, scene);

        BuildLighting(scene);
        BuildGroundAndCity(scene, cityRoot.transform);
        GameObject drone = BuildDrone(scene);
        Transform[] checkpoints = BuildCheckpoints(scene, cityRoot.transform);
        MissionTrafficSpawner traffic = BuildTraffic(scene, cityRoot.transform);
        BuildMissionUI(scene, drone, checkpoints, traffic);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        if (hasSavedSceneToPreserve && previousActive.IsValid() && previousActive.isLoaded)
            EditorSceneManager.SetActiveScene(previousActive);

        Debug.Log("Created editable lvl 1 scene with placed POLYGON city prefabs, a drone, six checkpoints, traffic, and mission UI.");
    }

    private static void BuildLighting(Scene scene)
    {
        GameObject lightObject = new GameObject("Sun", typeof(Light));
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        lightObject.transform.rotation = Quaternion.Euler(50f, -32f, 0f);
        Light sun = lightObject.GetComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.91f, 0.78f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.56f, 0.70f, 0.77f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 125f;
        RenderSettings.fogEndDistance = 280f;
    }

    private static void BuildGroundAndCity(Scene scene, Transform root)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Training district ground";
        ground.transform.SetParent(root, false);
        ground.transform.position = new Vector3(0f, -0.04f, 0f);
        ground.transform.localScale = new Vector3(22f, 1f, 22f);
        ground.GetComponent<Renderer>().sharedMaterial = GetOrCreateGroundMaterial();
        ground.AddComponent<MissionObstacle>();

        string road = "Assets/POLYGON city pack/Prefabs/Floor/Street 4 16M prefab.prefab";
        // Concrete prefab placements form a continuous square boulevard around the training park.
        float[] marks = { -40f, -24f, -8f, 8f, 24f, 40f };
        foreach (float mark in marks)
        {
            PlacePrefab(road, root, new Vector3(mark, 0f, -52f), 90f, Vector3.one, false);
            PlacePrefab(road, root, new Vector3(mark, 0f, 52f), 90f, Vector3.one, false);
            PlacePrefab(road, root, new Vector3(-52f, 0f, mark), 0f, Vector3.one, false);
            PlacePrefab(road, root, new Vector3(52f, 0f, mark), 0f, Vector3.one, false);
        }

        // Hand-placed city blocks around a clear central flight park.
        PlaceBuilding("Buildings/Bank_prefab.prefab", root, -77f, 0f, 77f, 180f, 1f);
        PlaceBuilding("Buildings/Motel_prefab.prefab", root, -28f, 0f, 80f, 180f, 1f);
        PlaceBuilding("Buildings/Shop_A_prefab.prefab", root, 28f, 0f, 80f, 180f, 1f);
        PlaceBuilding("Buildings/Building_A_prefab.prefab", root, 77f, 0f, 77f, 180f, 1f);
        PlaceBuilding("Buildings/Hospital_prefab.prefab", root, 82f, 0f, 26f, 270f, 1f);
        PlaceBuilding("Buildings/Building_D_prefab.prefab", root, 81f, 0f, -22f, 270f, 1f);
        PlaceBuilding("Buildings/Police_station_prefab.prefab", root, 77f, 0f, -77f, 0f, 1f);
        PlaceBuilding("Buildings/Supermaket_prefab.prefab", root, 27f, 0f, -81f, 0f, 1f);
        PlaceBuilding("Buildings/Car_repair_prefab.prefab", root, -28f, 0f, -80f, 0f, 1f);
        PlaceBuilding("Buildings/Fire_department_prefab.prefab", root, -77f, 0f, -77f, 0f, 1f);
        PlaceBuilding("Buildings/Building_B1_prefab.prefab", root, -82f, 0f, -23f, 90f, 1f);
        PlaceBuilding("Buildings/Building_I_2_Prefab.prefab", root, -82f, 0f, 26f, 90f, 1f);
        PlaceBuilding("Buildings/Building_K_prefab.prefab", root, -78f, 0f, 54f, 180f, 1f);
        PlaceBuilding("Buildings/Building_R_Prefab.prefab", root, 78f, 0f, 53f, 180f, 1f);

        string props = "Assets/POLYGON city pack/Prefabs/Props/";
        PlaceProp(props + "Bench 2 prefab.prefab", root, new Vector3(-17f, 0f, 0f), 90f);
        PlaceProp(props + "Bench 2 prefab.prefab", root, new Vector3(17f, 0f, 0f), 270f);
        PlaceProp(props + "Pot_tree prefab.prefab", root, new Vector3(-25f, 0f, -15f), 0f);
        PlaceProp(props + "Pot_tree prefab.prefab", root, new Vector3(25f, 0f, 15f), 0f);
        PlaceProp(props + "Bush 2 prefab.prefab", root, new Vector3(-27f, 0f, 12f), 0f);
        PlaceProp(props + "Bush 2 prefab.prefab", root, new Vector3(27f, 0f, -12f), 0f);
        PlaceProp(props + "Bush 2 prefab.prefab", root, new Vector3(-31f, 0f, -4f), 0f);
        PlaceProp(props + "Bush 2 prefab.prefab", root, new Vector3(31f, 0f, 4f), 0f);
        PlaceProp(props + "Traffic light 1 Prefab.prefab", root, new Vector3(-48f, 0f, -48f), 0f);
        PlaceProp(props + "Traffic light 1 Prefab.prefab", root, new Vector3(48f, 0f, 48f), 180f);
        PlaceProp("Assets/POLYGON city pack/Prefabs/Lamps/Lamp_1_prefab.prefab", root, new Vector3(-54f, 0f, 25f), 0f);
        PlaceProp("Assets/POLYGON city pack/Prefabs/Lamps/Lamp_1_prefab.prefab", root, new Vector3(54f, 0f, -25f), 180f);
    }

    private static void PlaceBuilding(string relativePath, Transform root, float x, float y, float z, float yaw, float scale)
    {
        string path = "Assets/POLYGON city pack/Prefabs/" + relativePath;
        PlacePrefab(path, root, new Vector3(x, y, z), yaw, Vector3.one * scale, true);
    }

    private static void PlaceProp(string path, Transform root, Vector3 position, float yaw)
    {
        PlacePrefab(path, root, position, yaw, Vector3.one, true);
    }

    private static GameObject PlacePrefab(string path, Transform parent, Vector3 position, float yaw, Vector3 scale, bool obstacle)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"Level 1 prefab not found: {path}");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene) as GameObject;
        if (instance == null)
            return null;

        instance.transform.SetParent(parent, true);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * instance.transform.rotation;
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scale);
        if (obstacle)
            MarkObstacle(instance);
        return instance;
    }

    private static void MarkObstacle(GameObject instance)
    {
        if (instance.GetComponent<MissionObstacle>() == null)
            instance.AddComponent<MissionObstacle>();

        if (instance.GetComponentInChildren<Collider>(true) != null)
            return;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds localBounds = default;
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            Vector3 center = instance.transform.InverseTransformPoint(world.center);
            Vector3 ext = world.extents;
            Vector3[] corners =
            {
                world.center + new Vector3(-ext.x, -ext.y, -ext.z), world.center + new Vector3(-ext.x, -ext.y, ext.z),
                world.center + new Vector3(-ext.x, ext.y, -ext.z), world.center + new Vector3(-ext.x, ext.y, ext.z),
                world.center + new Vector3(ext.x, -ext.y, -ext.z), world.center + new Vector3(ext.x, -ext.y, ext.z),
                world.center + new Vector3(ext.x, ext.y, -ext.z), world.center + new Vector3(ext.x, ext.y, ext.z)
            };
            foreach (Vector3 corner in corners)
            {
                Vector3 local = instance.transform.InverseTransformPoint(corner);
                if (!initialized)
                {
                    localBounds = new Bounds(local, Vector3.zero);
                    initialized = true;
                }
                else localBounds.Encapsulate(local);
            }
        }

        if (initialized)
        {
            BoxCollider collider = instance.AddComponent<BoxCollider>();
            collider.center = localBounds.center;
            collider.size = localBounds.size;
        }
    }

    private static Material FindMaterial(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static GameObject BuildDrone(Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/drone.prefab");
        if (prefab == null)
            throw new System.InvalidOperationException("The root drone prefab at Assets/drone.prefab was not found.");

        GameObject drone = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        drone.name = "Player Drone";
        drone.transform.position = new Vector3(0f, 12f, 36f);
        drone.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        DroneKeyboardController controller = drone.GetComponent<DroneKeyboardController>();
        if (controller != null)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("fuelCapacity").floatValue = 99999f;
            serializedController.FindProperty("fuelConsumptionPerSecond").floatValue = 0.5f;
            serializedController.FindProperty("motorLoopClip").objectReferenceValue = LoadMissionSound("drone sound.mp3");
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        Camera camera = drone.GetComponentInChildren<Camera>(true);
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Drone Follow Camera", typeof(Camera), typeof(AudioListener), typeof(DroneCameraFollow));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.GetComponent<Camera>();
            SerializedObject follow = new SerializedObject(cameraObject.GetComponent<DroneCameraFollow>());
            follow.FindProperty("target").objectReferenceValue = drone.transform;
            follow.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            camera.tag = "MainCamera";
            DroneCameraFollow followComponent = camera.GetComponent<DroneCameraFollow>();
            if (followComponent == null)
                followComponent = camera.gameObject.AddComponent<DroneCameraFollow>();
            SerializedObject follow = new SerializedObject(followComponent);
            follow.FindProperty("target").objectReferenceValue = drone.transform;
            follow.ApplyModifiedPropertiesWithoutUndo();
        }

        return drone;
    }

    private static Transform[] BuildCheckpoints(Scene scene, Transform parent)
    {
        Vector3[] positions =
        {
            new Vector3(0f, 13f, 17f),
            new Vector3(13f, 14f, 14f),
            new Vector3(25f, 16f, 4f),
            new Vector3(19f, 15f, -10f),
            new Vector3(3f, 14f, -22f),
            new Vector3(-15f, 16f, -17f)
        };

        List<Transform> targets = new List<Transform>();
        Mesh ringMesh = GetOrCreateCheckpointMesh();
        Material ringMaterial = GetOrCreateCheckpointMaterial();
        GameObject ringPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/checkPointRing.prefab");
        if (ringPrefab == null)
            throw new System.InvalidOperationException("Assets/checkPointRing.prefab was not found.");

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject ring = PrefabUtility.InstantiatePrefab(ringPrefab, scene) as GameObject;
            ring.name = $"Checkpoint {i + 1:00}";
            ring.transform.SetParent(parent, true);
            ring.transform.position = positions[i];
            ring.transform.rotation = Quaternion.identity;
            ring.transform.localScale = Vector3.one;
            RemoveOldCheckpointAction(ring);
            FixCheckpointVisual(ring.transform, ringMesh, ringMaterial);
            targets.Add(ring.transform);
        }

        return targets.ToArray();
    }

    private static void RemoveOldCheckpointAction(GameObject checkpoint)
    {
        foreach (MonoBehaviour behaviour in checkpoint.GetComponents<MonoBehaviour>())
            if (behaviour != null && behaviour.GetType().Name == "CheckAction")
                Object.DestroyImmediate(behaviour, true);
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(checkpoint);
    }

    private static MissionTrafficSpawner BuildTraffic(Scene scene, Transform parent)
    {
        GameObject trafficObject = new GameObject("Traffic - One Way Loop");
        SceneManager.MoveGameObjectToScene(trafficObject, scene);
        trafficObject.transform.SetParent(parent, false);
        MissionTrafficSpawner spawner = trafficObject.AddComponent<MissionTrafficSpawner>();

        GameObject waypointsRoot = new GameObject("Traffic Route Points");
        SceneManager.MoveGameObjectToScene(waypointsRoot, scene);
        waypointsRoot.transform.SetParent(trafficObject.transform, false);
        Vector3[] positions =
        {
            new Vector3(-48f, 0.1f, -48f),
            new Vector3(48f, 0.1f, -48f),
            new Vector3(48f, 0.1f, 48f),
            new Vector3(-48f, 0.1f, 48f)
        };

        Transform[] nodes = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject node = new GameObject($"Route Point {i + 1:00}");
            node.transform.SetParent(waypointsRoot.transform, false);
            node.transform.position = positions[i];
            nodes[i] = node.transform;
        }

        string[] carPaths =
        {
            "Assets/Pack_FREE_Cars/Prefabs/Hatchback.prefab",
            "Assets/Pack_FREE_Cars/Prefabs/Taxi.prefab",
            "Assets/Pack_FREE_Cars/Prefabs/Van.prefab",
            "Assets/Pack_FREE_Cars/Prefabs/Police.prefab"
        };
        SerializedObject serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("carMaterialOverride").objectReferenceValue = GetOrCreateCarMaterial();
        SerializedProperty prefabs = serializedSpawner.FindProperty("carPrefabs");
        prefabs.arraySize = carPaths.Length;
        for (int i = 0; i < carPaths.Length; i++)
            prefabs.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(carPaths[i]);
        SerializedProperty route = serializedSpawner.FindProperty("routePoints");
        route.arraySize = nodes.Length;
        for (int i = 0; i < nodes.Length; i++)
            route.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
        serializedSpawner.FindProperty("vehicleCount").intValue = 4;
        serializedSpawner.FindProperty("vehicleScale").floatValue = 0.35f;
        serializedSpawner.FindProperty("cruiseSpeed").floatValue = 3.2f;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
        return spawner;
    }

    private static void BuildMissionUI(Scene scene, GameObject drone, Transform[] checkpoints, MissionTrafficSpawner traffic)
    {
        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
        eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        GameObject canvasObject = new GameObject("Lvl1 Mission Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        TMP_Text progress = AddText("MissionProgress", root, "УЧЕБНЫЙ МАРШРУТ   1 / 6", 24f, White, TextAlignmentOptions.Left);
        SetAnchors(progress.rectTransform, new Vector2(0.035f, 0.875f), new Vector2(0.44f, 0.925f));
        TMP_Text arrow = AddText("CheckpointArrow", root, "▲", 36f, new Color(0.24f, 0.85f, 1f), TextAlignmentOptions.Center);
        SetAnchors(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        arrow.rectTransform.sizeDelta = new Vector2(60f, 60f);

        Button pauseButton = AddButton("PauseButton", root, "Ⅱ  ПАУЗА", Orange, new Vector2(0.035f, 0.945f), new Vector2(0.18f, 0.99f), 17f);

        GameObject pausePanel = AddPanel("PausePanel", root, new Vector2(0.5f, 0.5f), new Vector2(520f, 400f), false);
        AddText("Title", pausePanel.transform, "ПАУЗА", 34f, White, TextAlignmentOptions.Center);
        PositionChild(pausePanel.transform.Find("Title") as RectTransform, new Vector2(0f, 115f), new Vector2(460f, 60f));
        Button resumeButton = AddButton("ResumeButton", pausePanel.transform, "ПРОДОЛЖИТЬ", Blue, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 20f);
        resumeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 58f);
        resumeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 15f);
        Button pauseMenuButton = AddButton("MenuButton", pausePanel.transform, "В ГЛАВНОЕ МЕНЮ", new Color(0.10f, 0.19f, 0.29f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 18f);
        pauseMenuButton.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 58f);
        pauseMenuButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -72f);

        GameObject failedPanel = AddPanel("FailedPanel", root, new Vector2(0.5f, 0.5f), new Vector2(560f, 390f), false);
        AddText("Title", failedPanel.transform, "ВЫЛЕТ ЗАВЕРШЁН", 30f, Orange, TextAlignmentOptions.Center);
        PositionChild(failedPanel.transform.Find("Title") as RectTransform, new Vector2(0f, 110f), new Vector2(500f, 60f));
        AddText("Reason", failedPanel.transform, "Дрон столкнулся с объектом. Попробуйте пройти маршрут ещё раз.", 18f, Muted, TextAlignmentOptions.Center);
        PositionChild(failedPanel.transform.Find("Reason") as RectTransform, new Vector2(0f, 43f), new Vector2(460f, 56f));
        Button retryButton = AddButton("RetryButton", failedPanel.transform, "ПОВТОРИТЬ ВЫЛЕТ", Blue, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 19f);
        retryButton.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 56f);
        retryButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -28f);
        Button failedMenuButton = AddButton("MenuButton", failedPanel.transform, "В ГЛАВНОЕ МЕНЮ", new Color(0.10f, 0.19f, 0.29f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 18f);
        failedMenuButton.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 56f);
        failedMenuButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -102f);

        GameObject completedPanel = AddPanel("CompletedPanel", root, new Vector2(0.5f, 0.5f), new Vector2(560f, 350f), false);
        AddText("Title", completedPanel.transform, "ТРЕНИРОВКА ПРОЙДЕНА", 29f, Blue, TextAlignmentOptions.Center);
        PositionChild(completedPanel.transform.Find("Title") as RectTransform, new Vector2(0f, 85f), new Vector2(500f, 60f));
        AddText("Message", completedPanel.transform, "Шесть контрольных точек пройдены. Новые миссии отмечены на карте.", 18f, Muted, TextAlignmentOptions.Center);
        PositionChild(completedPanel.transform.Find("Message") as RectTransform, new Vector2(0f, 22f), new Vector2(470f, 56f));
        Button completedMapButton = AddButton("MapButton", completedPanel.transform, "К КАРТЕ МИССИЙ", Orange, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 19f);
        completedMapButton.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 58f);
        completedMapButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -58f);

        Lvl1MissionController mission = new GameObject("Level 1 Mission Controller").AddComponent<Lvl1MissionController>();
        SceneManager.MoveGameObjectToScene(mission.gameObject, scene);
        SerializedObject serializedMission = new SerializedObject(mission);
        serializedMission.FindProperty("drone").objectReferenceValue = drone.transform;
        SerializedProperty checkpointRefs = serializedMission.FindProperty("checkpoints");
        checkpointRefs.arraySize = checkpoints.Length;
        SerializedProperty visualRefs = serializedMission.FindProperty("checkpointVisuals");
        visualRefs.arraySize = checkpoints.Length;
        for (int i = 0; i < checkpoints.Length; i++)
        {
            checkpointRefs.GetArrayElementAtIndex(i).objectReferenceValue = checkpoints[i];
            visualRefs.GetArrayElementAtIndex(i).objectReferenceValue = checkpoints[i].gameObject;
        }
        serializedMission.FindProperty("progressLabel").objectReferenceValue = progress;
        serializedMission.FindProperty("arrowLabel").objectReferenceValue = arrow;
        serializedMission.FindProperty("pausePanel").objectReferenceValue = pausePanel;
        serializedMission.FindProperty("failedPanel").objectReferenceValue = failedPanel;
        serializedMission.FindProperty("completedPanel").objectReferenceValue = completedPanel;
        serializedMission.FindProperty("pauseButton").objectReferenceValue = pauseButton;
        serializedMission.FindProperty("resumeButton").objectReferenceValue = resumeButton;
        serializedMission.FindProperty("restartButton").objectReferenceValue = retryButton;
        serializedMission.FindProperty("pauseMenuButton").objectReferenceValue = pauseMenuButton;
        serializedMission.FindProperty("failedMenuButton").objectReferenceValue = failedMenuButton;
        serializedMission.FindProperty("completedMapButton").objectReferenceValue = completedMapButton;
        serializedMission.FindProperty("startMissionClip").objectReferenceValue = LoadMissionSound("start_mission.mp3");
        serializedMission.FindProperty("checkpointClip").objectReferenceValue = LoadMissionSound("checkpoint.mp3");
        serializedMission.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject AddPanel(string name, Transform parent, Vector2 anchor, Vector2 size, bool active)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = size;
        Image image = panel.GetComponent<Image>();
        image.color = PanelColor;
        panel.SetActive(active);
        return panel;
    }

    private static Button AddButton(string name, Transform parent, string label, Color color, Vector2 anchorMin, Vector2 anchorMax, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = color;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.14f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        go.AddComponent<ButtonHoverEffect>();
        TMP_Text text = AddText("Label", rect, label, fontSize, White, TextAlignmentOptions.Center);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(8f, 2f);
        text.rectTransform.offsetMax = new Vector2(-8f, -2f);
        return button;
    }

    private static TMP_Text AddText(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(640f, 50f);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        return text;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void PositionChild(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(scene => scene.path == scenePath))
            return;

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void FixCheckpointVisual(Transform checkpoint, Mesh ringMesh, Material ringMaterial)
    {
        checkpoint.localScale = Vector3.one;
        checkpoint.rotation = Quaternion.identity;

        MeshRenderer originalRenderer = checkpoint.GetComponent<MeshRenderer>();
        if (originalRenderer != null)
            originalRenderer.enabled = false;
        Collider originalCollider = checkpoint.GetComponent<Collider>();
        if (originalCollider != null)
            originalCollider.enabled = false;

        Transform visibleRing = checkpoint.Find("Visible Ring");
        if (visibleRing == null)
        {
            GameObject visual = new GameObject("Visible Ring", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(checkpoint, false);
            visibleRing = visual.transform;
        }

        visibleRing.localPosition = Vector3.zero;
        visibleRing.localRotation = Quaternion.identity;
        visibleRing.localScale = Vector3.one;
        visibleRing.GetComponent<MeshFilter>().sharedMesh = ringMesh;
        MeshRenderer renderer = visibleRing.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = ringMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        EditorUtility.SetDirty(checkpoint.gameObject);
    }

    private static Mesh GetOrCreateCheckpointMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CheckpointMeshPath);
        if (mesh != null)
            return mesh;

        const int majorSegments = 96;
        const int tubeSegments = 12;
        const float radius = 3.25f;
        const float tubeRadius = 0.14f;
        Vector3[] vertices = new Vector3[(majorSegments + 1) * (tubeSegments + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[majorSegments * tubeSegments * 6];

        for (int major = 0; major <= majorSegments; major++)
        {
            float theta = major * Mathf.PI * 2f / majorSegments;
            Vector3 radial = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0f);
            for (int tube = 0; tube <= tubeSegments; tube++)
            {
                float phi = tube * Mathf.PI * 2f / tubeSegments;
                int index = major * (tubeSegments + 1) + tube;
                vertices[index] = radial * (radius + Mathf.Cos(phi) * tubeRadius)
                                  + Vector3.forward * (Mathf.Sin(phi) * tubeRadius);
                uv[index] = new Vector2((float)major / majorSegments, (float)tube / tubeSegments);
            }
        }

        int triangle = 0;
        for (int major = 0; major < majorSegments; major++)
        {
            for (int tube = 0; tube < tubeSegments; tube++)
            {
                int a = major * (tubeSegments + 1) + tube;
                int b = a + tubeSegments + 1;
                triangles[triangle++] = a;
                triangles[triangle++] = b;
                triangles[triangle++] = a + 1;
                triangles[triangle++] = a + 1;
                triangles[triangle++] = b;
                triangles[triangle++] = b + 1;
            }
        }

        mesh = new Mesh { name = "lvl 1 Visible Checkpoint Ring" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, CheckpointMeshPath);
        return mesh;
    }

    private static Material GetOrCreateCheckpointMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CheckpointMaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        material = new Material(shader) { name = "lvl 1 Cyan Checkpoint Glow" };
        Color cyan = new Color(0.045f, 0.72f, 0.98f, 1f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", cyan);
        if (material.HasProperty("_Color")) material.SetColor("_Color", cyan);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.03f, 0.7f, 1f, 1f) * 2.2f);
        }
        AssetDatabase.CreateAsset(material, CheckpointMaterialPath);
        return material;
    }

    private static Material GetOrCreateCarMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CarMaterialPath);
        Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pack_FREE_Cars/Materials/ColorPalette.mat");
        Texture palette = AssetDatabase.LoadAssetAtPath<Texture>(TexturedPalettePath);
        if (palette == null)
            palette = source != null ? source.GetTexture("_BaseMap") ?? source.GetTexture("_MainTex")
                : AssetDatabase.LoadAssetAtPath<Texture>("Assets/Pack_FREE_Cars/Materials/Textures/ColorPalette.png");
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        if (material == null)
            material = new Material(shader) { name = "lvl 1 Car Palette" };
        else
            material.shader = shader;
        if (palette != null)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", palette);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", palette);
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.08f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.3f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.3f);
        if (AssetDatabase.LoadAssetAtPath<Material>(CarMaterialPath) == null)
            AssetDatabase.CreateAsset(material, CarMaterialPath);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateGroundMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        if (material == null)
        {
            material = new Material(shader) { name = "lvl 1 Dark Green Ground" };
            AssetDatabase.CreateAsset(material, GroundMaterialPath);
        }
        else material.shader = shader;
        Color green = new Color(0.025f, 0.25f, 0.045f, 1f);
        if (material.HasProperty("_Color")) material.SetColor("_Color", green);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", green);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.22f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.22f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
