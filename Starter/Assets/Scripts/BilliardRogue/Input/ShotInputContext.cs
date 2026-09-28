#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>What ShotInputRouter.Initialize needs to wire the paw, debug and bot inputs of one PlayerShotInput.</summary>
    public sealed class ShotInputContext
    {
        public ControlConfig control = null!;
        /// <summary>Arena clamp for every input and the bot's private PredictPath simulator.</summary>
        public GameRules rules = null!;
        /// <summary>PlayerPreference.leftHandedCue at creation; ShotInputRouter.SetLeftHanded applies later changes.</summary>
        public bool leftHanded;
        /// <summary>The run the bot aims at; null outside gameplay (calibration), where the bot shoots straight up.</summary>
        public Func<RunState?> run = NoRun;
        /// <summary>Optional: lets the debug input aim at the mouse on the arena (WorldCamera renders full screen).</summary>
        public Camera? worldCamera;
        /// <summary>Optional: the arena frame the mouse ray is intersected with.</summary>
        public ArenaLayout? layout;

        static RunState? NoRun() => null;
    }
}
