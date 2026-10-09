#nullable enable

using UnityEngine;

namespace Nex
{
    public static class AudioMixerUtils
    {
        public static float GetMixerVolumeValueFrom01Value(float value)
        {
            return Mathf.Log10(Mathf.Max(value, 0.001f)) * AudioMixerConstants.volumeMultiplier;
        }
    }
}
