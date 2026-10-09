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
        // Values are persisted in the prefab EnumDictionary: explicit ints, append only, keep ascending order.
        public enum SoundEffect
        {
            None = -1,
            GenericEnter = 0,
            GenericExit = 1,

            // UI
            UiMove = 100,
            UiSelect = 101,
            UiBack = 102,
            UiPause = 103,
            UiResume = 104,
            CountdownTick = 105,

            // Balls
            CueStrike = 200,
            BallLaunch = 201,
            BallWallBounce = 202,
            BallHitSoft = 203,
            BallHitMid = 204,
            BallHitHard = 205,
            CritHit = 206,
            Blocked = 207,
            BallReturn = 208,
            PowerShot = 209,

            // Enemies
            EnemyDeath = 300,
            BossHit = 301,
            BossDeath = 302,
            EnemyStep = 303,
            EnemyAttack = 304,
            EnemyCast = 305,
            CrateBreak = 306,

            // Abilities
            Explosion = 400,
            Freeze = 401,
            Burn = 402,
            Lightning = 403,
            Poison = 404,
            Heal = 405,
            Split = 406,
            Portal = 407,

            // Pickups & player
            PickupBall = 500,
            PickupHeal = 501,
            PickupPower = 502,
            PlayerHurt = 503,
            LowHpWarning = 504,

            // Flow
            TurnStart = 600,
            RewardReveal = 601,
            RewardPick = 602,
            LevelUp = 603,
            StageClear = 604,
            BossAppear = 605,
            GameOver = 606,
            Victory = 607,

            // Ninjago
            StaffWhoosh = 700,
            NinjaSlip = 701,
            NinjaSpin = 702,
            NinjaHit = 703,
            VehicleBump = 704,
        }

        [SerializeField] AudioSource audioSource = null!;

        [Header("Pitched playback")]
        [Tooltip("AudioSources added next to the main one at runtime so pitch/volume playback never affects other one-shots.")]
        [SerializeField, Range(1, 16)] int pooledSourceCount = 6;
        [Tooltip("Minimum seconds between two plays of the same effect; keeps many simultaneous ball hits under the voice limit.")]
        [SerializeField, Range(0f, 0.2f)] float minRepeatInterval = 0.035f;

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

        readonly HashSet<int> currentlyStartedClips = new();
        readonly Dictionary<int, float> lastPlayTimeByEffect = new();
        AudioSource[] pooledSources = Array.Empty<AudioSource>();
        int nextPooledIndex;

        protected override SfxManager GetThis() => this;

        #region Life Cycle

        protected override void Awake()
        {
            base.Awake();
            foreach (var pair in soundEffectDict)
            {
                // The prefab dictionary can lag behind the enum; a missing spec simply plays nothing.
                pair.Value?.Initialize();
            }

            CreatePooledSources();
        }

        void LateUpdate()
        {
            currentlyStartedClips.Clear();
        }

        #endregion

        #region Public Methods

        public void PlaySoundEffect(SoundEffect effect, AudioSource? customAudioSource = null)
        {
            if (!TryPickClip(effect, out var audioClip)) return;
            PlayAudioClip(audioClip, customAudioSource);
        }

        /// <summary>Plays on a pooled source so the pitch (combo rise) and volume only affect this clip.</summary>
        public void PlaySoundEffect(SoundEffect effect, float pitch, float volume = 1f)
        {
            if (!TryPickClip(effect, out var audioClip)) return;
            PlayAudioClip(audioClip, pitch, volume);
        }

        public bool HasClips(SoundEffect effect)
        {
            return soundEffectDict.TryGetValue(effect, out var spec) && spec != null && spec.clips.Length > 0;
        }

        // MARK - Helper
        public void PlayAudioClip(AudioClip clip, AudioSource? customAudioSource)
        {
            if (clip == null) return;
            if (!MarkStarted(clip)) return;
            (customAudioSource != null ? customAudioSource : audioSource).PlayOneShot(clip);
        }

        public void PlayAudioClip(AudioClip clip, float pitch, float volume)
        {
            if (clip == null) return;
            if (!MarkStarted(clip)) return;
            var source = NextPooledSource();
            source.pitch = pitch;
            source.volume = volume;
            source.clip = clip;
            source.Play();
        }

        #endregion

        #region Helpers

        bool TryPickClip(SoundEffect effect, out AudioClip clip)
        {
            clip = null!;
            if (effect == SoundEffect.None) return false; // Don't play anything for None.
            if (!soundEffectDict.TryGetValue(effect, out var spec) || spec == null) return false;

            var key = (int)effect;
            var now = Time.unscaledTime;
            if (lastPlayTimeByEffect.TryGetValue(key, out var lastTime) && now - lastTime < minRepeatInterval) return false;

            clip = spec.PickSingleClip();
            if (clip == null) return false;
            lastPlayTimeByEffect[key] = now;
            return true;
        }

        // The same AudioClip starts at most once per frame.
        bool MarkStarted(AudioClip clip)
        {
            return currentlyStartedClips.Add(clip.GetInstanceID());
        }

        void CreatePooledSources()
        {
            pooledSources = new AudioSource[pooledSourceCount];
            for (var i = 0; i < pooledSources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = audioSource.spatialBlend;
                source.priority = audioSource.priority;
                pooledSources[i] = source;
            }
        }

        AudioSource NextPooledSource()
        {
            var count = pooledSources.Length;
            for (var i = 0; i < count; i++)
            {
                var candidateIndex = (nextPooledIndex + i) % count;
                var candidate = pooledSources[candidateIndex];
                if (candidate.isPlaying) continue;
                nextPooledIndex = (candidateIndex + 1) % count;
                return candidate;
            }

            // Every source is busy: steal the oldest in round-robin order.
            var stolen = pooledSources[nextPooledIndex];
            nextPooledIndex = (nextPooledIndex + 1) % count;
            return stolen;
        }

        #endregion
    }
}
