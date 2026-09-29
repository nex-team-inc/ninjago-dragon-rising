#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Animates one resolved enemy phase from its events, step by step (SimEvent.step 0..4, HANDOFF §4): status
    /// ticks with numbers, abilities (telegraphs, casts, heals, spawns, quake), simultaneous hops, staggered
    /// danger-row attacks and the new wave, each step waiting its PacingConfig duration in gameplay time (the debug
    /// fast enemy phase is TimeScaleController's phase scale, set by EnemyPhaseRunner, so the waits, hops and tweens
    /// speed up together). Ends with a reconcile so the views match the state exactly.
    /// </summary>
    public sealed class EnemyPhasePlayer
    {
        const int StepCount = 5;

        readonly BoardViews views;
        readonly BoardEventPlayer player;
        readonly WorldLabelLayer labels;
        readonly CameraShaker shaker;
        readonly ArenaLayout layout;
        readonly BilliardRogueConfig config;
        readonly JuiceConfig juice;
        readonly CatView[] cats;

        public EnemyPhasePlayer(BoardViews aViews, BoardEventPlayer aPlayer, WorldLabelLayer aLabels, CameraShaker aShaker, ArenaLayout aLayout, BilliardRogueConfig aConfig, CatView[] aCats)
        {
            views = aViews;
            player = aPlayer;
            labels = aLabels;
            shaker = aShaker;
            layout = aLayout;
            config = aConfig;
            juice = aConfig.Juice;
            cats = aCats;
        }

        #region Public Methods

        public async UniTask PlayAsync(List<SimEvent> events, RunState run, PacingConfig pacing, CancellationToken ct)
        {
            ClearTelegraphs();
            for (var step = 0; step < StepCount; step++)
            {
                var wait = step switch
                {
                    0 => PlayStatus(events, run) ? pacing.StatusTickDuration : 0f,
                    1 => PlayAbilities(events, run, pacing) ? pacing.AbilityDuration : 0f,
                    2 => PlayAdvance(events, run, pacing.EnemyHopDuration) ? pacing.EnemyHopDuration : 0f,
                    3 => await PlayAttacksAsync(events, run, pacing, ct),
                    _ => PlaySpawn(events, run) ? pacing.WaveSpawnDuration : 0f,
                };
                if (wait > 0f) await Delay(wait, ct);
                if (run.outcome == RunOutcome.Defeat && step >= 3) break;
            }

            views.Reconcile(run);
        }

        #endregion

        #region Steps

        bool PlayStatus(List<SimEvent> events, RunState run)
        {
            var any = false;
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.step != 0) continue;
                switch (ev.kind)
                {
                    case SimEventKind.StatusTick:
                        any = true;
                        var world = views.TryGetEnemy(ev.targetId, out var view) ? view.Center : layout.ToWorld(ev.position, 0.45f);
                        labels.ShowNumber(world, ev.value, ev.status == StatusType.Burn ? NumberKind.Burn : NumberKind.Poison);
                        player.PlayVfx(ev.status == StatusType.Burn ? VfxManager.VisualEffect.BurnBurst : VfxManager.VisualEffect.PoisonBurst, world, 0.7f);
                        player.PlaySfx(ev.status == StatusType.Burn ? SfxManager.SoundEffect.Burn : SfxManager.SoundEffect.Poison, 1f, 0.7f);
                        break;
                    default:
                        player.Play(ev, run);
                        break;
                }
            }

            RefreshStatusVisuals(run);
            return any;
        }

        bool PlayAbilities(List<SimEvent> events, RunState run, PacingConfig pacing)
        {
            var any = false;
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.step != 1) continue;
                switch (ev.kind)
                {
                    case SimEventKind.EnemyAbilityTelegraph:
                        if (ev.flag)
                        {
                            any = true;
                            PlayQuake(ev, pacing);
                        }
                        else
                        {
                            labels.SetTelegraph(ev.targetId, ev.value);
                        }

                        break;
                    case SimEventKind.EnemyShieldRotated:
                        any = true;
                        if (views.TryGetEnemy(ev.targetId, out var golem))
                        {
                            golem.Status.SetShield((Face)ev.value, true);
                            labels.ShowText(golem.LabelAnchor, labels.Texts.Shield, juice.BlockColor);
                        }

                        player.PlaySfx(SfxManager.SoundEffect.Blocked, 0.8f, 0.8f);
                        break;
                    case SimEventKind.EnemyAttack:
                        any = true;
                        PlayRangedAttack(ev, pacing);
                        break;
                    case SimEventKind.EnemyHealed:
                        any = true;
                        if (views.TryGetEnemy(ev.sourceId, out var healer)) healer.PlayCast(pacing.AbilityDuration);
                        player.Play(ev, run);
                        break;
                    case SimEventKind.EnemySpawned:
                        any = true;
                        player.Play(ev, run);
                        if (views.TryGetEnemy(ev.sourceId, out var caster)) labels.ShowText(caster.LabelAnchor, labels.Texts.Summon, juice.PoisonColor);
                        break;
                    default:
                        player.Play(ev, run);
                        break;
                }
            }

            return any;
        }

        bool PlayAdvance(List<SimEvent> events, RunState run, float hopDuration)
        {
            var any = false;
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.step != 2) continue;
                switch (ev.kind)
                {
                    case SimEventKind.EnemyMoved when ev.flag:
                        if (views.TryGetPickup(ev.targetId, out var pickup)) pickup.MoveTo(layout.ToWorld(ev.position2), hopDuration);
                        any = true;
                        break;
                    case SimEventKind.EnemyMoved:
                        if (views.TryGetEnemy(ev.targetId, out var enemy))
                        {
                            var hop = config.Enemies.Get(ev.enemyType).HopHeight;
                            enemy.HopTo(layout.ToWorld(ev.position2), hopDuration, hop);
                        }

                        any = true;
                        break;
                    default:
                        player.Play(ev, run);
                        break;
                }
            }

            if (any) player.PlaySfx(SfxManager.SoundEffect.EnemyStep);
            return any;
        }

        async UniTask<float> PlayAttacksAsync(List<SimEvent> events, RunState run, PacingConfig pacing, CancellationToken ct)
        {
            var attackers = 0;
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.step != 3) continue;
                if (ev.kind == SimEventKind.EnemyAttack)
                {
                    if (attackers > 0) await Delay(pacing.EnemyStagger, ct);
                    attackers++;
                    PlayMeleeAttack(ev, pacing);
                    continue;
                }

                player.Play(ev, run);
            }

            return attackers > 0 ? pacing.EnemyAttackDuration : 0f;
        }

        bool PlaySpawn(List<SimEvent> events, RunState run)
        {
            var any = false;
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.step != 4) continue;
                if (ev.kind == SimEventKind.EnemySpawned || ev.kind == SimEventKind.WaveSpawned) any = true;
                player.Play(ev, run);
            }

            if (any) player.PlaySfx(SfxManager.SoundEffect.TurnStart, 1f, 0.7f);
            return any;
        }

        #endregion

        #region Helpers

        void PlayQuake(in SimEvent ev, PacingConfig pacing)
        {
            if (views.TryGetEnemy(ev.targetId, out var boss))
            {
                boss.PlayCast(pacing.AbilityDuration);
                labels.ShowText(boss.LabelAnchor, labels.Texts.Quake, juice.BurnColor);
            }

            shaker.Shake(juice.Sequences.quakeShake);
            player.PlaySfx(SfxManager.SoundEffect.Explosion, 0.7f, 0.9f);
        }

        void PlayRangedAttack(in SimEvent ev, PacingConfig pacing)
        {
            var definition = config.Enemies.Get(ev.enemyType);
            if (views.TryGetEnemy(ev.targetId, out var caster))
            {
                caster.PlayCast(pacing.AbilityDuration);
                player.PlayVfx(VfxManager.VisualEffect.LightningHit, caster.Center, 0.8f);
            }

            player.PlaySfx(definition.AttackSfx);
        }

        void PlayMeleeAttack(in SimEvent ev, PacingConfig pacing)
        {
            var definition = config.Enemies.Get(ev.enemyType);
            if (views.TryGetEnemy(ev.targetId, out var attacker)) attacker.PlayAttackLunge(pacing.EnemyAttackDuration);
            player.PlaySfx(definition.AttackSfx);
            shaker.Shake(juice.Sequences.enemyAttackShake);
        }

        void RefreshStatusVisuals(RunState run)
        {
            var enemies = run.board.enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var state = enemies[i];
                if (!views.TryGetEnemy(state.id, out var view)) continue;
                view.SetFrozen(state.status.frozenTurns > 0, state.status.burn, state.status.poison);
                if (labels.TryGetLabel(state.id, out var label)) label.SetStatus(state.status.burn, state.status.poison, state.status.frozenTurns > 0);
            }
        }

        void ClearTelegraphs()
        {
            var enemies = views.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                labels.SetTelegraph(enemies[i].Id, -1);
            }
        }

        static UniTask Delay(float seconds, CancellationToken ct)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds)), cancellationToken: ct);
        }

        #endregion
    }
}
