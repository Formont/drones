using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Connects the saved settings page controls to project settings.</summary>
public sealed class SettingsSceneController : MonoBehaviour
{
    [SerializeField] private Toggle soundToggle;
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private TMP_Dropdown difficultyDropdown;

    private void OnEnable()
    {
        soundToggle.SetIsOnWithoutNotify(GameSettingsService.SoundEnabled);
        musicToggle.SetIsOnWithoutNotify(GameSettingsService.MusicEnabled);
        difficultyDropdown.SetValueWithoutNotify((int)GameSettingsService.Difficulty);
        difficultyDropdown.RefreshShownValue();

        soundToggle.onValueChanged.AddListener(GameSettingsService.SetSoundEnabled);
        musicToggle.onValueChanged.AddListener(GameSettingsService.SetMusicEnabled);
        difficultyDropdown.onValueChanged.AddListener(GameSettingsService.SetDifficulty);
    }

    private void OnDisable()
    {
        soundToggle.onValueChanged.RemoveListener(GameSettingsService.SetSoundEnabled);
        musicToggle.onValueChanged.RemoveListener(GameSettingsService.SetMusicEnabled);
        difficultyDropdown.onValueChanged.RemoveListener(GameSettingsService.SetDifficulty);
    }
}
