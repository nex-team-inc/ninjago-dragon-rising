#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Aggregates the player-turn events of every fired ball (hits, damage, wall bounces, kills, best combo) into one
    /// RunAnalytics.ShotResult per ball when it exits. Splitter minis carry ids the tracker never saw launched, so
    /// they are attributed to the newest parent that still has open minis; kills are attributed to the last ball
    /// that hit the enemy in the same event batch (EnemyKilled carries no ball id). Fixed slots: no allocation.
    /// </summary>
    public sealed class ShotResultTracker
    {
        const int Capacity = 96;

        sealed class Entry
        {
            public bool active;
            public int ballId;
            public BallType type;
            public int hits;
            public int damage;
            public int bounces;
            public int kills;
            public int combo;
            public int openMinis;
        }

        readonly Entry[] entries = new Entry[Capacity];
        readonly SessionAnalytics analytics;
        int lastHitTarget;
        int lastHitBall;

        public ShotResultTracker(SessionAnalytics aAnalytics)
        {
            analytics = aAnalytics;
            for (var i = 0; i < Capacity; i++)
            {
                entries[i] = new Entry();
            }
        }

        #region Public Methods

        public void Consume(List<SimEvent> events)
        {
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                switch (ev.kind)
                {
                    case SimEventKind.BallLaunched:
                        Begin(ev.ballId, ev.ballType);
                        break;
                    case SimEventKind.EnemyHit:
                        OnHit(ev);
                        break;
                    case SimEventKind.EnemyKilled:
                        OnKilled(ev);
                        break;
                    case SimEventKind.BallWallBounce:
                        OnBounce(ev.ballId, ev.value);
                        break;
                    case SimEventKind.ComboChanged:
                        OnCombo(ev.ballId, ev.value);
                        break;
                    case SimEventKind.BallSplit:
                        OnSplit(ev.ballId, ev.value);
                        break;
                    case SimEventKind.BallExited:
                        OnExited(ev);
                        break;
                }
            }
        }

        /// <summary>Reports every open entry (turn end, stage jump).</summary>
        public void Flush()
        {
            for (var i = 0; i < Capacity; i++)
            {
                if (entries[i].active) Report(entries[i]);
            }

            lastHitTarget = 0;
            lastHitBall = 0;
        }

        #endregion

        #region Events

        void OnHit(in SimEvent ev)
        {
            var entry = Resolve(ev.ballId);
            lastHitTarget = ev.targetId;
            lastHitBall = entry != null ? entry.ballId : 0;
            if (entry == null) return;
            entry.hits++;
            entry.damage += ev.value;
        }

        void OnKilled(in SimEvent ev)
        {
            if (ev.targetId != lastHitTarget || lastHitBall == 0) return;
            var entry = Find(lastHitBall);
            if (entry != null) entry.kills++;
        }

        void OnBounce(int ballId, int wallBounces)
        {
            var entry = Resolve(ballId);
            if (entry != null) entry.bounces = Mathf.Max(entry.bounces, wallBounces);
        }

        void OnCombo(int ballId, int combo)
        {
            var entry = Resolve(ballId);
            if (entry != null) entry.combo = Mathf.Max(entry.combo, combo);
        }

        void OnSplit(int ballId, int miniCount)
        {
            var entry = Find(ballId);
            if (entry != null) entry.openMinis = miniCount;
        }

        void OnExited(in SimEvent ev)
        {
            var entry = ev.flag ? Resolve(ev.ballId) : Find(ev.ballId);
            if (entry == null) return;
            entry.combo = Mathf.Max(entry.combo, ev.value);
            entry.bounces = Mathf.Max(entry.bounces, ev.value2);
            if (ev.flag) entry.openMinis--;
            if (entry.openMinis <= 0) Report(entry);
        }

        #endregion

        #region Helpers

        void Begin(int ballId, BallType type)
        {
            for (var i = 0; i < Capacity; i++)
            {
                var entry = entries[i];
                if (entry.active) continue;
                entry.active = true;
                entry.ballId = ballId;
                entry.type = type;
                entry.hits = 0;
                entry.damage = 0;
                entry.bounces = 0;
                entry.kills = 0;
                entry.combo = 0;
                entry.openMinis = 0;
                return;
            }
        }

        Entry? Find(int ballId)
        {
            for (var i = 0; i < Capacity; i++)
            {
                var entry = entries[i];
                if (entry.active && entry.ballId == ballId) return entry;
            }

            return null;
        }

        // Unknown ids belong to split minis: the newest parent with open minis owns them.
        Entry? Resolve(int ballId)
        {
            var found = Find(ballId);
            if (found != null) return found;
            Entry? best = null;
            for (var i = 0; i < Capacity; i++)
            {
                var entry = entries[i];
                if (!entry.active || entry.openMinis <= 0) continue;
                if (best == null || entry.ballId > best.ballId) best = entry;
            }

            return best;
        }

        void Report(Entry entry)
        {
            analytics.ShotResult(entry.type, entry.hits, entry.damage, entry.bounces, entry.kills, entry.combo);
            entry.active = false;
        }

        #endregion
    }
}
