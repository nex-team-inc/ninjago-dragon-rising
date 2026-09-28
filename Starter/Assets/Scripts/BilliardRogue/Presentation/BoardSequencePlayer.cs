#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Stage clear, boss intro, defeat and victory moments on the board: cat poses, camera push-in through the
    /// CameraShaker rig offset, stingers via BgmManager and SFX via SfxManager, timed by PacingConfig.
    /// </summary>
    public sealed class BoardSequencePlayer
    {
        readonly BoardViews views;
        readonly BoardEventPlayer player;
        readonly CameraShaker shaker;
        readonly PacingConfig pacing;
        readonly JuiceConfig juice;
        readonly CatView[] cats;

        public BoardSequencePlayer(BoardViews aViews, BoardEventPlayer aPlayer, CameraShaker aShaker, BilliardRogueConfig config, CatView[] aCats)
        {
            views = aViews;
            player = aPlayer;
            shaker = aShaker;
            pacing = config.Pacing;
            juice = config.Juice;
            cats = aCats;
        }

        #region Public Methods

        public async UniTask StageClearAsync(CancellationToken ct)
        {
            BgmManager.Instance.PlayStinger(BgmManager.StingerType.StageClear);
            player.PlaySfx(SfxManager.SoundEffect.StageClear);
            for (var i = 0; i < cats.Length; i++)
            {
                if (cats[i].gameObject.activeSelf) cats[i].PlayVictory();
            }

            await Delay(pacing.StageClearDuration, ct);
            ResetCats();
        }

        public async UniTask BossIntroAsync(EnemyType boss, CancellationToken ct)
        {
            var view = views.FindBoss();
            BgmManager.Instance.PlayStinger(BgmManager.StingerType.BossAppear);
            player.PlaySfx(SfxManager.SoundEffect.BossAppear);
            var duration = pacing.BossIntroDuration;
            var s = juice.Sequences;
            if (view != null)
            {
                view.PlayRoar();
                player.PlayVfx(VfxManager.VisualEffect.DustPuff, view.Position, 2f);
            }

            shaker.Shake(s.bossIntroShake);
            await shaker.PushInAsync(s.bossIntroPushIn, duration * 0.5f, duration * 0.3f, duration * 0.2f, ct);
        }

        public async UniTask DefeatAsync(CancellationToken ct)
        {
            BgmManager.Instance.PlayStinger(BgmManager.StingerType.Defeat);
            player.PlaySfx(SfxManager.SoundEffect.GameOver);
            for (var i = 0; i < cats.Length; i++)
            {
                cats[i].PlayDefeat();
            }

            var duration = pacing.DefeatDuration;
            await shaker.PushInAsync(juice.Sequences.defeatPushIn, duration * 0.6f, duration * 0.4f, 0f, ct);
        }

        public async UniTask VictoryAsync(CancellationToken ct)
        {
            BgmManager.Instance.PlayStinger(BgmManager.StingerType.Victory);
            player.PlaySfx(SfxManager.SoundEffect.Victory);
            for (var i = 0; i < cats.Length; i++)
            {
                if (!cats[i].gameObject.activeSelf) continue;
                cats[i].PlayVictory();
                player.PlayVfx(VfxManager.VisualEffect.LevelUpBurst, cats[i].Center, 1.5f);
            }

            var duration = pacing.VictoryDuration;
            await shaker.PushInAsync(juice.Sequences.victoryPushIn, duration * 0.4f, duration * 0.6f, 0f, ct);
        }

        public void ResetCats()
        {
            for (var i = 0; i < cats.Length; i++)
            {
                cats[i].ResetPose();
            }

            shaker.ResetPushIn();
        }

        #endregion

        #region Helpers

        static UniTask Delay(float seconds, CancellationToken ct)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds)), cancellationToken: ct);
        }

        #endregion
    }
}
