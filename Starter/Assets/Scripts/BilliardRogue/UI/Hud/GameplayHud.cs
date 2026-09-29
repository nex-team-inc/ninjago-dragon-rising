#nullable enable

using System.Collections.Generic;
using DG.Tweening;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Gameplay HUD (IGameplayHud), laid out around a vertical centre arena: left column = camera panel (the PiP feed
    /// overlay sits on its screen, P1/P2 shooter chips in its header), stage/turn, HP and tracking warnings; right
    /// column = boss bar, ball queue and chips; turn/shooter ribbons slide in at the top centre. Both columns hide
    /// while the stage intro band is up and slide in after it (SetRevealed). Every setter ignores unchanged values,
    /// so GameSession may push every frame without cost. Must sit under a canvas (GameplayView); the root carries a
    /// nested Canvas so HUD rebuilds never dirty the view canvas.
    /// </summary>
    public sealed class GameplayHud : MonoBehaviour, IGameplayHud
    {
        [SerializeField] UiTheme theme = null!;

        [Header("Left column")]
        [SerializeField] TextLabel stageLabel = null!;
        [SerializeField] HudChip bossStageChip = null!;
        [SerializeField] TextLabel turnLabel = null!;
        [SerializeField] HpBarWidget hpBar = null!;
        [SerializeField] PlayerTagsWidget playerTags = null!;
        [Tooltip("One per player (index = player).")]
        [SerializeField] HudChip[] trackingWarnings = null!;

        [Header("Right column")]
        [SerializeField] BossBarWidget bossBar = null!;
        [SerializeField] TextLabel ballsCounter = null!;
        [SerializeField] BallQueueWidget ballQueue = null!;
        [SerializeField] HudChip bonusBallsChip = null!;
        [SerializeField] HudChip powerChip = null!;
        [SerializeField] HudChip fastForwardChip = null!;

        [Header("Ribbons")]
        [SerializeField] HudBanner turnBanner = null!;
        [SerializeField] HudBanner shooterBanner = null!;

        [Header("Reveal (slide in from the screen edges)")]
        [SerializeField] RectTransform leftColumn = null!;
        [SerializeField] CanvasGroup leftGroup = null!;
        [SerializeField] RectTransform rightColumn = null!;
        [SerializeField] CanvasGroup rightGroup = null!;

        PacingConfig pacing = null!;
        int stageAct = -1;
        int stageIndex = -1;
        int shownTurn = -1;
        int ballsRemaining = -1;
        int ballsTotal = -1;
        int bonusBalls = -1;
        bool revealed = true;
        Vector2 leftHome;
        Vector2 rightHome;
        Sequence? revealTween;

        #region Public Methods

        public void Initialize(BallCatalog balls, PacingConfig pacing)
        {
            this.pacing = pacing;
            leftHome = leftColumn.anchoredPosition;
            rightHome = rightColumn.anchoredPosition;
            ballQueue.Initialize(balls);
            turnLabel.gameObject.SetActive(false);
            for (var i = 0; i < trackingWarnings.Length; i++)
            {
                trackingWarnings[i].Label.SetKey(LocKeys.Hud.TrackingWarningPlayer, i + 1);
            }
        }

        public void SetHp(int cur, int max)
        {
            hpBar.Set(cur, max);
        }

        public void SetBallQueue(IReadOnlyList<BallInstance> bag, int nextIndex, int extraBalls)
        {
            ballQueue.Set(bag, nextIndex, extraBalls);
            if (extraBalls == bonusBalls) return;
            bonusBalls = extraBalls;
            if (extraBalls > 0)
            {
                bonusBallsChip.Label.SetKey(LocKeys.Hud.BonusBalls, extraBalls);
            }

            bonusBallsChip.SetVisible(extraBalls > 0);
        }

        public void SetBallsRemaining(int remaining, int total)
        {
            if (remaining == ballsRemaining && total == ballsTotal)
            {
                return;
            }

            ballsRemaining = remaining;
            ballsTotal = total;
            ballsCounter.SetNumbers("{0}/{1}", remaining, total);
            ballsCounter.Color = remaining > 0 ? theme.TextPrimary : theme.TextMuted;
        }

        /// <summary>actIndex / stageInAct are 0-based (as in RunState).</summary>
        public void SetStage(int actIndex, int stageInAct, bool isBoss)
        {
            bossStageChip.SetVisible(isBoss);
            if (actIndex == stageAct && stageInAct == stageIndex)
            {
                return;
            }

            stageAct = actIndex;
            stageIndex = stageInAct;
            stageLabel.SetKey(LocKeys.Hud.Stage, actIndex + 1, stageInAct + 1);
        }

        public void SetActivePlayer(int playerIndex, int numPlayers)
        {
            playerTags.Set(playerIndex, numPlayers);
        }

        public void SetBossHp(bool visible, int hp, int maxHp, EnemyType type)
        {
            bossBar.Set(visible, hp, maxHp, type);
        }

        public void SetFastForward(bool on)
        {
            fastForwardChip.SetVisible(on);
        }

        public void SetPowerArmed(bool armed)
        {
            powerChip.SetVisible(armed);
            ballQueue.SetPowerArmed(armed);
        }

        /// <summary>turn as shown to the player (1-based).</summary>
        public void ShowTurnBanner(int turn)
        {
            // A turn never starts under the intro band; this also covers a Continue that resumes into the reward.
            SetRevealed(true, true);
            if (turn != shownTurn)
            {
                shownTurn = turn;
                turnLabel.gameObject.SetActive(true);
                turnLabel.SetKey(LocKeys.Hud.Turn, turn);
            }

            turnBanner.Label.SetKey(LocKeys.Hud.TurnBanner, turn);
            turnBanner.Show(pacing.TurnBannerDuration);
        }

        public void ShowShooterBanner(int playerIndex)
        {
            var label = shooterBanner.Label;
            label.SetKey(LocKeys.Hud.ShooterBanner, playerIndex + 1);
            label.Color = theme.PlayerColor(playerIndex);
            shooterBanner.Show(pacing.ShooterBannerDuration);
        }

        public void SetTrackingWarning(int playerIndex, bool lost)
        {
            if (playerIndex < 0) return;
            if (playerIndex >= trackingWarnings.Length) return;
            trackingWarnings[playerIndex].SetVisible(lost);
        }

        /// <summary>Hides both columns (stage intro) or slides them back in from the screen edges.</summary>
        public void SetRevealed(bool show, bool animate)
        {
            if (show == revealed) return;
            revealed = show;
            revealTween?.Kill();
            var slide = theme.HudRevealSlide;
            var leftAway = leftHome - new Vector2(slide, 0f);
            var rightAway = rightHome + new Vector2(slide, 0f);
            var alpha = show ? 1f : 0f;
            if (!animate)
            {
                leftColumn.anchoredPosition = show ? leftHome : leftAway;
                rightColumn.anchoredPosition = show ? rightHome : rightAway;
                leftGroup.alpha = alpha;
                rightGroup.alpha = alpha;
                return;
            }

            var duration = show ? theme.HudRevealDuration : theme.DismissDuration;
            var ease = show ? Ease.OutCubic : Ease.InQuad;
            revealTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Join(leftColumn.DOAnchorPos(show ? leftHome : leftAway, duration).SetEase(ease))
                .Join(rightColumn.DOAnchorPos(show ? rightHome : rightAway, duration).SetEase(ease))
                .Join(leftGroup.DOFade(alpha, duration))
                .Join(rightGroup.DOFade(alpha, duration));
        }

        #endregion
    }
}
