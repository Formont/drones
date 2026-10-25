using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Restores the missing project URP asset so URP drone, fire, and water shaders render.</summary>
[InitializeOnLoad]
public static class RepairUniversalRenderPipeline
{
    private const string RenderingFolder = "Assets/Settings/Rendering";
    private const string RendererPath = RenderingFolder + "/Drone Universal Renderer.asset";
    private const string PipelinePath = RenderingFolder + "/Drone Universal Render Pipeline.asset";
    private const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
    private const string MaterialsConvertedKey = "DroneProject.URPMaterialsConverted.v1";

    static RepairUniversalRenderPipeline()
    {
        EditorApplication.delayCall += RepairIfNeeded;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += RepairIfNeeded;
    }

    [MenuItem("Tools/Rendering/Repair Missing URP Pipeline")]
    public static void RepairIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            EnsureFolder("Assets/Settings", "Rendering");

            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                var rendererSettings = new SerializedObject(renderer);
                SerializedProperty postProcess = rendererSettings.FindProperty("postProcessData");
                if (postProcess != null)
                {
                    postProcess.objectReferenceValue = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                    rendererSettings.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(renderer);
            }

            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            EditorUtility.SetDirty(pipeline);
        }

        bool changed = false;
        if (GraphicsSettings.defaultRenderPipeline != pipeline)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            changed = true;
        }

        int currentQuality = QualitySettings.GetQualityLevel();
        string[] qualityNames = QualitySettings.names;
        for (int i = 0; i < qualityNames.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            if (QualitySettings.renderPipeline == pipeline) continue;
            QualitySettings.renderPipeline = pipeline;
            changed = true;
        }
        if (currentQuality >= 0 && currentQuality < qualityNames.Length)
            QualitySettings.SetQualityLevel(currentQuality, false);

        if (changed || !AssetDatabase.Contains(pipeline))
        {
            AssetDatabase.SaveAssets();
            Debug.Log("Restored the missing URP pipeline and assigned it to the project and all quality levels. URP materials can now render.");
        }

        // Activating URP makes Built-in Standard materials render pink. Use Unity's
        // own pipeline converter so texture slots, colors, and surface settings migrate.
        if (!EditorPrefs.GetBool(MaterialsConvertedKey, false))
        {
            var upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset));
            if (upgraders != null && upgraders.Count > 0)
            {
                MaterialUpgrader.UpgradeProjectFolder(upgraders, "Convert Built-in Materials to URP");
                AssetDatabase.SaveAssets();
                EditorPrefs.SetBool(MaterialsConvertedKey, true);
                Debug.Log("Converted Built-in materials to URP with Unity's material upgrader.");
            }
            else
            {
                Debug.LogError("No URP material converters were found. Built-in materials could not be converted.");
            }
        }
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
