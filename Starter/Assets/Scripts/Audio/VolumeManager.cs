#nullable enable

using UnityEngine;
using UnityEngine.Audio;

namespace Nex
{
    public class VolumeManager  : Singleton<VolumeManager>
    {
        [SerializeField] AudioMixer audioMixer = null!;

        protected override VolumeManager GetThis()
        {
            return this;
        }

        public void SetMasterVolume(float value)
        {
            audioMixer.SetFloat(AudioMixerConstants.masterVolumeParameterName, AudioMixerUtils.GetMixerVolumeValueFrom01Value(value));
        }

        public void SetMusicVolume(float value)
        {
            audioMixer.SetFloat(AudioMixerConstants.musicVolumeParameterName, AudioMixerUtils.GetMixerVolumeValueFrom01Value(value));
        }

        public void SetSfxVolume(float value)
        {
            audioMixer.SetFloat(AudioMixerConstants.sfxVolumeParameterName, AudioMixerUtils.GetMixerVolumeValueFrom01Value(value));
        }
    }
}
