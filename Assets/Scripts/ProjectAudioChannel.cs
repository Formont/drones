using UnityEngine;

/// <summary>Marks an AudioSource as project music or a sound effect.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class ProjectAudioChannel : MonoBehaviour
{
    [SerializeField] private ProjectAudioType audioType = ProjectAudioType.SoundEffect;

    public ProjectAudioType AudioType => audioType;

    private void OnEnable()
    {
        GameSettingsService.ApplyToAudioSource(GetComponent<AudioSource>());
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            GameSettingsService.ApplyToAudioSource(GetComponent<AudioSource>());
    }
}
