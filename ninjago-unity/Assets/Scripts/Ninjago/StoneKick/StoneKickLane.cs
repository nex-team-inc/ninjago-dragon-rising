#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's Stone Kick world: a block boss on the far side and a camera rendering into that player's screen
    /// region. Runs that player's throws only: lob → hang (that player's cursors may slash) → KICK prompt (that
    /// player's knee pulses launch the pieces back into the boss), or the whole rock reaches the player.
    /// </summary>
    public class StoneKickLane : MonoBehaviour
    {
        [Header("Boss")]
        [SerializeField] BossRig boss = null!;
        [Header("Rock")]
        [SerializeField] Transform rock = null!;
        [Header("Rock Hit Radius")]
        [Tooltip("World radius of the rock for the cursor hit test.")]
        [SerializeField] float rockRadius = 0.75f;
        [Header("Piece Prefab")]
        [SerializeField] Transform piecePrefab = null!;
        [Header("Pieces Root")]
        [SerializeField] Transform piecesRoot = null!;
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
        [Header("Piece Spacing")]
        [SerializeField] float pieceSpacing = 0.95f;
        [Header("Row Max Width")]
        [Tooltip("The piece row never gets wider than this, so every piece stays on screen.")]
        [SerializeField] float rowMaxWidth = 2.6f;
        [Header("Ground Height")]
        [SerializeField] float groundHeight;

        readonly SlashDetector[] slashDetectors = { new(), new() };
        readonly List<Transform> pieces = new();
        PlayerBody body = null!;
        StoneKickConfig config = null!;
        HandCursorTracker cursors = null!;
        StoneKickHud hud = null!;
        PlayerKicks kicks = null!;
        Rect region;
        int currentThrow;
        bool promptOpen;
        int queuedKicks;
        float kickGapSum;
        int kickGapCount;

        public int PlayerIndex => body.PlayerIndex;
        public Color PlayerColor => body.Color;
        public int Hearts { get; private set; }
        public int Slashes { get; private set; }
        public int FullReturns { get; private set; }
        public int KicksFired { get; private set; }
        /// <summary>Kicks there was a piece for: the pieces of every landed slash.</summary>
        public int KicksPossible { get; private set; }
        public bool IsOut { get; private set; }
        /// <summary>Mean seconds between consecutive accepted kicks inside one prompt; -1 before any such pair.</summary>
        public float AverageKickGap => kickGapCount > 0 ? kickGapSum / kickGapCount : -1f;

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
            Hearts = config.Hearts;
            hud.Initialize(body.PlayerIndex, body.Color, Hearts, config.ThrowsPerPlayer);
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
                if (await ThrowAndHangAsync(cancellationToken))
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

        // True when one of this player's cursors slashed the rock before it left the hang point.
        async UniTask<bool> ThrowAndHangAsync(CancellationToken cancellationToken)
        {
            boss.PlayThrow(windupSeconds);
            await WaitAsync(windupSeconds, cancellationToken);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.StaffWhoosh);
            foreach (var detector in slashDetectors) detector.Reset();
            var start = boss.HandPosition;
            var hang = hangPoint.position;
            var flySeconds = config.ThrowSeconds;
            var airborneSeconds = flySeconds + config.HangSecondsFor(currentThrow);
            hud.ShowSlashHint(currentThrow < config.LongHangCount);
            rock.gameObject.SetActive(true);
            var elapsed = 0f;
            while (elapsed < airborneSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                rock.position = RockPosition(start, hang, elapsed, flySeconds);
                rock.Rotate(new Vector3(70f, 35f, 0f) * Time.unscaledDeltaTime, Space.Self);
                if (TrySlash(out var stroke, out var speed))
                {
                    hud.ShowSlashHint(false);
                    OnSlash(stroke, speed, elapsed);
                    return true;
                }

                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, cancellationToken);
            }

            hud.ShowSlashHint(false);
            return false;
        }

        void OnSlash(Vector2 stroke, float speed, float airborneSeconds)
        {
            Slashes++;
            StoneKickAnalytics.SlashHit(PlayerIndex, currentThrow, speed, airborneSeconds);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.RockSlash);
            var angle = Mathf.Atan2(stroke.y, stroke.x) * Mathf.Rad2Deg;
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.RockSlash, rock.position,
                laneCamera.transform.rotation * Quaternion.Euler(0f, 0f, angle));
            rock.gameObject.SetActive(false);
        }

        async UniTask KickAsync(CancellationToken cancellationToken)
        {
            var required = config.KicksRequired;
            KicksPossible += required;
            await SplitAsync(required, cancellationToken);

            queuedKicks = 0;
            promptOpen = true;
            hud.ShowKick(0, required);
            var launched = 0;
            var promptStart = Time.unscaledTime;
            var lastKickTime = 0f;
            var promptGapSum = 0f;
            while (launched < required && Time.unscaledTime - promptStart < config.KickWindowSeconds)
            {
                for (; queuedKicks > 0 && launched < required; queuedKicks--)
                {
                    var now = Time.unscaledTime;
                    if (launched > 0)
                    {
                        promptGapSum += now - lastKickTime;
                        kickGapSum += now - lastKickTime;
                        kickGapCount++;
                    }

                    lastKickTime = now;
                    LaunchPieceAsync(pieces[launched], destroyCancellationToken).Forget();
                    launched++;
                    KicksFired++;
                    hud.ShowKick(launched, required);
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.KickThud);
                    StoneKickAnalytics.KickAccepted(PlayerIndex, currentThrow, launched, now - promptStart);
                }

                BobRow(launched, required);
                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, cancellationToken);
            }

            promptOpen = false;
            queuedKicks = 0;
            hud.HideKick();
            if (launched == required)
            {
                FullReturns++;
                StoneKickAnalytics.FullReturn(PlayerIndex, currentThrow, launched, Time.unscaledTime - promptStart,
                    launched > 1 ? promptGapSum / (launched - 1) : 0f);
            }
            else
            {
                StoneKickAnalytics.IncompleteReturn(PlayerIndex, currentThrow, launched, required);
            }

            for (var i = launched; i < required; i++) DropPieceAsync(pieces[i], destroyCancellationToken).Forget();
            await WaitAsync(Mathf.Max(config.PieceFlySeconds, config.PieceDropSeconds), cancellationToken);
        }

        async UniTask MissAsync(CancellationToken cancellationToken)
        {
            var from = rock.position;
            var to = impactPoint.position;
            await AnimateAsync(config.MissFlySeconds, t => rock.position = Vector3.Lerp(from, to, t * t), cancellationToken);
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

        #region Pieces

        async UniTask SplitAsync(int count, CancellationToken cancellationToken)
        {
            while (pieces.Count < count) pieces.Add(Instantiate(piecePrefab, piecesRoot));
            var center = rock.position;
            for (var i = 0; i < pieces.Count; i++)
            {
                pieces[i].gameObject.SetActive(i < count);
                pieces[i].position = center;
            }

            await AnimateAsync(config.SplitSeconds, t =>
            {
                var eased = 1f - (1f - t) * (1f - t);
                for (var i = 0; i < count; i++) pieces[i].position = Vector3.Lerp(center, RowSlot(i, count), eased);
            }, cancellationToken);
        }

        Vector3 RowSlot(int index, int count)
        {
            var spacing = count > 1 ? Mathf.Min(pieceSpacing, rowMaxWidth / (count - 1)) : 0f;
            return hangPoint.position + laneCamera.transform.right * ((index - (count - 1) * 0.5f) * spacing);
        }

        void BobRow(int launched, int count)
        {
            var time = Time.unscaledTime;
            for (var i = launched; i < count; i++)
            {
                pieces[i].position = RowSlot(i, count) + Vector3.up * (Mathf.Sin(time * 3f + i * 1.7f) * 0.05f);
            }
        }

        async UniTaskVoid LaunchPieceAsync(Transform piece, CancellationToken cancellationToken)
        {
            var from = piece.position;
            var to = boss.ChestPosition;
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.PieceLaunch, from);
            await AnimateAsync(config.PieceFlySeconds, t =>
            {
                piece.position = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.5f);
                piece.Rotate(new Vector3(600f, 0f, 200f) * Time.unscaledDeltaTime, Space.Self);
            }, cancellationToken);
            piece.gameObject.SetActive(false);
            boss.PlayFlinch();
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BossHit, to);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.PieceHit);
        }

        async UniTaskVoid DropPieceAsync(Transform piece, CancellationToken cancellationToken)
        {
            var from = piece.position;
            var to = new Vector3(from.x, groundHeight, from.z);
            await AnimateAsync(config.PieceDropSeconds, t => piece.position = Vector3.Lerp(from, to, t * t), cancellationToken);
            piece.gameObject.SetActive(false);
        }

        #endregion

        #region Helpers

        Vector3 RockPosition(Vector3 start, Vector3 hang, float elapsed, float flySeconds)
        {
            if (elapsed >= flySeconds) return hang + Vector3.up * (Mathf.Sin((elapsed - flySeconds) * 2.4f) * 0.06f);
            var eased = 1f - (1f - elapsed / flySeconds) * (1f - elapsed / flySeconds);
            return Vector3.Lerp(start, hang, eased) + Vector3.up * (arcHeight * 4f * eased * (1f - eased));
        }

        bool TrySlash(out Vector2 stroke, out float speed)
        {
            stroke = default;
            speed = 0f;
            if (!TryProject(rock.position, out var center) || !TryProject(rock.position + laneCamera.transform.right * rockRadius, out var edge)) return false;
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

                if (!detector.Update(Vector2.Scale(cursor.ScreenPosition, pixels), cursor.SpeedInchesPerSecond, cursor.Continuity, center, radius,
                        Time.unscaledTime, config.SlashSpeedInchesPerSecond, config.SlashMaxCrossSeconds)) continue;
                stroke = detector.LastStep;
                speed = cursor.SpeedInchesPerSecond;
                return true;
            }

            return false;
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

        static async UniTask AnimateAsync(float seconds, Action<float> step, CancellationToken cancellationToken)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                step(Mathf.Clamp01(elapsed / seconds));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            step(1f);
        }

        #endregion
    }
}
