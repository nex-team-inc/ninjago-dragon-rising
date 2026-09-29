#nullable enable

using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// "Step back into view" overlay for one player. Closes itself once isTracked() holds (after a short
    /// "Found you! Resuming…"), or on RequestClose(). Back (remote/Escape, through the back-proxy key responder)
    /// raises requestPause, so the pause menu (Resume / Save &amp; Quit) opens over it even when a player never
    /// returns. Usage: Instantiate → Initialize → PushView → WaitClosedAsync.
    /// </summary>
    public sealed class TrackingLostView : RogueView
    {
        [Header("Content")]
        [SerializeField] TextLabel bodyLabel = null!;
        [SerializeField] GameObject playerChip = null!;
        [SerializeField] TextLabel playerChipLabel = null!;
        [Tooltip("Portrait of the missing player's cat; pulses while waiting.")]
        [SerializeField] Image portrait = null!;
        [SerializeField] GameObject waitingGroup = null!;
        [SerializeField] GameObject resumingGroup = null!;

        Func<bool> isTracked = () => false;
        Action? requestPause;
        bool started;
        bool closeRequested;

        public override ViewIdentifier Identifier => ViewIdentifier.TrackingLost;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "tracking_lost";

        /// <summary>
        /// playerIndex is 0-based; isTracked is polled every frame while the view is on top; requestPause runs on Back
        /// (the opener pushes the pause view over this one).
        /// </summary>
        public void Initialize(int playerIndex, int numPlayers, Func<bool> isTracked, Action? requestPause = null)
        {
            this.isTracked = isTracked;
            this.requestPause = requestPause;
            bodyLabel.SetKey(LocKeys.TrackingLost.Body, playerIndex + 1);
            playerChip.SetActive(numPlayers > 1);
            playerChipLabel.SetKey(LocKeys.Hud.PlayerTag, playerIndex + 1);
            playerChipLabel.Color = theme.PlayerColor(playerIndex);
            portrait.sprite = theme.Portrait(playerIndex);
            waitingGroup.SetActive(true);
            resumingGroup.SetActive(false);
        }

        /// <summary>Closes the overlay without waiting for tracking (e.g. the run ended meanwhile).</summary>
        public void RequestClose()
        {
            closeRequested = true;
        }

        public override void OnBackButton()
        {
            if (!IsActive || closeRequested || requestPause == null) return;
            TrackBack();
            requestPause();
        }

        public override void ViewDidBecomeTopView(bool afterPush)
        {
            base.ViewDidBecomeTopView(afterPush);
            if (started) return;
            started = true;
            RunAsync().Forget();
        }

        async UniTaskVoid RunAsync()
        {
            var ct = destroyCancellationToken;
            UiTheme.PlaySfx(theme.TrackingLostSfx);
            StartPulse();
            while (true)
            {
                await UniTask.WaitUntil(() => closeRequested || (IsActive && isTracked()), cancellationToken: ct);
                if (closeRequested) break;
                waitingGroup.SetActive(false);
                resumingGroup.SetActive(true);
                await UniTask.Delay(TimeSpan.FromSeconds(theme.TrackingResumeHold), DelayType.UnscaledDeltaTime, cancellationToken: ct);
                if (closeRequested || isTracked()) break;
                waitingGroup.SetActive(true);
                resumingGroup.SetActive(false);
            }

            await UniTask.WaitUntil(() => IsActive, cancellationToken: ct);
            await PopSelf();
        }

        void StartPulse()
        {
            portrait.rectTransform.DOScale(theme.ChipPulseScale, theme.ChipPulseDuration).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }
    }
}
