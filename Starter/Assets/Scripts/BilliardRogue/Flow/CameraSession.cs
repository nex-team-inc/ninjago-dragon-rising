#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// Implemented by UI & Flow module.
namespace Nex.BilliardRogue
{
    /// <summary>
    /// Owns the DetectionManager instance for a flow session: created before Calibration for numPlayers, one
    /// hidden-node OnePlayerDetectionEngine per player, disposed when returning to the title. A player-count change
    /// destroys and re-instantiates the prefab (TDD D1).
    /// </summary>
    public sealed class CameraSession : MonoBehaviour
    {
        DetectionManager detectionPrefab = null!;
        OnePlayerDetectionEngine enginePrefab = null!;
        Transform root = null!;

        public DetectionManager Detection => throw new NotImplementedException("UI & Flow module");
        public int NumPlayers { get; private set; }
        public bool IsRunning { get; private set; }

        public void Initialize(DetectionManager aDetectionPrefab, OnePlayerDetectionEngine aEnginePrefab, Transform aRoot)
        {
            detectionPrefab = aDetectionPrefab;
            enginePrefab = aEnginePrefab;
            root = aRoot;
        }

        /// <summary>Instantiates detection for numPlayers, waits for the first detection and configures for setup.</summary>
        public UniTask StartAsync(int numPlayers, CancellationToken ct)
        {
            NumPlayers = numPlayers;
            throw new NotImplementedException("UI & Flow module");
        }

        public OnePlayerDetectionEngine GetEngine(int playerIndex)
        {
            throw new NotImplementedException("UI & Flow module");
        }

        public void Stop()
        {
            IsRunning = false;
        }
    }
}
