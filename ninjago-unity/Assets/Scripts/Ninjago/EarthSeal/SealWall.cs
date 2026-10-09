#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The Earth Seal wall rules for one wave, free of Unity objects. Each beat every intact tile rolls the crack
    /// chance; a crack warns, then its monster pushes until the breakthrough timer ends. While any cursor is inside a
    /// live crack its seal fills at one rate however many cursors there are, and drains slowly once all have left.
    /// </summary>
    public sealed class SealWall
    {
        public enum TileState
        {
            Intact,
            Warning,
            Pushing,
            Sealed,
            Broken,
        }

        public sealed class Tile
        {
            public int X { get; internal set; }
            public int Y { get; internal set; }
            public TileState State { get; internal set; }
            /// <summary>Seconds in the current Warning or Pushing phase.</summary>
            public float PhaseTime { get; internal set; }
            /// <summary>Seconds since this crack started.</summary>
            public float Age { get; internal set; }
            public float Fill { get; internal set; }
            public float PeakFill { get; internal set; }
            public bool Dropped { get; internal set; }
            /// <summary>Most recent player with a cursor on this crack; -1 before anyone touched it.</summary>
            public int LastHolder { get; internal set; } = -1;
            public bool IsLive => State is TileState.Warning or TileState.Pushing;
        }

        public enum EventType
        {
            CrackStarted,
            PushStarted,
            SealCompleted,
            SealDropped,
            Breakthrough,
        }

        public readonly struct Event
        {
            public readonly EventType type;
            public readonly int tile;
            /// <summary>Bit per player with a cursor on the tile (SealCompleted).</summary>
            public readonly int holderMask;
            /// <summary>Cursors on the tile (SealCompleted).</summary>
            public readonly int cursorCount;
            /// <summary>True for a SealDropped caused by the breakthrough rather than by draining to nothing.</summary>
            public readonly bool atBreakthrough;

            public Event(EventType type, int tile, int holderMask = 0, int cursorCount = 0, bool atBreakthrough = false)
            {
                this.type = type;
                this.tile = tile;
                this.holderMask = holderMask;
                this.cursorCount = cursorCount;
                this.atBreakthrough = atBreakthrough;
            }
        }

        readonly List<Tile> tiles = new();
        readonly List<int> rollOrder = new();
        EarthSealConfig.WaveSettings wave = null!;
        System.Random random = null!;
        float waveTime;
        float beatTimer;

        public int GridSize { get; private set; }
        public IReadOnlyList<Tile> Tiles => tiles;
        public int LiveCount { get; private set; }
        public bool IsWaveOver => LiveCount == 0 && (waveTime >= wave.waveSeconds || !AnyIntact());

        #region Public API

        public void BeginWave(EarthSealConfig.WaveSettings settings, System.Random aRandom)
        {
            wave = settings;
            random = aRandom;
            waveTime = 0f;
            beatTimer = 0f;
            LiveCount = 0;
            GridSize = settings.gridSize;
            tiles.Clear();
            rollOrder.Clear();
            for (var y = 0; y < GridSize; y++)
            {
                for (var x = 0; x < GridSize; x++)
                {
                    rollOrder.Add(tiles.Count);
                    tiles.Add(new Tile { X = x, Y = y });
                }
            }
        }

        /// <param name="cursorCounts">Cursors inside each tile this frame (index = tile).</param>
        /// <param name="holderMasks">Bit per player with a cursor inside each tile this frame.</param>
        public void Tick(float deltaTime, IReadOnlyList<int> cursorCounts, IReadOnlyList<int> holderMasks, float drainSeconds, List<Event> events)
        {
            waveTime += deltaTime;
            if (waveTime < wave.waveSeconds)
            {
                beatTimer += deltaTime;
                while (beatTimer >= wave.beatSeconds)
                {
                    beatTimer -= wave.beatSeconds;
                    RollBeat(events);
                }
            }

            for (var i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].IsLive) TickCrack(i, deltaTime, cursorCounts[i], holderMasks[i], drainSeconds, events);
            }
        }

        #endregion

        #region Rules

        void RollBeat(List<Event> events)
        {
            for (var i = rollOrder.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (rollOrder[i], rollOrder[j]) = (rollOrder[j], rollOrder[i]);
            }

            foreach (var index in rollOrder)
            {
                if (LiveCount >= wave.maxCracks) return;
                var tile = tiles[index];
                if (tile.State != TileState.Intact || random.NextDouble() >= wave.crackChance || !FarFromLiveCracks(tile)) continue;
                tile.State = TileState.Warning;
                tile.PhaseTime = 0f;
                tile.Age = 0f;
                LiveCount++;
                events.Add(new Event(EventType.CrackStarted, index));
            }
        }

        void TickCrack(int index, float deltaTime, int cursorCount, int holderMask, float drainSeconds, List<Event> events)
        {
            var tile = tiles[index];
            tile.PhaseTime += deltaTime;
            tile.Age += deltaTime;
            if (cursorCount > 0)
            {
                if (tile.LastHolder < 0 || (holderMask & (1 << tile.LastHolder)) == 0) tile.LastHolder = LowestPlayer(holderMask);
                tile.Fill = Mathf.Min(1f, tile.Fill + deltaTime / wave.sealSeconds);
                tile.PeakFill = Mathf.Max(tile.PeakFill, tile.Fill);
                if (tile.Fill >= 1f)
                {
                    Resolve(tile, TileState.Sealed);
                    events.Add(new Event(EventType.SealCompleted, index, holderMask, cursorCount));
                    return;
                }
            }
            else if (tile.Fill > 0f)
            {
                tile.Fill = Mathf.Max(0f, tile.Fill - deltaTime / drainSeconds);
                if (tile.Fill <= 0f) Drop(tile, index, false, events);
            }

            if (tile.State == TileState.Warning && tile.PhaseTime >= wave.warningSeconds)
            {
                tile.State = TileState.Pushing;
                tile.PhaseTime = 0f;
                events.Add(new Event(EventType.PushStarted, index));
            }
            else if (tile.State == TileState.Pushing && tile.PhaseTime >= wave.breakthroughSeconds)
            {
                if (tile.Fill > 0f) Drop(tile, index, true, events);
                Resolve(tile, TileState.Broken);
                events.Add(new Event(EventType.Breakthrough, index));
            }
        }

        static void Drop(Tile tile, int index, bool atBreakthrough, List<Event> events)
        {
            if (tile.Dropped) return;
            tile.Dropped = true;
            events.Add(new Event(EventType.SealDropped, index, atBreakthrough: atBreakthrough));
        }

        void Resolve(Tile tile, TileState state)
        {
            tile.State = state;
            LiveCount--;
        }

        bool FarFromLiveCracks(Tile candidate)
        {
            foreach (var tile in tiles)
            {
                if (!tile.IsLive) continue;
                if (Mathf.Max(Mathf.Abs(tile.X - candidate.X), Mathf.Abs(tile.Y - candidate.Y)) < wave.minCrackSpacing) return false;
            }

            return true;
        }

        bool AnyIntact()
        {
            foreach (var tile in tiles)
            {
                if (tile.State == TileState.Intact) return true;
            }

            return false;
        }

        static int LowestPlayer(int mask)
        {
            for (var player = 0; player < 31; player++)
            {
                if ((mask & (1 << player)) != 0) return player;
            }

            return -1;
        }

        #endregion
    }
}
