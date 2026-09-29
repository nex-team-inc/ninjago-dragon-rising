#nullable enable

using DG.Tweening;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// One player's test-strike status on the calibration screen: "Waiting for player N…" → "Player N ready" with a
    /// glow and a hop; while waiting, "Show both paws" when the camera does not see the player's arms.
    /// </summary>
    public sealed class CalibrationPlayerCard : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [SerializeField] TextLabel statusLabel = null!;
        [SerializeField] GameObject readyGlow = null!;
        [SerializeField] RectTransform portrait = null!;
        [Tooltip("'P1' / 'P2' chip in the player colour.")]
        [SerializeField] TextLabel tagLabel = null!;

        bool? shownReady;
        bool shownTracked = true;

        public void Set(int playerIndex, bool ready)
        {
            if (shownReady == ready) return;
            if (shownReady == null)
            {
                tagLabel.SetKey(LocKeys.Hud.PlayerTag, playerIndex + 1);
            }

            var wasWaiting = shownReady == false;
            shownReady = ready;
            shownTracked = true;
            statusLabel.SetKey(ready ? LocKeys.Calibration.PlayerReady : LocKeys.Calibration.Waiting, playerIndex + 1);
            statusLabel.Color = ready ? theme.Positive : theme.TextMuted;
            readyGlow.SetActive(ready);
            if (!ready) return;
            if (!wasWaiting) return;
            portrait.DOKill(true);
            portrait.DOPunchAnchorPos(new Vector2(0f, 24f), 0.4f, 4, 0.5f).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>
        /// While waiting for the strike: a strike only registers when chest, elbows and wrists are all in frame
        /// (IShotInput.IsTracking), which the setup steps do not check, so say why nothing happens.
        /// </summary>
        public void SetTracked(int playerIndex, bool tracked)
        {
            if (shownReady != false) return;
            if (shownTracked == tracked) return;
            shownTracked = tracked;
            if (tracked)
            {
                statusLabel.SetKey(LocKeys.Calibration.Waiting, playerIndex + 1);
            }
            else
            {
                statusLabel.SetKey(LocKeys.Setup.ShowBothPaws);
            }

            statusLabel.Color = tracked ? theme.TextMuted : theme.Danger;
        }
    }
}
