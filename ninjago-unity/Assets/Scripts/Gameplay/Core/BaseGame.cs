#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex
{
    public abstract class BaseGame : MonoBehaviour
    {
        // Read before Initialize: GameDriver configures detection for this many players.
        public abstract int NumOfPlayers { get; }

        #region Initialization

        public abstract void Initialize(DetectionManager detectionManager, ViewManager viewManager);

        #endregion

        #region Public API

        // Completes when the game is over and the player should return to game selection.
        public abstract UniTask RunAsync(CancellationToken cancellationToken);

        #endregion
    }
}
