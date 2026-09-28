#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Turns one SimEvent into immediate feedback: view reactions, damage numbers and floats, VFX through
    /// VfxManager, SFX through SfxManager (combo-pitched hits, at most one clip per effect per frame) and camera
    /// shake. Used by BoardPresenter.Consume during the player turn and by EnemyPhasePlayer step by step.
    /// </summary>
    public sealed class BoardEventPlayer
    {
        const int SfxSlots = 200;

        readonly BoardViews views;
        readonly WorldLabelLayer labels;
        readonly ComboPresenter combo;
        readonly CameraShaker shaker;
        readonly JuiceConfig juice;
        readonly BilliardRogueConfig config;
        readonly ArenaLayout layout;
        readonly CatView[] cats;
        readonly int[] sfxFrame = new int[SfxSlots];
        readonly Dictionary<int, int> combos = new();
        int shooterIndex;

        public BoardEventPlayer(BoardViews aViews, WorldLabelLayer aLabels, ComboPresenter aCombo, CameraShaker aShaker, BilliardRogueConfig aConfig, ArenaLayout aLayout, CatView[] aCats)
        {
            views = aViews;
            labels = aLabels;
            combo = aCombo;
            shaker = aShaker;
            config = aConfig;
            juice = aConfig.Juice;
            layout = aLayout;
            cats = aCats;
        }

        public int ShooterIndex
        {
            get => shooterIndex;
            set => shooterIndex = value;
        }

        #region Dispatch

        public void Play(in SimEvent ev, RunState run)
        {
            switch (ev.kind)
            {
                case SimEventKind.BallLaunched: OnBallLaunched(ev); break;
                case SimEventKind.BallWallBounce: OnWallBounce(ev); break;
                case SimEventKind.BallSplit: Burst(VfxManager.VisualEffect.SplitPop, SfxManager.SoundEffect.Split, ev.position, labels.Texts.Split, juice.NormalDamageColor); break;
                case SimEventKind.BallTeleported: OnTeleport(ev); break;
                case SimEventKind.BallSlowed: PlayVfx(VfxManager.VisualEffect.DustPuff, Ground(ev.position)); break;
                case SimEventKind.EnemyHit: OnEnemyHit(ev); break;
                case SimEventKind.EnemyBlocked: OnEnemyBlocked(ev); break;
                case SimEventKind.EnemyKilled: OnEnemyKilled(ev); break;
                case SimEventKind.EnemyHealed: OnEnemyHealed(ev, run); break;
                case SimEventKind.EnemySpawned: OnEnemySpawned(ev, run); break;
                case SimEventKind.StatusApplied: OnStatusApplied(ev, run); break;
                case SimEventKind.FreezeExpired: OnFreezeExpired(ev, run); break;
                case SimEventKind.ChainLightning: OnChain(ev); break;
                case SimEventKind.Explosion: OnExplosion(ev); break;
                case SimEventKind.CrateHit: OnCrateHit(ev); break;
                case SimEventKind.CrateBroken: OnCrateBroken(ev, run); break;
                case SimEventKind.PickupCollected: OnPickupCollected(ev); break;
                case SimEventKind.PickupExpired: views.RemovePickup(ev.targetId, false); break;
                case SimEventKind.PlayerDamaged: OnPlayerDamaged(ev); break;
                case SimEventKind.PlayerHealed: OnPlayerHealed(ev); break;
                case SimEventKind.PlayerDied: OnPlayerDied(); break;
                case SimEventKind.PowerShotConsumed: Burst(VfxManager.VisualEffect.LevelUpBurst, SfxManager.SoundEffect.PowerShot, ev.position, labels.Texts.Power, juice.CritDamageColor); break;
                case SimEventKind.BallExited: combos.Remove(ev.ballId); break;
                case SimEventKind.ComboChanged:
                    combos[ev.ballId] = ev.value;
                    combo.OnComboChanged(ev.value, Center(ev.position), juice.PlayerColor(shooterIndex));
                    break;
                case SimEventKind.BossPhaseChanged: OnBossPhase(ev); break;
                case SimEventKind.WaveSpawned: views.SyncPickups(run); break;
            }
        }

        #endregion

        #region Balls

        void OnBallLaunched(in SimEvent ev)
        {
            var definition = config.Balls.Get(ev.ballType);
            PlaySfx(definition.LaunchSfx);
            PlayVfx(VfxManager.VisualEffect.DustPuff, Ground(ev.position));
        }

        void OnWallBounce(in SimEvent ev)
        {
            PlaySfx(SfxManager.SoundEffect.BallWallBounce, 1f, 0.6f);
            PlayVfx(VfxManager.VisualEffect.WallSpark, Center(ev.position));
        }

        void OnTeleport(in SimEvent ev)
        {
            PlaySfx(SfxManager.SoundEffect.Portal);
            PlayVfx(VfxManager.VisualEffect.PortalFlash, Center(ev.position));
            PlayVfx(VfxManager.VisualEffect.PortalFlash, Center(ev.position2));
        }

        #endregion

        #region Enemies

        void OnEnemyHit(in SimEvent ev)
        {
            // Status ticks carry no ball (ballId 0) and the status ball type; explosions also have no ball but show numbers.
            var isTick = ev.ballId == 0 && (ev.ballType == BallType.Flame || ev.ballType == BallType.Venom);
            var world = Center(ev.position);
            if (views.TryGetEnemy(ev.targetId, out var view))
            {
                view.SetHp(ev.value2);
                var direction = views.TryGetBall(ev.ballId, out var ball) ? ball.Velocity : Vector3.zero;
                if (isTick) view.Status.Flash(0.5f, labels.ColorFor(ev.ballType == BallType.Venom ? StatusType.Poison : StatusType.Burn));
                else view.PlayHit(direction, ev.flag, juice.EnemyHitFlash);
                world = view.Center;
                if (labels.TryGetLabel(ev.targetId, out var label)) label.SetHp(ev.value2, view.MaxHp);
            }

            if (isTick) return;
            var definition = config.Balls.Get(ev.ballType);
            var damage = ev.value;
            labels.ShowNumber(world, damage, ev.flag ? NumberKind.Crit : NumberKind.Normal);
            if (ev.flag) labels.ShowText(world + Vector3.up * 0.25f, labels.Texts.Crit, juice.CritDamageColor);
            var s = juice.Sequences;
            var isBoss = view != null && view.IsBoss;
            var sfx = ev.flag ? SfxManager.SoundEffect.CritHit : damage >= s.hardDamage ? SfxManager.SoundEffect.BallHitHard : damage >= s.midDamage ? SfxManager.SoundEffect.BallHitMid : definition.HitSfx;
            var pitch = combo.Pitch(views.TryGetBall(ev.ballId, out var b) && b.IsMini ? 1 : ComboOf(ev.ballId));
            PlaySfx(isBoss ? SfxManager.SoundEffect.BossHit : sfx, pitch, 1f);
            PlayVfx(ev.flag ? VfxManager.VisualEffect.CritSpark : definition.HitVfx, world, ev.flag ? s.critVfxScale : 1f);
            shaker.Shake(juice.ShakeAmplitudeByDamage.Evaluate(damage));
        }

        /// <summary>ComboChanged arrives right after EnemyHit for the same ball, so this hit is the previous combo + 1.</summary>
        int ComboOf(int ballId)
        {
            return combos.TryGetValue(ballId, out var previous) ? previous + 1 : 1;
        }

        void OnEnemyBlocked(in SimEvent ev)
        {
            var world = Center(ev.position);
            if (views.TryGetEnemy(ev.targetId, out var view))
            {
                view.Status.Flash(0.6f, juice.BlockColor);
                world = view.Center;
            }

            labels.ShowText(world, labels.Texts.Block, juice.BlockColor);
            PlaySfx(SfxManager.SoundEffect.Blocked);
            PlayVfx(VfxManager.VisualEffect.WallSpark, Center(ev.position));
        }

        void OnEnemyKilled(in SimEvent ev)
        {
            var definition = config.Enemies.Get(ev.enemyType);
            var world = views.TryGetEnemy(ev.targetId, out var view) ? view.Center : Center(ev.position);
            views.RemoveEnemy(ev.targetId, true);
            PlaySfx(definition.DeathSfx);
            PlayVfx(definition.DeathVfx, world, ev.flag ? 2f : 1f);
            if (ev.flag) shaker.Shake(juice.BossDeathShake);
        }

        void OnEnemyHealed(in SimEvent ev, RunState run)
        {
            var world = Center(ev.position);
            if (views.TryGetEnemy(ev.targetId, out var view))
            {
                world = view.Center;
                var state = BoardViews.FindEnemy(run, ev.targetId);
                if (state != null)
                {
                    view.SetHp(state.hp);
                    if (labels.TryGetLabel(ev.targetId, out var label)) label.SetHp(state.hp, state.maxHp);
                }
            }

            labels.ShowNumber(world, ev.value, NumberKind.Heal);
            PlayVfx(VfxManager.VisualEffect.HealSparkle, world);
            PlaySfx(SfxManager.SoundEffect.Heal);
        }

        void OnEnemySpawned(in SimEvent ev, RunState run)
        {
            var state = BoardViews.FindEnemy(run, ev.targetId);
            if (state == null) return;
            var view = views.SpawnEnemy(state, true);
            PlayVfx(VfxManager.VisualEffect.DustPuff, view.Position);
            if (ev.sourceId != 0 && views.TryGetEnemy(ev.sourceId, out var caster)) caster.PlayCast(config.Pacing.AbilityDuration);
        }

        void OnStatusApplied(in SimEvent ev, RunState run)
        {
            var state = BoardViews.FindEnemy(run, ev.targetId);
            var world = Center(ev.position);
            if (views.TryGetEnemy(ev.targetId, out var view) && state != null)
            {
                view.SetFrozen(state.status.frozenTurns > 0, state.status.burn, state.status.poison);
                world = view.Center;
                if (labels.TryGetLabel(ev.targetId, out var label)) label.SetStatus(state.status.burn, state.status.poison, state.status.frozenTurns > 0);
            }

            switch (ev.status)
            {
                case StatusType.Burn:
                    PlayVfx(VfxManager.VisualEffect.BurnBurst, world);
                    PlaySfx(SfxManager.SoundEffect.Burn);
                    break;
                case StatusType.Poison:
                    PlayVfx(VfxManager.VisualEffect.PoisonBurst, world);
                    PlaySfx(SfxManager.SoundEffect.Poison);
                    break;
                default:
                    PlayVfx(VfxManager.VisualEffect.FreezeBurst, world);
                    PlaySfx(SfxManager.SoundEffect.Freeze);
                    labels.ShowText(world + Vector3.up * 0.25f, labels.Texts.Frozen, juice.EnemyFreezeTint);
                    break;
            }
        }

        void OnFreezeExpired(in SimEvent ev, RunState run)
        {
            var state = BoardViews.FindEnemy(run, ev.targetId);
            if (!views.TryGetEnemy(ev.targetId, out var view) || state == null) return;
            view.SetFrozen(false, state.status.burn, state.status.poison);
            if (labels.TryGetLabel(ev.targetId, out var label)) label.SetStatus(state.status.burn, state.status.poison, false);
        }

        void OnChain(in SimEvent ev)
        {
            var target = views.TryGetEnemy(ev.targetId, out var view) ? view.Center : Center(ev.position2);
            PlayVfx(VfxManager.VisualEffect.LightningHit, target);
            PlaySfx(SfxManager.SoundEffect.Lightning, 1f, 0.8f);
        }

        void OnExplosion(in SimEvent ev)
        {
            var radius = Mathf.Max(1, ev.value);
            PlayVfx(VfxManager.VisualEffect.Explosion, Center(ev.position), radius * juice.Sequences.explosionVfxScalePerRadius);
            PlaySfx(SfxManager.SoundEffect.Explosion);
            shaker.Shake(juice.Sequences.explosionShake * radius);
        }

        void OnBossPhase(in SimEvent ev)
        {
            if (!views.TryGetEnemy(ev.targetId, out var view)) return;
            view.PlayRoar();
            labels.ShowText(view.LabelAnchor, labels.Texts.Enraged, juice.CritDamageColor);
            PlaySfx(SfxManager.SoundEffect.BossAppear);
            shaker.Shake(juice.Sequences.bossIntroShake);
        }

        #endregion

        #region Crates, pickups, player

        void OnCrateHit(in SimEvent ev)
        {
            if (!views.TryGetObject(ev.targetId, out var view)) return;
            view.SetHp(ev.value2);
            view.PlayHit();
            if (labels.TryGetLabel(ev.targetId, out var label)) label.SetHp(ev.value2, view.MaxHp);
            PlaySfx(SfxManager.SoundEffect.BallHitSoft, 0.9f, 0.8f);
            PlayVfx(VfxManager.VisualEffect.HitSpark, view.Center);
        }

        void OnCrateBroken(in SimEvent ev, RunState run)
        {
            var world = views.TryGetObject(ev.targetId, out var view) ? view.Center : Center(ev.position);
            views.RemoveObject(ev.targetId, true);
            PlaySfx(SfxManager.SoundEffect.CrateBreak);
            PlayVfx(VfxManager.VisualEffect.CratePieces, world);
            if (!ev.flag) return;
            var pickup = BoardViews.FindPickup(run, ev.sourceId);
            if (pickup != null) views.SpawnPickup(pickup);
        }

        void OnPickupCollected(in SimEvent ev)
        {
            var world = views.TryGetPickup(ev.targetId, out var view) ? view.Center : Center(ev.position);
            views.RemovePickup(ev.targetId, true);
            PlayVfx(VfxManager.VisualEffect.PickupSparkle, world);
            switch (ev.pickup)
            {
                case PickupType.ExtraBall:
                    PlaySfx(SfxManager.SoundEffect.PickupBall);
                    labels.ShowText(world, labels.Texts.ExtraBall, juice.PlayerColor(shooterIndex));
                    break;
                case PickupType.Heal:
                    PlaySfx(SfxManager.SoundEffect.PickupHeal);
                    break;
                default:
                    PlaySfx(SfxManager.SoundEffect.PickupPower);
                    labels.ShowText(world, labels.Texts.PowerUp, juice.CritDamageColor);
                    break;
            }
        }

        void OnPlayerDamaged(in SimEvent ev)
        {
            var cat = ActiveCat();
            cat.PlayHurt();
            labels.ShowNumber(cat.LabelAnchor, ev.value, NumberKind.PlayerHurt);
            PlaySfx(SfxManager.SoundEffect.PlayerHurt);
            PlayVfx(VfxManager.VisualEffect.PlayerHurtFlash, cat.Center);
            shaker.Shake(juice.PlayerHurtShake);
            if (ev.value2 > 0 && ev.value2 * 4 <= config.Balance.Rules.playerMaxHp) PlaySfx(SfxManager.SoundEffect.LowHpWarning);
        }

        void OnPlayerHealed(in SimEvent ev)
        {
            var cat = ActiveCat();
            labels.ShowNumber(cat.LabelAnchor, ev.value, NumberKind.Heal);
            PlayVfx(VfxManager.VisualEffect.HealSparkle, cat.Center);
            PlaySfx(SfxManager.SoundEffect.Heal);
        }

        void OnPlayerDied()
        {
            for (var i = 0; i < cats.Length; i++)
            {
                cats[i].PlayDefeat();
            }
        }

        #endregion

        #region Helpers

        CatView ActiveCat() => cats[Mathf.Clamp(shooterIndex, 0, cats.Length - 1)];

        Vector3 Center(Vector2 sim) => layout.ToWorld(sim, 0.45f);

        Vector3 Ground(Vector2 sim) => layout.ToWorld(sim, 0.02f);

        void Burst(VfxManager.VisualEffect vfx, SfxManager.SoundEffect sfx, Vector2 sim, string text, Color color)
        {
            var world = Center(sim);
            PlayVfx(vfx, world);
            PlaySfx(sfx);
            labels.ShowText(world, text, color);
        }

        public void PlayVfx(VfxManager.VisualEffect effect, Vector3 world, float scale = 1f)
        {
            VfxManager.Instance.PlayVisualEffect(effect, world, Quaternion.identity, scale);
        }

        /// <summary>One clip per effect per frame keeps many simultaneous balls under the voice budget.</summary>
        public void PlaySfx(SfxManager.SoundEffect effect, float pitch = 1f, float volume = 1f)
        {
            var slot = (int)effect + 1;
            if (slot < 0 || slot >= SfxSlots) return;
            var frame = Time.frameCount;
            if (sfxFrame[slot] == frame) return;
            sfxFrame[slot] = frame;
            if (Mathf.Approximately(pitch, 1f) && Mathf.Approximately(volume, 1f)) SfxManager.Instance.PlaySoundEffect(effect);
            else SfxManager.Instance.PlaySoundEffect(effect, pitch, volume);
        }

        #endregion
    }
}
