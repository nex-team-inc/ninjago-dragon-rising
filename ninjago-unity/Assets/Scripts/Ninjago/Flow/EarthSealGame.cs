#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Platform;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>Mini-game B: park hand cursors on cracking wall tiles and hold them until the earth seal fills.</summary>
    public class EarthSealGame : HandCursorGame
    {
        [Header("Earth Seal View")]
        [SerializeField] EarthSealView viewPrefab = null!;
        [Header("Earth Seal Config")]
        [SerializeField] EarthSealConfig config = null!;
        [Header("Result: Wall Held Title")]
        [SerializeField] LocalizedString heldTitle = new();
        [Header("Result: Wall Broke Title")]
        [SerializeField] LocalizedString brokeTitle = new();
        [Header("Result: Wall Line")]
        [Tooltip("Smart string with {seals} {breaks} {hearts} {maxHearts}.")]
        [SerializeField] LocalizedString wallLine = new();
        [Header("Result: Teamwork Line")]
        [Tooltip("Smart string with {twoHands} {drops}.")]
        [SerializeField] LocalizedString teamworkLine = new();
        [Header("Result: Player Line")]
        [Tooltip("Smart string with {player} {held} {seconds} {drops}.")]
        [SerializeField] LocalizedString playerLine = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {seals} {twoHands}.")]
        [SerializeField] LocalizedString bestLine = new();
        [Header("Result: New Best Line")]
        [SerializeField] LocalizedString newBestLine = new();
        [Header("Result: Best Color")]
        [SerializeField] Color bestColor = new(1f, 0.85f, 0.3f);

        protected override GameModeType Mode => GameModeType.EarthSeal;

        #region Gameplay

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            var view = Instantiate(viewPrefab);
            view.Initialize(activePlayers, config, Cursors, Detection, NumOfPlayers);
            await Views.ReplaceView(view);
            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.EarthSeal, cancellationToken: cancellationToken).Forget();
            if (!await view.RunAsync(cancellationToken)) return null;
            return BuildOutcome(view.World, activePlayers);
        }

        NinjagoOutcome BuildOutcome(EarthSealWorld world, IReadOnlyList<PlayerBody> players)
        {
            var stats = world.Stats;
            var lines = new List<ResultLine.Data>
            {
                new(wallLine, Color.white, ("seals", stats.Seals), ("breaks", stats.Breakthroughs), ("hearts", world.Hearts), ("maxHearts", config.Hearts)),
                new(teamworkLine, Color.white, ("twoHands", stats.TwoHandSeals), ("drops", stats.DroppedSeals)),
            };
            var details = new GameAnalyticsProperties
            {
                ["seals"] = stats.Seals, ["breakthroughs"] = stats.Breakthroughs, ["two_hand_seals"] = stats.TwoHandSeals,
                ["dropped_seals"] = stats.DroppedSeals, ["hearts"] = world.Hearts, ["waves_cleared"] = stats.WavesCleared,
            };
            foreach (var player in players)
            {
                var index = player.PlayerIndex;
                lines.Add(new ResultLine.Data(playerLine, player.Color, ("player", index + 1), ("held", stats.GetSealsHeld(index)),
                    ("seconds", stats.GetHoldSeconds(index)), ("drops", stats.GetDrops(index))));
                var prefix = $"p{index + 1}_";
                details[prefix + "seals_held"] = stats.GetSealsHeld(index);
                details[prefix + "hold_s"] = Mathf.Round(stats.GetHoldSeconds(index) * 10f) / 10f;
                details[prefix + "drops"] = stats.GetDrops(index);
            }

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var fewerBreaks = world.Finished && (progress.earthSealFewestBreakthroughs < 0 || stats.Breakthroughs < progress.earthSealFewestBreakthroughs);
            var isNewBest = progress.earthSealRuns > 0 && (stats.Seals > progress.earthSealBestSeals || fewerBreaks);
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.earthSealRuns++;
                saved.earthSealBestSeals = Mathf.Max(saved.earthSealBestSeals, stats.Seals);
                saved.earthSealBestHearts = Mathf.Max(saved.earthSealBestHearts, world.Hearts);
                saved.earthSealBestTwoHandSeals = Mathf.Max(saved.earthSealBestTwoHandSeals, stats.TwoHandSeals);
                if (fewerBreaks) saved.earthSealFewestBreakthroughs = stats.Breakthroughs;
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            lines.Add(new ResultLine.Data(bestLine, Color.white, ("seals", progress.earthSealBestSeals), ("twoHands", progress.earthSealBestTwoHandSeals)));
            var result = world.Finished ? "win" : "lose";
            return new NinjagoOutcome(world.Finished ? heldTitle : brokeTitle, lines, result, details);
        }

        #endregion
    }
}
