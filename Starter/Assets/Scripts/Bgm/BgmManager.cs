#nullable enable

using Cysharp.Threading.Tasks;
using DG.Tweening;
using Nex.Util;
using UnityEngine;

namespace Nex
{
    public class BgmManager : Singleton<BgmManager>
    {
        public enum BgmType
        {
            Main
        }

        [SerializeField] AudioSource audioSource = null!;
        [SerializeField] EnumDictionary<BgmType, AudioClip> bgmDict = null!;

        protected override BgmManager GetThis() => this;

        public void Play(BgmType type)
        {
            audioSource.clip = bgmDict[type];
            audioSource.Play();
        }

        public void Stop()
        {
            audioSource.Stop();
        }

        #region Fading In/Out

        public async UniTask FadeIn(float duration = 0.5f)
        {
            await audioSource.DOFade(1, duration).WithCancellation(this.GetCancellationTokenOnDestroy());
        }

        public async UniTask FadeOut(float duration = 0.5f)
        {
            await audioSource.DOFade(0, duration).WithCancellation(this.GetCancellationTokenOnDestroy());
        }

        #endregion
    }
}
