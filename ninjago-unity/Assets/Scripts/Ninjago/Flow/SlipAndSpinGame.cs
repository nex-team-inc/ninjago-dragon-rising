#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Platform;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>Mini-game A: dodge the staff with a chest lean, then fill the Spinjitzu swirl with wild hands.</summary>
    public class SlipAndSpinGame : NinjagoGame
    {
        [Header("Fight View")]
        [SerializeField] FightView fightViewPrefab = null!;
        [Header("Fight Config")]
        [SerializeField] FightConfig fightConfig = null!;
        [Header("Hand Storm Sampler")]
        [SerializeField] HandStormSampler storms = null!;
        [Header("Result: Victory Title")]
        [SerializeField] LocalizedString victoryTitle = new();
        [Header("Result: Defeat Title")]
        [SerializeField] LocalizedString defeatTitle = new();
        [Header("Result: Player Line")]
        [Tooltip("Smart string with {player} {slips} {sweeps} {backs} {hearts} {maxHearts}.")]
        [SerializeField] LocalizedString playerLine = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {slips} {backs} {hearts}.")]
        [SerializeField] LocalizedString bestLine = new();
        [Header("Result: New Best Line")]
        [SerializeField] LocalizedString newBestLine = new();
        [Header("Result: Best Color")]
        [SerializeField] Color bestColor = new(1f, 0.85f, 0.3f);

        protected override GameModeType Mode => GameModeType.SlipAndSpin;

        #region Initialization

        public override void Initialize(DetectionManager detectionManager, ViewManager viewManager)
        {
            base.Initialize(detectionManager, viewManager);
            storms.Initialize(detectionManager, fightConfig);
        }

        #endregion

        #region Gameplay

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            var view = Instantiate(fightViewPrefab);
            view.Initialize(activePlayers, fightConfig, storms, Detection, NumOfPlayers);
            await Views.ReplaceView(view);
            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.NinjaFight, cancellationToken: cancellationToken).Forget();
            if (!await view.RunAsync(cancellationToken)) return null;
            return BuildOutcome(view.Lanes);
        }

        NinjagoOutcome BuildOutcome(IReadOnlyList<FightLane> lanes)
        {
            var sweeps = fightConfig.SweepsPerPlayer;
            var maxHearts = fightConfig.Hearts;
            var lines = new List<ResultLine.Data>();
            var details = new GameAnalyticsProperties();
            var anyStanding = false;
            int bestSlips = 0, bestBacks = 0, bestHearts = 0;
            foreach (var lane in lanes)
            {
                anyStanding |= !lane.IsOut;
                bestSlips = Mathf.Max(bestSlips, lane.Slips);
                bestBacks = Mathf.Max(bestBacks, lane.FightBacks);
                bestHearts = Mathf.Max(bestHearts, lane.Hearts);
                lines.Add(new ResultLine.Data(playerLine, lane.PlayerColor,
                    ("player", lane.PlayerIndex + 1), ("slips", lane.Slips), ("sweeps", sweeps), ("backs", lane.FightBacks),
                    ("hearts", lane.Hearts), ("maxHearts", maxHearts)));
                var prefix = $"p{lane.PlayerIndex + 1}_";
                details[prefix + "slips"] = lane.Slips;
                details[prefix + "fight_backs"] = lane.FightBacks;
                details[prefix + "hearts"] = lane.Hearts;
                details[prefix + "out"] = lane.IsOut;
            }

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var isNewBest = progress.fightRuns > 0 && (bestSlips > progress.fightBestSlips || bestBacks > progress.fightBestFightBacks || bestHearts > progress.fightBestHearts);
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.fightRuns++;
                saved.fightBestSlips = Mathf.Max(saved.fightBestSlips, bestSlips);
                saved.fightBestFightBacks = Mathf.Max(saved.fightBestFightBacks, bestBacks);
                saved.fightBestHearts = Mathf.Max(saved.fightBestHearts, bestHearts);
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            lines.Add(new ResultLine.Data(bestLine, Color.white, ("slips", progress.fightBestSlips),
                ("backs", progress.fightBestFightBacks), ("hearts", progress.fightBestHearts)));
            details["sweeps"] = sweeps;
            return new NinjagoOutcome(anyStanding ? victoryTitle : defeatTitle, lines, anyStanding ? "win" : "lose", details);
        }

        #endregion
    }
}
