#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Platform;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>Mini-game A: slash the boss's rock into stones in a cursor frenzy, then send every stone back with one kick.</summary>
    public class StoneKickGame : HandCursorGame
    {
        [Header("Stone Kick View")]
        [SerializeField] StoneKickView viewPrefab = null!;
        [Header("Stone Kick Config")]
        [SerializeField] StoneKickConfig config = null!;
        [Header("Result: Victory Title")]
        [SerializeField] LocalizedString victoryTitle = new();
        [Header("Result: Defeat Title")]
        [SerializeField] LocalizedString defeatTitle = new();
        [Header("Result: Player Line")]
        [Tooltip("Smart string with {player} {slashes} {throws} {returns} {hearts} {maxHearts}.")]
        [SerializeField] LocalizedString playerLine = new();
        [Header("Result: Player Stones Line")]
        [Tooltip("Smart string with {player} {stones} {cut} {time} (seconds from KICK to the kick).")]
        [SerializeField] LocalizedString stonesLine = new();
        [Header("Result: Player Stones Line Without Kicks")]
        [Tooltip("Smart string with {player} {stones} {cut}, for a player who never kicked in time.")]
        [SerializeField] LocalizedString stonesLineNoKick = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {slashes} {returns} {stones}.")]
        [SerializeField] LocalizedString bestLine = new();
        [Header("Result: New Best Line")]
        [SerializeField] LocalizedString newBestLine = new();
        [Header("Result: Best Color")]
        [SerializeField] Color bestColor = new(1f, 0.85f, 0.3f);

        protected override GameModeType Mode => GameModeType.StoneKick;

        #region Gameplay

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            var view = Instantiate(viewPrefab);
            view.Initialize(activePlayers, config, Cursors, Detection, NumOfPlayers);
            await Views.ReplaceView(view);
            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.StoneKick, cancellationToken: cancellationToken).Forget();
            if (!await view.RunAsync(cancellationToken)) return null;
            return BuildOutcome(view.Lanes);
        }

        NinjagoOutcome BuildOutcome(IReadOnlyList<StoneKickLane> lanes)
        {
            var throws = config.ThrowsPerPlayer;
            var lines = new List<ResultLine.Data>();
            var details = new GameAnalyticsProperties();
            var anyStanding = false;
            int bestSlashes = 0, bestReturns = 0, bestStones = 0;
            var bestKickTime = -1f;
            foreach (var lane in lanes)
            {
                anyStanding |= !lane.IsOut;
                bestSlashes = Mathf.Max(bestSlashes, lane.Slashes);
                bestReturns = Mathf.Max(bestReturns, lane.Returns);
                bestStones = Mathf.Max(bestStones, lane.StonesReturned);
                var kickTime = lane.AverageKickTime;
                if (kickTime >= 0f && (bestKickTime < 0f || kickTime < bestKickTime)) bestKickTime = kickTime;
                var player = lane.PlayerIndex + 1;
                lines.Add(new ResultLine.Data(playerLine, lane.PlayerColor, ("player", player), ("slashes", lane.Slashes), ("throws", throws),
                    ("returns", lane.Returns), ("hearts", lane.Hearts), ("maxHearts", config.Hearts)));
                lines.Add(kickTime >= 0f
                    ? new ResultLine.Data(stonesLine, lane.PlayerColor, ("player", player), ("stones", lane.StonesReturned), ("cut", lane.StonesCut), ("time", kickTime))
                    : new ResultLine.Data(stonesLineNoKick, lane.PlayerColor, ("player", player), ("stones", lane.StonesReturned), ("cut", lane.StonesCut)));
                var prefix = $"p{player}_";
                details[prefix + "slashes"] = lane.Slashes;
                details[prefix + "returns"] = lane.Returns;
                details[prefix + "stones_cut"] = lane.StonesCut;
                details[prefix + "stones_returned"] = lane.StonesReturned;
                details[prefix + "average_kick_s"] = Mathf.Round(kickTime * 100f) / 100f;
                details[prefix + "hearts"] = lane.Hearts;
                details[prefix + "out"] = lane.IsOut;
            }

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var isNewBest = progress.stoneKickRuns > 0 && (bestSlashes > progress.stoneKickBestSlashes ||
                bestReturns > progress.stoneKickBestFullReturns || bestStones > progress.stoneKickBestStones);
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.stoneKickRuns++;
                saved.stoneKickBestSlashes = Mathf.Max(saved.stoneKickBestSlashes, bestSlashes);
                saved.stoneKickBestFullReturns = Mathf.Max(saved.stoneKickBestFullReturns, bestReturns);
                saved.stoneKickBestStones = Mathf.Max(saved.stoneKickBestStones, bestStones);
                if (bestKickTime >= 0f && (saved.stoneKickBestKickTime < 0f || bestKickTime < saved.stoneKickBestKickTime))
                {
                    saved.stoneKickBestKickTime = bestKickTime;
                }
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            lines.Add(new ResultLine.Data(bestLine, Color.white, ("slashes", progress.stoneKickBestSlashes),
                ("returns", progress.stoneKickBestFullReturns), ("stones", progress.stoneKickBestStones)));
            details["throws"] = throws;
            details["max_stones"] = config.MaxStones;
            return new NinjagoOutcome(anyStanding ? victoryTitle : defeatTitle, lines, anyStanding ? "win" : "lose", details);
        }

        #endregion
    }
}
