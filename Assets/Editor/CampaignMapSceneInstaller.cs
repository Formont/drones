using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class CampaignMapSceneInstaller
{
    private const string ScenePath = "Assets/Scenes/CampaignMap.unity";
    private static bool installing;

    static CampaignMapSceneInstaller()
    {
        EditorApplication.delayCall += EnsureEditorLayout;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Campaign Map/Build Scene UI")]
    private static void BuildSceneUI()
    {
        EnsureEditorLayout();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += EnsureEditorLayout;
    }

    private static void EnsureEditorLayout()
    {
        string absoluteScenePath = Path.Combine(Application.dataPath, "Scenes", "CampaignMap.unity");
        if (installing || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(absoluteScenePath)) return;

        Scene scene = default;
        Scene previousActive = SceneManager.GetActiveScene();
        bool openedForInstall = false;
        installing = true;
        try
        {
            scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                openedForInstall = true;
            }

            CampaignMapScene builder = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                builder = root.GetComponent<CampaignMapScene>();
                if (builder != null) break;
            }
            if (builder == null) return;

            EditorSceneManager.SetActiveScene(scene);
            bool changed = builder.BuildEditorLayout();
            changed |= builder.ApplyMissionContentInEditor();
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (previousActive.IsValid() && previousActive.isLoaded)
                EditorSceneManager.SetActiveScene(previousActive);
            if (openedForInstall && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
            installing = false;
        }
    }
}
