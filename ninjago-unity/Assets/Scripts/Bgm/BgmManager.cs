#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Nex.Util;
using UnityEngine;

namespace Nex
{
    public class BgmManager : Singleton<BgmManager>
    {
        // Values are persisted in the prefab EnumDictionary: explicit ints, append only, keep ascending order.
        public enum BgmType
        {
            Main = 0,
            Title = 1,
            Act1 = 10,
            Act2 = 11,
            Act3 = 12,
            Boss = 20,
            Reward = 30,
            NinjaFight = 40,
            NinjaChase = 41,
            StoneKick = 42,
            EarthSeal = 43,
        }

        public enum StingerType
        {
            StageClear = 0,
            BossAppear = 1,
            Victory = 2,
            Defeat = 3,
        }

        [SerializeField] AudioSource audioSource = null!;
        [SerializeField] EnumDictionary<BgmType, AudioClip> bgmDict = null!;

        [Header("Crossfade & stingers")]
        [Tooltip("Optional second music source for crossfades; created from the primary one at runtime when missing.")]
        [SerializeField] AudioSource? secondarySource;
        [Tooltip("Optional one-shot source for stingers; created from the primary one at runtime when missing.")]
        [SerializeField] AudioSource? stingerSource;
        [SerializeField] EnumDictionary<StingerType, AudioClip> stingerDict = new();
        [Tooltip("Music volume while a stinger plays.")]
        [SerializeField, Range(0f, 1f)] float stingerDuckVolume = 0.3f;
        [SerializeField, Range(0.05f, 2f)] float duckFadeSeconds = 0.15f;
        [SerializeField, Range(0.05f, 3f)] float unduckFadeSeconds = 0.6f;

        AudioSource active = null!;
        AudioSource inactive = null!;
        AudioSource stingerPlayer = null!;
        Tween? duckRestoreTween;
        // Volume every fade lands on: 1, or the duck level while a duck holds. Keeps crossfades and ducks from
        // fighting when a stinger plays right before a track change (stage clear → reward).
        float musicVolumeTarget = 1f;

        public BgmType? Current { get; private set; }
        public bool IsPlaying => active.isPlaying;

        protected override BgmManager GetThis() => this;

        #region Life Cycle

        protected override void Awake()
        {
            base.Awake();
            active = audioSource;
            inactive = secondarySource != null ? secondarySource : CloneSource(audioSource, true);
            stingerPlayer = stingerSource != null ? stingerSource : CloneSource(audioSource, false);
        }

        #endregion

        #region Playback

        /// <summary>Starts a track at once on the active source (no fade, volume untouched). Unknown clips stop music.</summary>
        public void Play(BgmType type)
        {
            active.DOKill();
            if (!TryGetClip(type, out var clip))
            {
                active.Stop();
                Current = null;
                return;
            }

            Current = type;
            active.clip = clip;
            active.Play();
        }

        public void Stop()
        {
            active.DOKill();
            inactive.DOKill();
            active.Stop();
            inactive.Stop();
            Current = null;
        }

        /// <summary>Fades the current track out and the new one in on the other source. Unscaled time, pause-safe.</summary>
        public async UniTask CrossFadeTo(BgmType type, float duration = 0.8f, CancellationToken cancellationToken = default)
        {
            if (Current == type && active.isPlaying) return;
            if (!TryGetClip(type, out var clip))
            {
                Current = null;
                await FadeOut(duration);
                active.Stop();
                return;
            }

            Current = type;
            var from = active;
            var to = inactive;
            active = to;
            inactive = from;
            from.DOKill();
            to.DOKill();
            to.clip = clip;
            to.volume = 0f;
            to.Play();

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            try
            {
                await UniTask.WhenAll(
                    from.DOFade(0f, duration).SetUpdate(true).ToUniTask(cancellationToken: linked.Token),
                    to.DOFade(musicVolumeTarget, duration).SetUpdate(true).ToUniTask(cancellationToken: linked.Token));
            }
            finally
            {
                // Sources may be gone when the manager is destroyed mid-fade. A duck or restore that took over the
                // fade-in (its tween is still running on `to`) lands on the target by itself.
                if (from != null) from.Stop();
                if (to != null && !DOTween.IsTweening(to)) to.volume = musicVolumeTarget;
            }
        }

        /// <summary>Plays a one-shot stinger over ducked music; returns the stinger length (0 when no clip is set).</summary>
        public float PlayStinger(StingerType stinger)
        {
            if (!stingerDict.TryGetValue(stinger, out var clip) || clip == null) return 0f;
            stingerPlayer.PlayOneShot(clip);
            Duck(stingerDuckVolume, clip.length);
            return clip.length;
        }

        /// <summary>Lowers the music to volume01 for holdSeconds (unscaled), then restores it. A crossfade started meanwhile fades in to the ducked level.</summary>
        public void Duck(float volume01, float holdSeconds)
        {
            duckRestoreTween?.Kill();
            musicVolumeTarget = volume01;
            active.DOKill();
            active.DOFade(volume01, duckFadeSeconds).SetUpdate(true).SetLink(gameObject);
            duckRestoreTween = DOVirtual.DelayedCall(holdSeconds, RestoreVolume, true).SetLink(gameObject);
        }

        #endregion

        #region Fading In/Out

        public async UniTask FadeIn(float duration = 0.5f)
        {
            await active.DOFade(musicVolumeTarget, duration).SetUpdate(true).WithCancellation(destroyCancellationToken);
        }

        public async UniTask FadeOut(float duration = 0.5f)
        {
            await active.DOFade(0f, duration).SetUpdate(true).WithCancellation(destroyCancellationToken);
        }

        #endregion

        #region Helpers

        bool TryGetClip(BgmType type, out AudioClip clip)
        {
            // The prefab dictionary can lag behind the enum; a missing clip is tolerated.
            return bgmDict.TryGetValue(type, out clip) && clip != null;
        }

        // Fades whichever source is active now: a crossfade may have swapped sources while the duck held.
        void RestoreVolume()
        {
            duckRestoreTween = null;
            musicVolumeTarget = 1f;
            active.DOKill();
            active.DOFade(1f, unduckFadeSeconds).SetUpdate(true).SetLink(gameObject);
        }

        static AudioSource CloneSource(AudioSource template, bool loop)
        {
            var source = template.gameObject.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = template.outputAudioMixerGroup;
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = template.spatialBlend;
            source.priority = template.priority;
            source.volume = 1f;
            return source;
        }

        #endregion
    }
}
