#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's courtyard: a ninja, a brute and a camera rendering into that player's screen half. Runs the
    /// exchange loop for that player only: telegraph → slip (chest lean) → staff hit or miss → counter (hand storm
    /// fills the swirl under local slow motion) → spin and brick spray, or a whiff.
    /// </summary>
    public class FightLane : MonoBehaviour
    {
        [Header("Ninja")]
        [SerializeField] NinjaRig ninja = null!;
        [Header("Brute")]
        [SerializeField] BruteRig brute = null!;
        [Header("Brick Spray Point")]
        [SerializeField] Transform bruteChest = null!;
        [Header("Lane Camera")]
        [SerializeField] Camera laneCamera = null!;
        [Header("Solo Field Of View")]
        [SerializeField] float soloFieldOfView = 52f;
        [Header("Split Field Of View")]
        [SerializeField] float splitFieldOfView = 64f;
        [Header("Staff Rest Lift")]
        [SerializeField] float restLiftDegrees = 70f;
        [Header("Staff Cock Yaw")]
        [Tooltip("How far the staff winds up toward the danger side during the telegraph.")]
        [SerializeField] float cockYawDegrees = 85f;
        [Header("Staff Cock Lift")]
        [SerializeField] float cockLiftDegrees = 20f;

        readonly LocalClock clock = new();
        readonly SlipDetector slipDetector = new();
        PlayerBody body = null!;
        FightConfig config = null!;
        HandStormSampler storms = null!;
        FightHud hud = null!;
        SweepSide firstSafeSide;

        public int PlayerIndex => body.PlayerIndex;
        public Color PlayerColor => body.Color;
        public int Hearts { get; private set; }
        public int Slips { get; private set; }
        public int FightBacks { get; private set; }
        public bool IsOut { get; private set; }

        #region Initialization

        public void Initialize(PlayerBody aBody, FightConfig aConfig, HandStormSampler aStorms, FightHud aHud,
            RenderTexture target, bool splitScreen, SweepSide aFirstSafeSide)
        {
            body = aBody;
            config = aConfig;
            storms = aStorms;
            hud = aHud;
            firstSafeSide = aFirstSafeSide;
            laneCamera.targetTexture = target;
            laneCamera.fieldOfView = splitScreen ? splitFieldOfView : soloFieldOfView;
            ninja.Initialize(body.PlayerIndex, body.Color);
            Hearts = config.Hearts;
            hud.Initialize(body.PlayerIndex, body.Color, Hearts);
            brute.SetStaff(0f, restLiftDegrees);
            brute.SetTrail(false);
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            clock.Tick();
            var deltaTime = clock.DeltaTime;
            ninja.Tick(deltaTime);
            brute.Tick(deltaTime);
        }

        #endregion

        #region Exchange Loop

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            var sweeps = config.SweepsPerPlayer;
            hud.SetSweep(1, sweeps);
            await WaitLocal(config.IntroSeconds, cancellationToken);
            var safeSide = firstSafeSide;
            for (var sweep = 0; sweep < sweeps; sweep++)
            {
                hud.SetSweep(sweep + 1, sweeps);
                await TelegraphAsync(safeSide, sweep == 0, cancellationToken);
                if (await SlipAsync(safeSide, sweep, cancellationToken))
                {
                    Slips++;
                    if (await CounterAsync(sweep, Slips == 1, cancellationToken)) FightBacks++;
                }
                else if (IsOut)
                {
                    return;
                }

                if (sweep < sweeps - 1) await WaitLocal(config.RestBetweenSweepsSeconds, cancellationToken);
                safeSide = safeSide.Opposite();
            }

            hud.ShowFinished();
        }

        async UniTask TelegraphAsync(SweepSide safeSide, bool firstSweep, CancellationToken cancellationToken)
        {
            hud.ShowSafeSide(safeSide, firstSweep);
            var cockYaw = CockYaw(safeSide);
            await AnimateLocal(config.TelegraphSeconds, t =>
            {
                var eased = 1f - (1f - t) * (1f - t);
                brute.SetStaff(Mathf.Lerp(0f, cockYaw, eased), Mathf.Lerp(restLiftDegrees, cockLiftDegrees, eased));
                FollowLean();
            }, cancellationToken);
        }

        // Real-time window. The staff reaches the center line at the end of it, or sooner once the player has slipped.
        async UniTask<bool> SlipAsync(SweepSide safeSide, int sweep, CancellationToken cancellationToken)
        {
            NinjagoAnalytics.SlipStart(PlayerIndex, sweep, safeSide);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.StaffWhoosh);
            slipDetector.Begin(safeSide, config.LeanThresholdInches);
            brute.SetTrail(true);
            var cockYaw = CockYaw(safeSide);
            var elapsed = 0f;
            var progress = 0f;
            var progressPerSecond = 1f / config.SlipWindowSeconds;
            var tracked = false;
            var decidedAt = 0f;
            var outcome = SlipDetector.Outcome.Pending;
            while (progress < 1f)
            {
                var deltaTime = Time.unscaledDeltaTime;
                elapsed += deltaTime;
                if (outcome == SlipDetector.Outcome.Pending && body.TryGetLeanInches(out var lean))
                {
                    tracked = true;
                    outcome = slipDetector.Update(lean.x);
                    ninja.SetLeanHint(lean.x / config.LeanThresholdInches);
                    if (outcome != SlipDetector.Outcome.Pending)
                    {
                        decidedAt = elapsed;
                        OnSlipDecided(outcome, safeSide, ref progressPerSecond, progress);
                    }
                }

                progress = Mathf.Min(1f, progress + deltaTime * progressPerSecond);
                brute.SetStaff(Mathf.Lerp(cockYaw, 0f, progress * progress), Mathf.Lerp(cockLiftDegrees, 0f, progress));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            brute.SetTrail(false);
            hud.HideSafeSide();
            if (outcome == SlipDetector.Outcome.Slipped)
            {
                NinjagoAnalytics.SlipSuccess(PlayerIndex, sweep, safeSide, decidedAt);
                brute.SetStance(1f);
                return true;
            }

            var reason = outcome == SlipDetector.Outcome.WrongWay ? "wrong_way" : tracked ? "no_lean" : "not_tracked";
            await StaffHitAsync(safeSide, sweep, reason, cancellationToken);
            return false;
        }

        void OnSlipDecided(SlipDetector.Outcome outcome, SweepSide safeSide, ref float progressPerSecond, float progress)
        {
            ninja.SlipTo(outcome == SlipDetector.Outcome.Slipped ? safeSide : safeSide.Opposite());
            if (outcome != SlipDetector.Outcome.Slipped) return;
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.NinjaSlip);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.NinjaDustKick, ninja.FeetPosition);
            progressPerSecond = (1f - progress) / config.SlipResolveSeconds;
        }

        async UniTask StaffHitAsync(SweepSide safeSide, int sweep, string reason, CancellationToken cancellationToken)
        {
            Hearts--;
            hud.SetHearts(Hearts);
            NinjagoAnalytics.SlipFail(PlayerIndex, sweep, safeSide, reason, Hearts);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.NinjaHit);
            PlayTinted(VfxManager.VisualEffect.NinjaBrickPop, ninja.FeetPosition + Vector3.up, body.Color);
            ninja.PlayHit(config.HitRecoverSeconds);
            if (Hearts <= 0)
            {
                IsOut = true;
                ninja.SetOut();
                hud.ShowOut();
            }

            await WaitLocal(config.HitRecoverSeconds, cancellationToken);
            ninja.ReturnToCenter();
            brute.SetStaff(0f, restLiftDegrees);
        }

        async UniTask<bool> CounterAsync(int sweep, bool firstCounter, CancellationToken cancellationToken)
        {
            NinjagoAnalytics.CounterStart(PlayerIndex, sweep);
            clock.EaseTo(config.SlowMotionScale, config.SlowMotionEaseSeconds);
            var meter = storms.Begin(body);
            hud.ShowSpinHint(firstCounter);
            ninja.ShowSwirl(true);
            var swirl = PlayTinted(VfxManager.VisualEffect.SpinjitzuSwirl, ninja.FeetPosition, body.Color);
            var elapsed = 0f;
            while (elapsed < config.CounterWindowSeconds && !meter.IsFilled)
            {
                elapsed += Time.unscaledDeltaTime;
                ninja.SetSwirlFill(meter.Fill01);
                if (swirl != null) DriveSwirl(swirl, meter.Fill01);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            storms.End(PlayerIndex);
            hud.ShowSpinHint(false);
            clock.EaseTo(1f, config.SlowMotionEaseSeconds);
            if (swirl != null)
            {
                // Pooled: the next player to get this instance must not inherit the slow motion.
                var swirlMain = swirl.main;
                swirlMain.simulationSpeed = 1f;
                swirl.Stop();
            }

            ninja.ShowSwirl(false);
            if (meter.IsFilled)
            {
                NinjagoAnalytics.CounterSuccess(PlayerIndex, sweep, elapsed, meter.TravelInches, meter.PeakSpeed);
                SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.NinjaSpin);
                PlayTinted(VfxManager.VisualEffect.SpinjitzuBurst, ninja.FeetPosition, body.Color);
                VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BruteBrickSpray, bruteChest.position);
                ninja.PlaySpin(config.SpinSeconds);
                await WaitLocal(config.SpinSeconds, cancellationToken);
            }
            else
            {
                NinjagoAnalytics.CounterWhiff(PlayerIndex, sweep, meter.TravelInches, meter.PeakSpeed, meter.Fill01);
                await WaitLocal(config.WhiffRecoverSeconds, cancellationToken);
            }

            brute.SetStance(0f);
            brute.SetStaff(0f, restLiftDegrees);
            return meter.IsFilled;
        }

        #endregion

        #region Helpers

        float CockYaw(SweepSide safeSide) => -safeSide.Opposite().Sign() * cockYawDegrees;

        void FollowLean()
        {
            if (body.TryGetLeanInches(out var lean)) ninja.SetLeanHint(lean.x / config.LeanThresholdInches);
        }

        void DriveSwirl(ParticleSystem swirl, float fill01)
        {
            var main = swirl.main;
            main.simulationSpeed = clock.Scale;
            var emission = swirl.emission;
            emission.rateOverTimeMultiplier = Mathf.Lerp(6f, 90f, fill01);
        }

        static ParticleSystem? PlayTinted(VfxManager.VisualEffect effect, Vector3 position, Color color)
        {
            var system = VfxManager.Instance.PlayVisualEffect(effect, position, Quaternion.identity);
            if (system == null) return null;
            var main = system.main;
            main.startColor = color;
            return system;
        }

        async UniTask WaitLocal(float seconds, CancellationToken cancellationToken)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += clock.DeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        async UniTask AnimateLocal(float seconds, Action<float> step, CancellationToken cancellationToken)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += clock.DeltaTime;
                step(Mathf.Clamp01(elapsed / seconds));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        #endregion
    }
}
