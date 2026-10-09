#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's Stone Kick world: a block boss on the far side and a camera rendering into that player's screen
    /// region. Runs that player's throws only: lob → hang → the first slash with that player's cursors opens a slashing
    /// frenzy where every slash cuts one more stone → KICK prompt where one knee pulse of that player launches every
    /// stone back into the boss. A rock nobody slashed reaches the player.
    /// </summary>
    public class StoneKickLane : MonoBehaviour
    {
        [Header("Boss")]
        [SerializeField] BossRig boss = null!;
        [Header("Rock")]
        [SerializeField] Transform rock = null!;
        [Header("Rock Hit Radius")]
        [Tooltip("World radius of the whole rock for the cursor hit test.")]
        [SerializeField] float rockRadius = 0.75f;
        [Header("Stones")]
        [SerializeField] RockPieces pieces = null!;
        [Header("Hang Point")]
        [SerializeField] Transform hangPoint = null!;
        [Header("Impact Point")]
        [Tooltip("Just in front of the camera: where a rock nobody slashed hits the player.")]
        [SerializeField] Transform impactPoint = null!;
        [Header("Lane Camera")]
        [SerializeField] Camera laneCamera = null!;
        [Header("Solo Field Of View")]
        [SerializeField] float soloFieldOfView = 50f;
        [Header("Split Field Of View")]
        [SerializeField] float splitFieldOfView = 62f;
        [Header("Wind-up Seconds")]
        [Tooltip("Boss arm swing before the rock leaves its hand.")]
        [SerializeField] float windupSeconds = 0.6f;
        [Header("Throw Arc Height")]
        [SerializeField] float arcHeight = 1.4f;
        [Header("Slash Pop Seconds")]
        [Tooltip("How long the rock pops and shakes after each slash.")]
        [SerializeField] float slashPopSeconds = 0.15f;
        [Header("Smallest Rock Scale")]
        [Tooltip("Scale of the rock once it has been cut into the most stones.")]
        [SerializeField, Range(0.2f, 1f)] float smallestRockScale = 0.55f;
        [Header("Slash Pitch Step")]
        [Tooltip("Each slash in a frenzy plays the slash sound this much higher.")]
        [SerializeField] float slashPitchStep = 0.07f;

        readonly SlashDetector[] slashDetectors = { new(), new() };
        PlayerBody body = null!;
        StoneKickConfig config = null!;
        HandCursorTracker cursors = null!;
        StoneKickHud hud = null!;
        PlayerKicks kicks = null!;
        Rect region;
        Vector3 rockBaseScale;
        int currentThrow;
        int throwSlashes;
        float slashPopStart = float.NegativeInfinity;
        bool promptOpen;
        int queuedKicks;
        float kickTimeSum;

        public int PlayerIndex => body.PlayerIndex;
        public Color PlayerColor => body.Color;
        public int Hearts { get; private set; }
        /// <summary>Every slash that landed, all frenzies together.</summary>
        public int Slashes { get; private set; }
        /// <summary>Throws whose stones went back to the boss on a kick in time.</summary>
        public int Returns { get; private set; }
        public int StonesCut { get; private set; }
        public int StonesReturned { get; private set; }
        public bool IsOut { get; private set; }
        /// <summary>Mean seconds from the KICK prompt to the kick; -1 before the first kick.</summary>
        public float AverageKickTime => Returns > 0 ? kickTimeSum / Returns : -1f;

        #region Initialization

        public void Initialize(PlayerBody aBody, StoneKickConfig aConfig, HandCursorTracker aCursors, StoneKickHud aHud, RenderTexture target, Rect aRegion)
        {
            body = aBody;
            config = aConfig;
            cursors = aCursors;
            hud = aHud;
            region = aRegion;
            kicks = new PlayerKicks(body);
            laneCamera.targetTexture = target;
            laneCamera.fieldOfView = region.width < 1f ? splitFieldOfView : soloFieldOfView;
            boss.Initialize(body.PlayerIndex, body.Color);
            pieces.Initialize(hangPoint, laneCamera, boss, config);
            Hearts = config.Hearts;
            hud.Initialize(body.PlayerIndex, body.Color, Hearts, config.ThrowsPerPlayer);
            rockBaseScale = rock.localScale;
            rock.gameObject.SetActive(false);
        }

        #endregion

        #region Life Cycle

        // Knees are read every frame so a knee already up when the prompt opens does not count until it is released.
        void Update()
        {
            if (IsOut) return;
            var pulse = kicks.Poll(Time.unscaledTime, config.KickSettings);
            if (pulse == KickDetector.Pulse.Blocked)
            {
                StoneKickAnalytics.KickRejected(PlayerIndex, currentThrow, "cooldown");
            }
            else if (pulse == KickDetector.Pulse.Kick)
            {
                if (promptOpen) queuedKicks++;
                else StoneKickAnalytics.KickRejected(PlayerIndex, currentThrow, "no_prompt");
            }
        }

        #endregion

        #region Throw Loop

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            var throws = config.ThrowsPerPlayer;
            await WaitAsync(config.IntroSeconds, cancellationToken);
            for (currentThrow = 0; currentThrow < throws; currentThrow++)
            {
                hud.SetThrow(currentThrow + 1, throws);
                if (await ThrowAndSlashAsync(cancellationToken))
                {
                    await KickAsync(cancellationToken);
                }
                else
                {
                    await MissAsync(cancellationToken);
                    if (IsOut) return;
                }

                if (currentThrow < throws - 1) await WaitAsync(config.RestBetweenThrowsSeconds, cancellationToken);
            }

            hud.ShowFinished();
        }

        // True when this player's cursors slashed the rock; the slashing frenzy keeps the rock up until it ends.
        async UniTask<bool> ThrowAndSlashAsync(CancellationToken cancellationToken)
        {
            boss.PlayThrow(windupSeconds);
            await WaitAsync(windupSeconds, cancellationToken);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.StaffWhoosh);
            foreach (var detector in slashDetectors) detector.Reset();
            var start = boss.HandPosition;
            var hang = hangPoint.position;
            var flySeconds = config.ThrowSeconds;
            var airborneSeconds = flySeconds + config.HangSecondsFor(currentThrow);
            var frenzyEnd = float.PositiveInfinity;
            throwSlashes = 0;
            hud.ShowSlashHint(currentThrow < config.LongHangCount);
            rock.localScale = rockBaseScale;
            rock.gameObject.SetActive(true);
            var elapsed = 0f;
            while (throwSlashes > 0 ? Time.unscaledTime < frenzyEnd : elapsed < airborneSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var pop = 1f - Mathf.Clamp01((Time.unscaledTime - slashPopStart) / slashPopSeconds);
                var shake = Vector3.right * (Mathf.Sin(Time.unscaledTime * 90f) * 0.08f * pop);
                rock.position = RockPosition(start, hang, elapsed, flySeconds) + shake;
                rock.Rotate(new Vector3(70f, 35f, 0f) * Time.unscaledDeltaTime, Space.Self);
                var slashesBefore = throwSlashes;
                CollectSlashes(elapsed);
                if (slashesBefore == 0 && throwSlashes > 0) frenzyEnd = Time.unscaledTime + config.SlashFrenzySeconds;
                var carved = config.StonesFor(throwSlashes) / (float)config.MaxStones;
                rock.localScale = rockBaseScale * (Mathf.Lerp(1f, smallestRockScale, carved) * (1f + 0.2f * pop));
                if (config.StonesFor(throwSlashes) >= config.MaxStones) break;
                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, cancellationToken);
            }

            hud.ShowSlashHint(false);
            hud.HideSlashCount();
            return throwSlashes > 0;
        }

        // Every slash: a rising slash sound, a streak along the stroke, stone chips, a rock pop and the counter.
        void OnSlash(Vector2 stroke, float speed, float airborneSeconds)
        {
            throwSlashes++;
            Slashes++;
            slashPopStart = Time.unscaledTime;
            StoneKickAnalytics.SlashHit(PlayerIndex, currentThrow, throwSlashes, speed, airborneSeconds);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.RockSlash, 1f + slashPitchStep * Mathf.Min(throwSlashes - 1, 8));
            var angle = Mathf.Atan2(stroke.y, stroke.x) * Mathf.Rad2Deg;
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RockSlash, rock.position,
                laneCamera.transform.rotation * Quaternion.Euler(0f, 0f, angle));
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RockImpact, rock.position);
            hud.ShowSlashCount(throwSlashes);
        }

        async UniTask KickAsync(CancellationToken cancellationToken)
        {
            var stones = config.StonesFor(throwSlashes);
            StonesCut += stones;
            var burstPoint = rock.position;
            rock.gameObject.SetActive(false);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RockImpact, burstPoint);
            await pieces.SplitAsync(burstPoint, stones, cancellationToken);

            queuedKicks = 0;
            promptOpen = true;
            hud.ShowKick(stones);
            var promptStart = Time.unscaledTime;
            while (queuedKicks == 0 && Time.unscaledTime - promptStart < config.KickWindowSeconds)
            {
                pieces.Bob();
                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, cancellationToken);
            }

            var kicked = queuedKicks > 0;
            promptOpen = false;
            queuedKicks = 0;
            hud.HideKick();
            var settleSeconds = config.PieceDropSeconds;
            if (kicked)
            {
                var kickTime = Time.unscaledTime - promptStart;
                Returns++;
                StonesReturned += stones;
                kickTimeSum += kickTime;
                SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.KickThud);
                StoneKickAnalytics.KickAccepted(PlayerIndex, currentThrow, stones, kickTime);
                StoneKickAnalytics.FullReturn(PlayerIndex, currentThrow, stones, kickTime);
                settleSeconds = pieces.LaunchAll(destroyCancellationToken);
            }
            else
            {
                StoneKickAnalytics.IncompleteReturn(PlayerIndex, currentThrow, stones);
                pieces.DropAll(destroyCancellationToken);
            }

            await WaitAsync(settleSeconds, cancellationToken);
        }

        async UniTask MissAsync(CancellationToken cancellationToken)
        {
            var from = rock.position;
            var to = impactPoint.position;
            var elapsed = 0f;
            while (elapsed < config.MissFlySeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / config.MissFlySeconds);
                rock.position = Vector3.Lerp(from, to, t * t);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            rock.gameObject.SetActive(false);
            Hearts--;
            hud.SetHearts(Hearts);
            hud.FlashHit();
            StoneKickAnalytics.SlashMiss(PlayerIndex, currentThrow, Hearts);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.NinjaHit);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RockImpact, to);
            if (Hearts <= 0)
            {
                IsOut = true;
                hud.ShowOut();
            }

            await WaitAsync(config.HitRecoverSeconds, cancellationToken);
        }

        #endregion

        #region Helpers

        Vector3 RockPosition(Vector3 start, Vector3 hang, float elapsed, float flySeconds)
        {
            if (elapsed >= flySeconds) return hang + Vector3.up * (Mathf.Sin((elapsed - flySeconds) * 2.4f) * 0.06f);
            var eased = 1f - (1f - elapsed / flySeconds) * (1f - elapsed / flySeconds);
            return Vector3.Lerp(start, hang, eased) + Vector3.up * (arcHeight * 4f * eased * (1f - eased));
        }

        // Both of this player's hands can slash in the same frame; each crossing counts.
        void CollectSlashes(float airborneSeconds)
        {
            if (!TryProject(rock.position, out var center) || !TryProject(rock.position + laneCamera.transform.right * rockRadius, out var edge)) return;
            var radius = (edge - center).magnitude;
            var pixels = new Vector2(Screen.width, Screen.height);
            for (var hand = 0; hand < 2; hand++)
            {
                var cursor = cursors.GetCursor(PlayerIndex, hand);
                var detector = slashDetectors[hand];
                if (!cursor.IsVisible)
                {
                    detector.Reset();
                    continue;
                }

                if (detector.Update(Vector2.Scale(cursor.ScreenPosition, pixels), cursor.SpeedInchesPerSecond, cursor.Continuity, center, radius,
                        Time.unscaledTime, config.SlashSpeedInchesPerSecond, config.SlashMaxCrossSeconds))
                {
                    OnSlash(detector.LastStep, cursor.SpeedInchesPerSecond, airborneSeconds);
                }
            }
        }

        // World point → screen pixels, through this lane's camera and the screen region its feed fills.
        bool TryProject(Vector3 world, out Vector2 pixels)
        {
            var viewport = laneCamera.WorldToViewportPoint(world);
            pixels = Vector2.Scale(region.min + Vector2.Scale(viewport, region.size), new Vector2(Screen.width, Screen.height));
            return viewport.z > 0f;
        }

        static UniTask WaitAsync(float seconds, CancellationToken cancellationToken)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, cancellationToken);
        }

        #endregion
    }
}
