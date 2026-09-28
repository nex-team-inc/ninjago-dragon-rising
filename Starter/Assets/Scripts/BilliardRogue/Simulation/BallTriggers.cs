#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>Overlap triggers a ball passes through without reflecting: pickups, portals, mud, pierced enemies.</summary>
    internal sealed class BallTriggers
    {
        const float PickupInset = 0.2f;
        const float PortalInset = 0.25f;
        const float MudInset = 0.1f;

        readonly GameRules rules;
        readonly BoardOps ops;

        #region Life Cycle

        public BallTriggers(GameRules rules, BoardOps ops)
        {
            this.rules = rules;
            this.ops = ops;
        }

        #endregion

        #region Public Methods

        public void CollectPickups(RunState run, BallSlot b, List<SimEvent> events)
        {
            var pickups = run.board.pickups;
            for (var i = pickups.Count - 1; i >= 0; i--)
            {
                if (i >= pickups.Count) continue;
                var pickup = pickups[i];
                if (!BallCollision.OverlapsCell(rules.arena, pickup.col, pickup.row, PickupInset, b.position, b.radius)) continue;
                b.idleWallBounces = 0;
                ops.CollectPickup(run, pickup, b.id, events);
            }
        }

        public bool TryTeleport(RunState run, BallSlot b, List<SimEvent> events)
        {
            if (b.portalLock > 0f) return false;
            var index = PortalIndexAt(run.board, b.position, b.radius);
            if (index < 0) return false;
            var objects = run.board.fieldObjects;
            var portal = objects[index];
            var partner = FindObject(objects, portal.pairId);
            if (partner == null) return false;
            var from = b.position;
            b.position = ArenaGeometry.CellCenter(rules.arena, partner.col, partner.row);
            b.portalLock = rules.balance.portalLockSeconds;
            b.idleWallBounces = 0;
            events.Add(new SimEvent
            {
                kind = SimEventKind.BallTeleported, ballId = b.id, ballType = b.type, sourceId = portal.id, targetId = partner.id,
                position = from, position2 = b.position,
            });
            return true;
        }

        public void UpdateMud(RunState run, BallSlot b, List<SimEvent> events)
        {
            var objects = run.board.fieldObjects;
            for (var i = 0; i < objects.Count; i++)
            {
                var mud = objects[i];
                if (mud.type != FieldObjectType.Mud) continue;
                if (!BallCollision.OverlapsCell(rules.arena, mud.col, mud.row, MudInset, b.position, b.radius)) continue;
                if (b.mudInsideId == mud.id) return;
                b.mudInsideId = mud.id;
                b.slowTimer = rules.balance.mudSlowSeconds;
                events.Add(new SimEvent { kind = SimEventKind.BallSlowed, ballId = b.id, ballType = b.type, sourceId = mud.id, position = b.position });
                return;
            }
            b.mudInsideId = -1;
        }

        public void PrunePierced(BoardState board, BallSlot b)
        {
            for (var i = b.piercedCount - 1; i >= 0; i--)
            {
                var enemy = FindEnemy(board.enemies, b.piercedIds[i]);
                if (enemy != null && BallCollision.CircleRect(BallCollision.EnemyRect(rules.arena, enemy), b.position, b.radius, out _, out _))
                {
                    continue;
                }
                b.piercedCount--;
                b.piercedIds[i] = b.piercedIds[b.piercedCount];
            }
        }

        public int PortalIndexAt(BoardState board, Vector2 position, float radius)
        {
            var objects = board.fieldObjects;
            for (var i = 0; i < objects.Count; i++)
            {
                var o = objects[i];
                if (o.type != FieldObjectType.Portal || o.pairId < 0) continue;
                if (BallCollision.OverlapsCell(rules.arena, o.col, o.row, PortalInset, position, radius)) return i;
            }
            return -1;
        }

        #endregion

        #region Helpers

        static FieldObjectState? FindObject(List<FieldObjectState> objects, int id)
        {
            for (var i = 0; i < objects.Count; i++)
            {
                if (objects[i].id == id) return objects[i];
            }
            return null;
        }

        static EnemyState? FindEnemy(List<EnemyState> enemies, int id)
        {
            for (var i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].id == id) return enemies[i];
            }
            return null;
        }

        #endregion
    }
}
