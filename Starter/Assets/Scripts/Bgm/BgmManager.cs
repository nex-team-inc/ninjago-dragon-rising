#nullable enable

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
    }
}
