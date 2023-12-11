#nullable enable

using Nex.Util;
using UnityEngine;

namespace Nex
{
    public class SfxManager : Singleton<SfxManager>
    {
        public enum SoundEffect
        {
            GenericEnter,
            GenericExit,
        }

        [SerializeField] AudioSource audioSource = null!;
        [SerializeField] EnumDictionary<SoundEffect, AudioClip> soundEffectDict = null!;

        protected override SfxManager GetThis() => this;

        public void PlaySoundEffect(SoundEffect effect)
        {
            var audioClip = soundEffectDict[effect];
            PlayAudioClip(audioClip);
        }

        // MARK - Helper
        public void PlayAudioClip(AudioClip clip)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
