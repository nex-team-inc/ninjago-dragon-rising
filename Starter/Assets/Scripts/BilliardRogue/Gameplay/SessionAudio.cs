#nullable enable

using Cysharp.Threading.Tasks;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The session's music and non-board SFX: act/boss/reward BGM crossfades, stingers, turn start, enemy step
    /// batches, player hurt and the low-HP warning. Board hit sounds belong to BoardPresenter. Disabled in
    /// headless smoke runs (no BgmManager / SfxManager).
    /// </summary>
    public sealed class SessionAudio
    {
        readonly bool enabled;
        readonly float crossfadeSeconds;

        public SessionAudio(bool aEnabled, float aCrossfadeSeconds)
        {
            enabled = aEnabled;
            crossfadeSeconds = aCrossfadeSeconds;
        }

        public void PlayMusic(BgmManager.BgmType type)
        {
            if (!enabled) return;
            BgmManager.Instance.CrossFadeTo(type, crossfadeSeconds).Forget();
        }

        public void Stinger(BgmManager.StingerType stinger)
        {
            if (!enabled) return;
            BgmManager.Instance.PlayStinger(stinger);
        }

        public void Sfx(SfxManager.SoundEffect effect)
        {
            if (!enabled) return;
            SfxManager.Instance.PlaySoundEffect(effect);
        }
    }
}
