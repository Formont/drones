using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameDifficulty
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}

public enum ProjectAudioType
{
    SoundEffect,
    Music
}

/// <summary>Persistent project-wide settings, saved between game sessions.</summary>
public sealed class GameSettingsService : MonoBehaviour
{
    private const string SoundKey = "Drones.SoundEnabled";
    private const string MusicKey = "Drones.MusicEnabled";
    private const string DifficultyKey = "Drones.Difficulty";

    private static GameSettingsService instance;
    private readonly Dictionary<AudioSource, bool> authoredMuteStates = new Dictionary<AudioSource, bool>();

    public static event Action SettingsChanged;
    public static event Action<GameDifficulty> DifficultyChanged;

    public static bool SoundEnabled => PlayerPrefs.GetInt(SoundKey, 1) != 0;
    public static bool MusicEnabled => PlayerPrefs.GetInt(MusicKey, 1) != 0;
    public static GameDifficulty Difficulty => (GameDifficulty)Mathf.Clamp(
        PlayerPrefs.GetInt(DifficultyKey, (int)GameDifficulty.Easy),
        (int)GameDifficulty.Easy,
        (int)GameDifficulty.Hard);

    public static string DifficultyLabel => GetDifficultyLabel(Difficulty);

    public static string GetDifficultyLabel(GameDifficulty difficulty) => difficulty switch
    {
        GameDifficulty.Easy => "Начальная",
        GameDifficulty.Normal => "Нормальная",
        GameDifficulty.Hard => "Сложная",
        _ => "Начальная"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        new GameObject("GameSettingsService").AddComponent<GameSettingsService>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyToLoadedAudioSources();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        // Some vehicle and package prefabs create AudioSources after sceneLoaded.
        // Reapply disabled channels so late or one-shot sources cannot bypass Settings.
        if (!SoundEnabled || !MusicEnabled)
            ApplyToLoadedAudioSources();
    }

    public static void SetSoundEnabled(bool enabled)
    {
        if (SoundEnabled == enabled) return;
        PlayerPrefs.SetInt(SoundKey, enabled ? 1 : 0);
        SaveAndApply();
    }

    public static void SetMusicEnabled(bool enabled)
    {
        if (MusicEnabled == enabled) return;
        PlayerPrefs.SetInt(MusicKey, enabled ? 1 : 0);
        SaveAndApply();
    }

    public static void SetDifficulty(int value)
    {
        GameDifficulty selected = (GameDifficulty)Mathf.Clamp(value, 0, 2);
        if (Difficulty == selected) return;
        PlayerPrefs.SetInt(DifficultyKey, (int)selected);
        PlayerPrefs.Save();
        DifficultyChanged?.Invoke(selected);
        SettingsChanged?.Invoke();
    }

    public static bool IsAudioEnabled(ProjectAudioType type)
    {
        return type == ProjectAudioType.Music ? MusicEnabled : SoundEnabled;
    }

    /// <summary>Applies the matching project toggle to an AudioSource.</summary>
    public static void ApplyToAudioSource(AudioSource source)
    {
        if (source == null) return;
        if (instance == null)
        {
            source.mute = !SoundEnabled;
            return;
        }

        if (!instance.authoredMuteStates.ContainsKey(source))
            instance.authoredMuteStates.Add(source, source.mute);

        ProjectAudioChannel channel = source.GetComponent<ProjectAudioChannel>();
        bool enabled = channel == null ? SoundEnabled : IsAudioEnabled(channel.AudioType);
        source.mute = instance.authoredMuteStates[source] || !enabled;
    }

    private static void SaveAndApply()
    {
        PlayerPrefs.Save();
        instance?.ApplyToLoadedAudioSources();
        SettingsChanged?.Invoke();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                ApplyToAudioSource(source);
        }

        RemoveDestroyedAudioSources();
    }

    private void ApplyToLoadedAudioSources()
    {
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ApplyToAudioSource(source);
        RemoveDestroyedAudioSources();
    }

    private void RemoveDestroyedAudioSources()
    {
        var dead = new List<AudioSource>();
        foreach (AudioSource source in authoredMuteStates.Keys)
            if (source == null) dead.Add(source);
        foreach (AudioSource source in dead) authoredMuteStates.Remove(source);
    }
}
