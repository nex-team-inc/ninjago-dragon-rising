#nullable enable

using System;
using System.Collections.Generic;
using Nex.Util;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Nex
{
    public class SfxManager : Singleton<SfxManager>
    {
        public enum SoundEffect
        {
            None = -1,
            GenericEnter = 0,
            GenericExit = 1,
        }

        [SerializeField] AudioSource audioSource = null!;

        [Serializable]
        class SoundEffectSpec
        {
            [SerializeField] public AudioClip[] clips = null!;
            int index;

            public void Initialize()
            {
                index = Random.Range(0, clips.Length);
            }

            public AudioClip PickSingleClip()
            {
                if (clips.Length == 0) return null!;
                var ret = clips[index];
                var n = clips.Length;
                if (n > 1)
                {
                    index = (index + Random.Range(1, n)) % n;
                }

                return ret;
            }
        }

        [SerializeField] EnumDictionary<SoundEffect, SoundEffectSpec> soundEffectDict = null!;

        protected override SfxManager GetThis() => this;

        protected override void Awake()
        {
            base.Awake();
            foreach (var pair in soundEffectDict)
            {
                pair.Value.Initialize();
            }
        }

        void LateUpdate()
        {
            currentlyStartedClips.Clear();
        }

        public void PlaySoundEffect(SoundEffect effect, AudioSource? customAudioSource = null)
        {
            if (effect == SoundEffect.None) return; // Don't play anything for None.
            var audioClip = soundEffectDict[effect].PickSingleClip();
            PlayAudioClip(audioClip, customAudioSource);
        }

        readonly HashSet<int> currentlyStartedClips = new();

        // MARK - Helper
        public void PlayAudioClip(AudioClip clip, AudioSource? customAudioSource)
        {
            if (clip == null) return;
            var clipId = clip.GetInstanceID();
            if (!currentlyStartedClips.Add(clipId)) return;
            (customAudioSource != null ? customAudioSource : audioSource).PlayOneShot(clip);
        }
    }
}
