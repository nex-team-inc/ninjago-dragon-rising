#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;
using Nex.Util;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Registry of every pooled view on the board (enemies, field objects, pickups, balls) keyed by simulation id,
    /// plus the HP labels attached to enemies and crates. Owns spawning/releasing through the pools; never derives
    /// geometry itself (ArenaLayout only).
    /// </summary>
    public sealed class BoardViews
    {
        readonly BilliardRogueConfig config;
        readonly GameRules rules;
        readonly ArenaLayout layout;
        readonly JuiceConfig juice;
        readonly EnumDictionary<EnemyType, EnemyViewPool> enemyPools;
        readonly BallViewPool ballPool;
        readonly EnumDictionary<FieldObjectType, FieldObjectViewPool> objectPools;
        readonly EnumDictionary<PickupType, PickupViewPool> pickupPools;
        readonly WorldLabelLayer labels;
        readonly Dictionary<int, EnemyView> enemies = new();
        readonly List<EnemyView> enemyList = new();
        readonly Dictionary<int, FieldObjectView> objects = new();
        readonly List<FieldObjectView> objectList = new();
        readonly Dictionary<int, PickupView> pickups = new();
        readonly List<PickupView> pickupList = new();
        readonly Dictionary<int, BallView> balls = new();
        readonly List<BallView> ballList = new();
        readonly List<int> scratchIds = new();
        int ballFrame;

        public BoardViews(BilliardRogueConfig aConfig, GameRules aRules, ArenaLayout aLayout, EnumDictionary<EnemyType, EnemyViewPool> aEnemyPools,
            BallViewPool aBallPool, EnumDictionary<FieldObjectType, FieldObjectViewPool> aObjectPools, EnumDictionary<PickupType, PickupViewPool> aPickupPools,
            WorldLabelLayer aLabels, BallView ballPrefab, int worldLayer)
        {
            config = aConfig;
            rules = aRules;
            layout = aLayout;
            juice = aConfig.Juice;
            enemyPools = aEnemyPools;
            ballPool = aBallPool;
            objectPools = aObjectPools;
            pickupPools = aPickupPools;
            labels = aLabels;
            var enemyTypes = EnumDictionary<EnemyType, EnemyViewPool>.allKeys;
            for (var i = 0; i < enemyTypes.Length; i++)
            {
                var pool = enemyPools[enemyTypes[i]];
                pool.Layer = worldLayer;
                pool.Initialize(config.Enemies.Get(enemyTypes[i]).Prefab, 6);
                pool.Prewarm(2);
            }

            ballPool.Layer = worldLayer;
            ballPool.Initialize(ballPrefab, 16);
            ballPool.Prewarm(16);
            var objectTypes = EnumDictionary<FieldObjectType, FieldObjectViewPool>.allKeys;
            for (var i = 0; i < objectTypes.Length; i++)
            {
                var pool = objectPools[objectTypes[i]];
                pool.Layer = worldLayer;
                pool.Initialize(config.FieldObjects.ObjectPrefabs[objectTypes[i]], 2);
            }

            var pickupTypes = EnumDictionary<PickupType, PickupViewPool>.allKeys;
            for (var i = 0; i < pickupTypes.Length; i++)
            {
                var pool = pickupPools[pickupTypes[i]];
                pool.Layer = worldLayer;
                pool.Initialize(config.FieldObjects.PickupPrefabs[pickupTypes[i]], 2);
            }
        }

        public IReadOnlyList<EnemyView> Enemies => enemyList;
        public IReadOnlyList<FieldObjectView> Objects => objectList;
        public IReadOnlyList<PickupView> Pickups => pickupList;
        public IReadOnlyList<BallView> Balls => ballList;

        #region Rebuild & reconcile

        public void Rebuild(RunState run)
        {
            ClearAll();
            var board = run.board;
            for (var i = 0; i < board.enemies.Count; i++)
            {
                SpawnEnemy(board.enemies[i], false);
            }

            for (var i = 0; i < board.fieldObjects.Count; i++)
            {
                SpawnObject(board.fieldObjects[i], run);
            }

            for (var i = 0; i < board.pickups.Count; i++)
            {
                SpawnPickup(board.pickups[i]);
            }
        }

        /// <summary>Brings the views back in line with the state after an animated phase (drift safety net).</summary>
        public void Reconcile(RunState run)
        {
            var board = run.board;
            scratchIds.Clear();
            foreach (var pair in enemies)
            {
                if (FindEnemy(run, pair.Key) == null) scratchIds.Add(pair.Key);
            }

            for (var i = 0; i < scratchIds.Count; i++)
            {
                RemoveEnemy(scratchIds[i], false);
            }

            for (var i = 0; i < board.enemies.Count; i++)
            {
                var state = board.enemies[i];
                if (!enemies.TryGetValue(state.id, out var view))
                {
                    SpawnEnemy(state, false);
                    continue;
                }

                view.SnapTo(layout.FootprintCenterWorld(state.col, state.row, state.width, state.height));
                view.SetHp(state.hp);
                view.SetFrozen(state.status.frozenTurns > 0, state.status.burn, state.status.poison);
                view.Status.SetShield(state.shieldFace, false);
                if (labels.TryGetLabel(state.id, out var label))
                {
                    label.SetHp(state.hp, state.maxHp);
                    label.SetStatus(state.status.burn, state.status.poison, state.status.frozenTurns > 0);
                }
            }

            scratchIds.Clear();
            foreach (var pair in objects)
            {
                if (FindObject(run, pair.Key) == null) scratchIds.Add(pair.Key);
            }

            for (var i = 0; i < scratchIds.Count; i++)
            {
                RemoveObject(scratchIds[i], false);
            }

            for (var i = 0; i < board.fieldObjects.Count; i++)
            {
                var state = board.fieldObjects[i];
                if (!objects.TryGetValue(state.id, out var view))
                {
                    SpawnObject(state, run);
                    continue;
                }

                view.SetHp(state.hp);
                if (labels.TryGetLabel(state.id, out var label)) label.SetHp(state.hp, view.MaxHp);
            }

            SyncPickups(run);
        }

        /// <summary>Wave pickups have no spawn event (HANDOFF §4): add the missing views, drop the stale ones.</summary>
        public void SyncPickups(RunState run)
        {
            var board = run.board;
            scratchIds.Clear();
            foreach (var pair in pickups)
            {
                if (FindPickup(run, pair.Key) == null) scratchIds.Add(pair.Key);
            }

            for (var i = 0; i < scratchIds.Count; i++)
            {
                RemovePickup(scratchIds[i], false);
            }

            for (var i = 0; i < board.pickups.Count; i++)
            {
                var state = board.pickups[i];
                if (pickups.TryGetValue(state.id, out var view))
                {
                    view.SnapTo(layout.CellCenterWorld(state.col, state.row));
                    continue;
                }

                SpawnPickup(state);
            }
        }

        public void ClearAll()
        {
            for (var i = 0; i < enemyList.Count; i++)
            {
                enemyList[i].ReleaseNow();
            }

            enemyList.Clear();
            enemies.Clear();
            for (var i = 0; i < objectList.Count; i++)
            {
                objectList[i].Release();
            }

            objectList.Clear();
            objects.Clear();
            for (var i = 0; i < pickupList.Count; i++)
            {
                pickupList[i].Release();
            }

            pickupList.Clear();
            pickups.Clear();
            for (var i = 0; i < ballList.Count; i++)
            {
                ballList[i].Release();
            }

            ballList.Clear();
            balls.Clear();
            labels.ClearAll();
        }

        #endregion

        #region Enemies

        public EnemyView SpawnEnemy(EnemyState state, bool pop)
        {
            if (enemies.TryGetValue(state.id, out var existing)) return existing;
            var definition = config.Enemies.Get(state.type);
            var view = enemyPools[state.type].Get();
            view.Spawn(state, definition.Rules.isBoss, layout, juice, pop);
            enemies[state.id] = view;
            enemyList.Add(view);
            var label = labels.AttachEnemy(view);
            label.SetStatus(state.status.burn, state.status.poison, state.status.frozenTurns > 0);
            return view;
        }

        public bool TryGetEnemy(int id, out EnemyView view) => enemies.TryGetValue(id, out view);

        /// <summary>Detaches the label and either plays the death shrink (the view releases itself) or releases now.</summary>
        public void RemoveEnemy(int id, bool playDeath)
        {
            if (!enemies.TryGetValue(id, out var view)) return;
            enemies.Remove(id);
            enemyList.Remove(view);
            labels.Detach(id);
            if (playDeath) view.PlayDeath();
            else view.ReleaseNow();
        }

        public static EnemyState? FindEnemy(RunState run, int id)
        {
            var list = run.board.enemies;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].id == id) return list[i];
            }

            return null;
        }

        public EnemyView? FindBoss()
        {
            for (var i = 0; i < enemyList.Count; i++)
            {
                if (enemyList[i].IsBoss) return enemyList[i];
            }

            return null;
        }

        #endregion

        #region Field objects & pickups

        public FieldObjectView SpawnObject(FieldObjectState state, RunState run)
        {
            if (objects.TryGetValue(state.id, out var existing)) return existing;
            var view = objectPools[state.type].Get();
            var maxHp = state.type == FieldObjectType.Crate ? CrateMaxHp(run) : state.hp;
            view.Spawn(state, maxHp, layout, juice);
            objects[state.id] = view;
            objectList.Add(view);
            if (state.type == FieldObjectType.Crate) labels.AttachCrate(view);
            return view;
        }

        public bool TryGetObject(int id, out FieldObjectView view) => objects.TryGetValue(id, out view);

        public void RemoveObject(int id, bool playBreak)
        {
            if (!objects.TryGetValue(id, out var view)) return;
            objects.Remove(id);
            objectList.Remove(view);
            labels.Detach(id);
            if (playBreak) view.PlayBreak();
            else view.Release();
        }

        public static FieldObjectState? FindObject(RunState run, int id)
        {
            var list = run.board.fieldObjects;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].id == id) return list[i];
            }

            return null;
        }

        public PickupView SpawnPickup(PickupState state)
        {
            if (pickups.TryGetValue(state.id, out var existing)) return existing;
            var view = pickupPools[state.type].Get();
            view.Spawn(state, layout, juice);
            pickups[state.id] = view;
            pickupList.Add(view);
            return view;
        }

        public bool TryGetPickup(int id, out PickupView view) => pickups.TryGetValue(id, out view);

        public void RemovePickup(int id, bool playCollect)
        {
            if (!pickups.TryGetValue(id, out var view)) return;
            pickups.Remove(id);
            pickupList.Remove(view);
            if (playCollect) view.PlayCollect();
            else view.Release();
        }

        public static PickupState? FindPickup(RunState run, int id)
        {
            var list = run.board.pickups;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].id == id) return list[i];
            }

            return null;
        }

        int CrateMaxHp(RunState run)
        {
            var balance = rules.balance;
            return Mathf.Max(1, Mathf.RoundToInt(balance.crateHp * (1f + balance.hpScalePerStage * run.stageNumber)));
        }

        #endregion

        #region Balls

        public void BeginBallFrame()
        {
            ballFrame++;
        }

        /// <summary>Visitor for BallSimulator.ForEachBall: spawns unknown ids and stamps every reported ball.</summary>
        public void OnBall(in BallSnapshot ball)
        {
            var world = layout.ToWorld(ball.position, ball.radius);
            if (!balls.TryGetValue(ball.id, out var view))
            {
                view = ballPool.Get();
                view.Spawn(ball.id, config.Balls.Get(ball.type), ball.isMini, ball.radius * layout.CellSize, world, juice.Balls);
                balls[ball.id] = view;
                ballList.Add(view);
            }

            view.Sync(world, layout.DirectionToWorld(ball.velocity) * layout.CellSize, ballFrame);
        }

        /// <summary>Releases the balls the simulator stopped reporting this frame.</summary>
        public void EndBallFrame()
        {
            for (var i = ballList.Count - 1; i >= 0; i--)
            {
                var view = ballList[i];
                if (view.FrameStamp == ballFrame) continue;
                ballList.RemoveAt(i);
                balls.Remove(view.Id);
                view.Release();
            }
        }

        public bool TryGetBall(int id, out BallView view) => balls.TryGetValue(id, out view);

        #endregion
    }
}
