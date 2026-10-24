using UnityEngine;

namespace DroneGameplay
{
    public class CheckAction : MonoBehaviour
    {
        [SerializeField] private AudioClip disappeareSound;
        [SerializeField] private Transform myPosition;

        public void DieAction(AudioSource source)
        {
            if (source != null && disappeareSound != null && GameSettingsService.SoundEnabled)
            {
                GameSettingsService.ApplyToAudioSource(source);
                if (!source.mute)
                    source.PlayOneShot(disappeareSound);
            }

            Transform target = myPosition != null ? myPosition : transform;
            target.gameObject.SetActive(false);
        }
    }
}
