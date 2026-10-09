#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Platform;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>Mini-game A: slash the boss's rock with a hand cursor, then kick the pieces back with fast knee pulses.</summary>
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
        [Header("Result: Player Kicks Line")]
        [Tooltip("Smart string with {player} {kicks} {possible} {gap} (seconds).")]
        [SerializeField] LocalizedString kicksLine = new();
        [Header("Result: Player Kicks Line Without Gap")]
        [Tooltip("Smart string with {player} {kicks} {possible}, for a player who never kicked twice in one prompt.")]
        [SerializeField] LocalizedString kicksLineNoGap = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {slashes} {returns} {kicks}.")]
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
            int bestSlashes = 0, bestReturns = 0, bestKicks = 0;
            var bestGap = -1f;
            foreach (var lane in lanes)
            {
                anyStanding |= !lane.IsOut;
                bestSlashes = Mathf.Max(bestSlashes, lane.Slashes);
                bestReturns = Mathf.Max(bestReturns, lane.FullReturns);
                bestKicks = Mathf.Max(bestKicks, lane.KicksFired);
                var gap = lane.AverageKickGap;
                if (gap >= 0f && (bestGap < 0f || gap < bestGap)) bestGap = gap;
                var player = lane.PlayerIndex + 1;
                lines.Add(new ResultLine.Data(playerLine, lane.PlayerColor, ("player", player), ("slashes", lane.Slashes), ("throws", throws),
                    ("returns", lane.FullReturns), ("hearts", lane.Hearts), ("maxHearts", config.Hearts)));
                lines.Add(gap >= 0f
                    ? new ResultLine.Data(kicksLine, lane.PlayerColor, ("player", player), ("kicks", lane.KicksFired), ("possible", lane.KicksPossible), ("gap", gap))
                    : new ResultLine.Data(kicksLineNoGap, lane.PlayerColor, ("player", player), ("kicks", lane.KicksFired), ("possible", lane.KicksPossible)));
                var prefix = $"p{player}_";
                details[prefix + "slashes"] = lane.Slashes;
                details[prefix + "full_returns"] = lane.FullReturns;
                details[prefix + "kicks"] = lane.KicksFired;
                details[prefix + "kicks_possible"] = lane.KicksPossible;
                details[prefix + "average_kick_gap_s"] = Mathf.Round(gap * 100f) / 100f;
                details[prefix + "hearts"] = lane.Hearts;
                details[prefix + "out"] = lane.IsOut;
            }

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var isNewBest = progress.stoneKickRuns > 0 && (bestSlashes > progress.stoneKickBestSlashes ||
                bestReturns > progress.stoneKickBestFullReturns || bestKicks > progress.stoneKickBestKicks);
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.stoneKickRuns++;
                saved.stoneKickBestSlashes = Mathf.Max(saved.stoneKickBestSlashes, bestSlashes);
                saved.stoneKickBestFullReturns = Mathf.Max(saved.stoneKickBestFullReturns, bestReturns);
                saved.stoneKickBestKicks = Mathf.Max(saved.stoneKickBestKicks, bestKicks);
                if (bestGap >= 0f && (saved.stoneKickBestAverageKickGap < 0f || bestGap < saved.stoneKickBestAverageKickGap))
                {
                    saved.stoneKickBestAverageKickGap = bestGap;
                }
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            lines.Add(new ResultLine.Data(bestLine, Color.white, ("slashes", progress.stoneKickBestSlashes),
                ("returns", progress.stoneKickBestFullReturns), ("kicks", progress.stoneKickBestKicks)));
            details["throws"] = throws;
            details["kicks_required"] = config.KicksRequired;
            return new NinjagoOutcome(anyStanding ? victoryTitle : defeatTitle, lines, anyStanding ? "win" : "lose", details);
        }

        #endregion
    }
}
