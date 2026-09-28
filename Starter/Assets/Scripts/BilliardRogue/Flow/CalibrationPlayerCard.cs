#nullable enable

using DG.Tweening;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>One player's test-strike status on the calibration screen: "Waiting for player N…" → "Player N ready" with a glow and a hop.</summary>
    public sealed class CalibrationPlayerCard : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [SerializeField] TextLabel statusLabel = null!;
        [SerializeField] GameObject readyGlow = null!;
        [SerializeField] RectTransform portrait = null!;
        [Tooltip("'P1' / 'P2' chip in the player colour.")]
        [SerializeField] TextLabel tagLabel = null!;

        bool? shownReady;

        public void Set(int playerIndex, bool ready)
        {
            if (shownReady == ready) return;
            if (shownReady == null) tagLabel.SetKey(LocKeys.Hud.PlayerTag, playerIndex + 1);
            var wasWaiting = shownReady == false;
            shownReady = ready;
            statusLabel.SetKey(ready ? LocKeys.Calibration.PlayerReady : LocKeys.Calibration.Waiting, playerIndex + 1);
            statusLabel.Color = ready ? theme.Positive : theme.TextMuted;
            readyGlow.SetActive(ready);
            if (!ready || !wasWaiting) return;
            portrait.DOKill(true);
            portrait.DOPunchAnchorPos(new Vector2(0f, 24f), 0.4f, 4, 0.5f).SetUpdate(true).SetLink(gameObject);
        }
    }
}
