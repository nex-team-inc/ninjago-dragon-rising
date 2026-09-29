#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// One player's control readout (control lab, debug builds): active source and tracking, launch X, aim angle,
    /// the right paw's closing speed against the strike threshold, the paw distance against the contact distance,
    /// the detector state, strike count, last strike and a 4-line event log (strikes, misses and why, dropped
    /// strikes). Values come from the ShotInputRouter (what the game uses) and its PawShotInput (the body path, also
    /// while a debug source is active). Texts are rewritten only when their shown value changes (TMP SetText with
    /// float arguments: no allocation); bars are anchored rects.
    /// </summary>
    public sealed class ControlReadoutPanel : MonoBehaviour
    {
        const float PeakHoldSeconds = 1f;
        const float BarEpsilon = 0.002f;

        enum LogKind { Strike, StrikePower, TooSlow, TooFar, ShortThrust, Cooldown, NotArmed, Untracked, Expired }

        struct LogEntry
        {
            public LogKind kind;
            public float value;
        }

        // Index = LogKind; TMP SetText format ({0:0} = integer, {0:0.0} = one decimal).
        static readonly string[] logFormats =
        {
            "STRIKE {0:0} in/s", "STRIKE {0:0} in/s POWER", "missed: too slow {0:0} in/s", "missed: paws too far {0:0.0} in",
            "missed: short thrust", "cooldown", "not armed: open paws", "dropped: no tracking", "expired: not fired",
        };
        static readonly string[] headerFormats = { "P{0:0} PAW", "P{0:0} DEBUG", "P{0:0} BOT" };

        [Header("Rows")]
        [SerializeField] TextMeshProUGUI headerLabel = null!;
        [SerializeField] TextMeshProUGUI trackingLabel = null!;
        [SerializeField] TextMeshProUGUI launchLabel = null!;
        [SerializeField] TextMeshProUGUI aimLabel = null!;
        [SerializeField] TextMeshProUGUI handLabel = null!;
        [SerializeField] TextMeshProUGUI speedLabel = null!;
        [SerializeField] TextMeshProUGUI speedNeedLabel = null!;
        [SerializeField] TextMeshProUGUI distanceLabel = null!;
        [SerializeField] TextMeshProUGUI distanceNeedLabel = null!;
        [SerializeField] TextMeshProUGUI stateLabel = null!;
        [SerializeField] TextMeshProUGUI strikesLabel = null!;
        [SerializeField] TextMeshProUGUI lastLabel = null!;
        [Tooltip("Newest first.")]
        [SerializeField] TextMeshProUGUI[] logLabels = null!;

        [Header("Bars (fills and markers are anchored by x inside their bar)")]
        [SerializeField] Image speedFill = null!;
        [SerializeField] RectTransform speedPeak = null!;
        [SerializeField] RectTransform speedThresholdTick = null!;
        [SerializeField] RectTransform speedPowerTick = null!;
        [SerializeField] Image distanceFill = null!;
        [SerializeField] RectTransform distanceContactTick = null!;
        [SerializeField] RectTransform distanceArmTick = null!;

        [Header("Scales")]
        [Tooltip("The speed bar spans 0 .. this x the power-shot speed.")]
        [SerializeField, Range(1f, 2f)] float speedBarHeadroom = 1.25f;
        [Tooltip("The distance bar spans 0 .. this x the arm distance.")]
        [SerializeField, Range(1f, 4f)] float distanceBarSpan = 2f;

        [Header("Colors")]
        [SerializeField] Color goodColor = new(0.49f, 0.894f, 0.42f, 1f);
        [SerializeField] Color warnColor = new(1f, 0.784f, 0.267f, 1f);
        [SerializeField] Color badColor = new(1f, 0.353f, 0.29f, 1f);
        [SerializeField] Color idleColor = new(0.72f, 0.77f, 0.9f, 1f);
        [SerializeField] Color powerColor = new(1f, 0.45f, 0.95f, 1f);
        [Tooltip("Alpha of the log lines, newest first.")]
        [SerializeField] float[] logAlpha = { 1f, 0.8f, 0.6f, 0.45f };

        ShotInputRouter router = null!;
        readonly LogEntry[] log = new LogEntry[4];
        int logCount;
        int seenStrikes;
        int seenMisses;
        int seenUntracked;
        int seenExpired;
        float peakSpeed;
        float peakTime;
        // Last shown (quantized) values; int.MinValue forces a rewrite.
        int shownHeader;
        int shownTracking;
        int shownLaunch;
        int shownAim;
        int shownHand;
        int shownSpeed;
        int shownNeed;
        int shownDistance;
        int shownContact;
        int shownState;
        int shownStrikes;
        int shownLast;

        #region Public Methods

        /// <summary>Follows a new router (calibration and gameplay each create their own); the log carries over.</summary>
        public void Bind(ShotInputRouter aRouter)
        {
            router = aRouter;
            var readout = router.Paw.StrikeReadout;
            seenStrikes = readout.strikeCount;
            seenMisses = readout.missCount;
            seenUntracked = router.Paw.UntrackedStrikeCount;
            seenExpired = router.Paw.ExpiredStrikeCount;
            peakSpeed = 0f;
            shownHeader = shownTracking = shownLaunch = shownAim = shownHand = int.MinValue;
            shownSpeed = shownNeed = shownDistance = shownContact = shownState = shownStrikes = shownLast = int.MinValue;
            RenderLog();
        }

        public void Refresh(float now)
        {
            var paw = router.Paw;
            var readout = paw.StrikeReadout;
            var settings = readout.settings;
            RefreshHeader(paw);
            RefreshAim(paw.LeftHanded);
            RefreshSpeed(readout, settings, now);
            RefreshDistance(readout, settings);
            RefreshState(readout);
            CollectEvents(paw, readout);
        }

        #endregion

        #region Rows

        void RefreshHeader(PawShotInput paw)
        {
            var source = (int)router.ActiveSource;
            var header = source * 8 + router.PlayerIndex;
            if (header != shownHeader)
            {
                shownHeader = header;
                headerLabel.SetText(headerFormats[source], router.PlayerIndex + 1);
            }

            // 0 tracked, 1 acquiring, 2 no paws in the frame, 3 no camera frames.
            var tracking = paw.IsTracking ? 0 : !paw.HasCameraFrames ? 3 : !paw.PawsDetected ? 2 : 1;
            if (tracking == shownTracking) return;
            shownTracking = tracking;
            trackingLabel.SetText(tracking switch
            {
                0 => "TRACKED",
                1 => "ACQUIRING",
                2 => "NO TRACKING: PAWS",
                _ => "NO TRACKING: CAMERA",
            });
            trackingLabel.color = tracking == 0 ? goodColor : tracking == 1 ? warnColor : badColor;
        }

        void RefreshAim(bool leftHanded)
        {
            var launch = Mathf.RoundToInt(router.LaunchX01 * 100f);
            if (launch != shownLaunch)
            {
                shownLaunch = launch;
                launchLabel.SetText("X {0:0.00}", launch / 100f);
            }

            var aim = router.AimDirection;
            var degrees = Mathf.RoundToInt(Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            if (degrees != shownAim)
            {
                shownAim = degrees;
                aimLabel.SetText("AIM {0:0}°", degrees);
            }

            var hand = leftHanded ? 1 : 0;
            if (hand == shownHand) return;
            shownHand = hand;
            handLabel.SetText(leftHanded ? "L-HAND" : "R-HAND");
        }

        void RefreshSpeed(in StrikeReadout readout, in StrikeSettings settings, float now)
        {
            var speed = Mathf.Max(0f, readout.closingSpeed);
            if (speed >= peakSpeed || now - peakTime > PeakHoldSeconds)
            {
                peakSpeed = speed;
                peakTime = now;
            }

            var shown = Mathf.RoundToInt(speed);
            if (shown != shownSpeed)
            {
                shownSpeed = shown;
                speedLabel.SetText("SPEED {0:0} in/s", shown);
            }

            var need = Mathf.RoundToInt(settings.strikeSpeed);
            if (need != shownNeed)
            {
                shownNeed = need;
                speedNeedLabel.SetText("NEED {0:0}", need);
            }

            var powerSpeed = settings.strikeSpeed * settings.powerMultiplier;
            var span = Mathf.Max(1f, powerSpeed * speedBarHeadroom);
            SetFill(speedFill, speed / span);
            SetMarker(speedPeak, peakSpeed / span);
            SetMarker(speedThresholdTick, settings.strikeSpeed / span);
            SetMarker(speedPowerTick, powerSpeed / span);
            speedFill.color = speed >= powerSpeed ? powerColor : speed >= settings.strikeSpeed ? goodColor : warnColor;
        }

        void RefreshDistance(in StrikeReadout readout, in StrikeSettings settings)
        {
            var distance = Mathf.RoundToInt(readout.pawDistance * 10f);
            if (distance != shownDistance)
            {
                shownDistance = distance;
                distanceLabel.SetText("DIST {0:0.0} in", distance / 10f);
            }

            var contact = Mathf.RoundToInt(settings.contactDistance * 10f);
            if (contact != shownContact)
            {
                shownContact = contact;
                distanceNeedLabel.SetText("CONTACT {0:0.0}", contact / 10f);
            }

            var span = Mathf.Max(1f, settings.armDistance * distanceBarSpan);
            SetFill(distanceFill, readout.pawDistance / span);
            SetMarker(distanceContactTick, settings.contactDistance / span);
            SetMarker(distanceArmTick, settings.armDistance / span);
            distanceFill.color = readout.pawDistance <= settings.contactDistance ? goodColor
                : readout.pawDistance >= settings.armDistance ? idleColor : warnColor;
        }

        void RefreshState(in StrikeReadout readout)
        {
            // Cooldown past rearmSeconds only waits for the paws to open: shown as OPEN PAWS.
            var cooling = readout.state == StrikeState.Cooldown && readout.cooldownRemaining > 0f;
            var open = readout.state == StrikeState.Disarmed || (readout.state == StrikeState.Cooldown && !cooling);
            // Codes: 0..3 StrikeState, 10 open paws, 1000+ cooldown hundredths.
            var state = cooling ? 1000 + Mathf.RoundToInt(readout.cooldownRemaining * 100f) : open ? 10 : (int)readout.state;
            if (state != shownState)
            {
                shownState = state;
                if (cooling)
                {
                    stateLabel.SetText("COOLDOWN {0:0.00}s", readout.cooldownRemaining);
                    stateLabel.color = warnColor;
                }
                else if (open)
                {
                    stateLabel.SetText("OPEN PAWS");
                    stateLabel.color = warnColor;
                }
                else
                {
                    var approaching = readout.state == StrikeState.Approaching;
                    stateLabel.SetText(approaching ? "THRUST" : "ARMED");
                    stateLabel.color = approaching ? powerColor : goodColor;
                }
            }

            if (readout.strikeCount != shownStrikes)
            {
                shownStrikes = readout.strikeCount;
                strikesLabel.SetText("STRIKES {0:0}", readout.strikeCount);
            }

            var last = readout.strikeCount == 0 ? -1 : Mathf.RoundToInt(readout.lastStrikeSpeed) * 2 + (readout.lastStrikeWasPower ? 1 : 0);
            if (last == shownLast) return;
            shownLast = last;
            if (last < 0)
            {
                lastLabel.SetText("LAST -");
                lastLabel.color = idleColor;
                return;
            }

            lastLabel.SetText(readout.lastStrikeWasPower ? "LAST {0:0} in/s POWER" : "LAST {0:0} in/s", Mathf.Round(readout.lastStrikeSpeed));
            lastLabel.color = readout.lastStrikeWasPower ? powerColor : goodColor;
        }

        #endregion

        #region Event Log

        void CollectEvents(PawShotInput paw, in StrikeReadout readout)
        {
            if (readout.strikeCount != seenStrikes)
            {
                seenStrikes = readout.strikeCount;
                Push(readout.lastStrikeWasPower ? LogKind.StrikePower : LogKind.Strike, readout.lastStrikeSpeed);
            }

            if (readout.missCount != seenMisses)
            {
                seenMisses = readout.missCount;
                switch (readout.lastMiss)
                {
                    case StrikeMiss.TooSlow:
                        Push(LogKind.TooSlow, readout.lastMissSpeed);
                        break;
                    case StrikeMiss.TooFar:
                        Push(LogKind.TooFar, readout.lastMissDistance);
                        break;
                    case StrikeMiss.ShortThrust:
                        Push(LogKind.ShortThrust, 0f);
                        break;
                    case StrikeMiss.Cooldown:
                        Push(LogKind.Cooldown, 0f);
                        break;
                    case StrikeMiss.NotArmed:
                        Push(LogKind.NotArmed, 0f);
                        break;
                }
            }

            if (paw.UntrackedStrikeCount != seenUntracked)
            {
                seenUntracked = paw.UntrackedStrikeCount;
                Push(LogKind.Untracked, 0f);
            }

            if (paw.ExpiredStrikeCount == seenExpired) return;
            seenExpired = paw.ExpiredStrikeCount;
            Push(LogKind.Expired, 0f);
        }

        void Push(LogKind kind, float value)
        {
            for (var i = log.Length - 1; i > 0; i--)
            {
                log[i] = log[i - 1];
            }

            log[0] = new LogEntry { kind = kind, value = value };
            logCount = Mathf.Min(logCount + 1, log.Length);
            RenderLog();
        }

        void RenderLog()
        {
            for (var i = 0; i < logLabels.Length; i++)
            {
                var label = logLabels[i];
                if (i >= logCount)
                {
                    label.SetText(i == 0 ? "thrust right paw into left" : "");
                    label.color = idleColor;
                    continue;
                }

                var entry = log[i];
                label.SetText(logFormats[(int)entry.kind], entry.value);
                var color = entry.kind switch
                {
                    LogKind.Strike => goodColor,
                    LogKind.StrikePower => powerColor,
                    LogKind.Untracked or LogKind.Expired => badColor,
                    _ => warnColor,
                };
                color.a = i < logAlpha.Length ? logAlpha[i] : 0.4f;
                label.color = color;
            }
        }

        #endregion

        #region Helpers

        static void SetFill(Image fill, float value01)
        {
            var rt = fill.rectTransform;
            var x = Mathf.Clamp01(value01);
            if (Mathf.Abs(rt.anchorMax.x - x) < BarEpsilon) return;
            rt.anchorMax = new Vector2(x, rt.anchorMax.y);
        }

        static void SetMarker(RectTransform marker, float value01)
        {
            var x = Mathf.Clamp01(value01);
            if (Mathf.Abs(marker.anchorMin.x - x) < BarEpsilon) return;
            marker.anchorMin = new Vector2(x, marker.anchorMin.y);
            marker.anchorMax = new Vector2(x, marker.anchorMax.y);
        }

        #endregion
    }
}
