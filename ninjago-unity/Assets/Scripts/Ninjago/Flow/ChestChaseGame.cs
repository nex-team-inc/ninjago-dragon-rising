#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Platform;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>Mini-game B: steer one shared car, then one shared skycraft, with the chest.</summary>
    public class ChestChaseGame : NinjagoGame
    {
        [Header("Runner View")]
        [SerializeField] RunnerView runnerViewPrefab = null!;
        [Header("Chase Config")]
        [SerializeField] ChaseConfig chaseConfig = null!;
        [Header("Result: Title")]
        [SerializeField] LocalizedString title = new();
        [Header("Result: Car Line")]
        [Tooltip("Smart string with {dodges} {hits}.")]
        [SerializeField] LocalizedString carLine = new();
        [Header("Result: Skycraft Line")]
        [Tooltip("Smart string with {dodges} {hits}.")]
        [SerializeField] LocalizedString skycraftLine = new();
        [Header("Result: Steer Share Line")]
        [Tooltip("Smart string with {player} {share} (percent).")]
        [SerializeField] LocalizedString steerLine = new();
        [Header("Result: Best Line")]
        [Tooltip("Smart string with {car} {sky} (best dodges).")]
        [SerializeField] LocalizedString bestLine = new();
        [Header("Result: New Best Line")]
        [SerializeField] LocalizedString newBestLine = new();
        [Header("Result: Best Color")]
        [SerializeField] Color bestColor = new(1f, 0.85f, 0.3f);

        protected override GameModeType Mode => GameModeType.ChestChase;

        #region Gameplay

        protected override async UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken)
        {
            var view = Instantiate(runnerViewPrefab);
            view.Initialize(activePlayers, chaseConfig, Detection, NumOfPlayers);
            await Views.ReplaceView(view);
            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.NinjaChase, cancellationToken: cancellationToken).Forget();
            if (!await view.RunAsync(cancellationToken)) return null;
            return BuildOutcome(view.World, activePlayers);
        }

        NinjagoOutcome BuildOutcome(RunnerWorld world, IReadOnlyList<PlayerBody> players)
        {
            var carDodges = world.GetDodges(VehicleType.Car);
            var carHits = world.GetHits(VehicleType.Car);
            var skyDodges = world.GetDodges(VehicleType.Skycraft);
            var skyHits = world.GetHits(VehicleType.Skycraft);
            var lines = new List<ResultLine.Data>
            {
                new(carLine, Color.white, ("dodges", carDodges), ("hits", carHits)),
                new(skycraftLine, Color.white, ("dodges", skyDodges), ("hits", skyHits)),
            };
            var details = new GameAnalyticsProperties
            {
                ["car_dodges"] = carDodges, ["car_hits"] = carHits, ["sky_dodges"] = skyDodges, ["sky_hits"] = skyHits,
            };
            foreach (var player in players)
            {
                var share = Mathf.RoundToInt(world.SteerShare(player.PlayerIndex) * 100f);
                lines.Add(new ResultLine.Data(steerLine, player.Color, ("player", player.PlayerIndex + 1), ("share", share)));
                details[$"p{player.PlayerIndex + 1}_steer_share"] = share;
            }

            var progress = PlayerDataManager.Instance.NinjagoProgress;
            var isNewBest = progress.chaseRuns > 0 && (carDodges > progress.chaseBestCarDodges || skyDodges > progress.chaseBestSkyDodges);
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(saved =>
            {
                saved.chaseRuns++;
                saved.chaseBestCarDodges = Mathf.Max(saved.chaseBestCarDodges, carDodges);
                saved.chaseBestSkyDodges = Mathf.Max(saved.chaseBestSkyDodges, skyDodges);
                saved.chaseFewestCarHits = saved.chaseFewestCarHits < 0 ? carHits : Mathf.Min(saved.chaseFewestCarHits, carHits);
                saved.chaseFewestSkyHits = saved.chaseFewestSkyHits < 0 ? skyHits : Mathf.Min(saved.chaseFewestSkyHits, skyHits);
            });

            if (isNewBest) lines.Add(new ResultLine.Data(newBestLine, bestColor));
            lines.Add(new ResultLine.Data(bestLine, Color.white, ("car", progress.chaseBestCarDodges), ("sky", progress.chaseBestSkyDodges)));
            return new NinjagoOutcome(title, lines, "complete", details);
        }

        #endregion
    }
}
