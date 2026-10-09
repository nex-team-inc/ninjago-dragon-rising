#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Nex.BilliardRogue.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// "Act 1 · Stage 2 — Mossy Ruins" band (boss variant: red band, boss portrait and name). Pops itself
    /// PacingConfig.StageIntroDuration after it becomes the top view; await WaitClosedAsync for the end.
    /// Usage: Instantiate → Initialize → Show → PushView → WaitClosedAsync.
    /// </summary>
    public sealed class StageIntroView : RogueView
    {
        [Header("Band")]
        [SerializeField] RectTransform band = null!;
        [SerializeField] Image bandImage = null!;
        [SerializeField] RectTransform textGroup = null!;
        [SerializeField] TextLabel headerLabel = null!;
        [SerializeField] TextLabel subtitleLabel = null!;

        [Header("Boss variant")]
        [SerializeField] GameObject bossDecor = null!;
        [SerializeField] Image bossIcon = null!;
        [SerializeField] GameObject normalDecor = null!;

        PacingConfig pacing = null!;
        EnemyCatalog enemies = null!;
        IReadOnlyList<ActDefinition> acts = Array.Empty<ActDefinition>();
        bool isBoss;
        bool started;

        public override ViewIdentifier Identifier => ViewIdentifier.StageIntro;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.None;
        public override string AnalyticsScreenName => "stage_intro";

        #region Public Methods

        public void Initialize(PacingConfig pacing, EnemyCatalog enemies, IReadOnlyList<ActDefinition> acts)
        {
            this.pacing = pacing;
            this.enemies = enemies;
            this.acts = acts;
        }

        /// <summary>actIndex / stageInAct are 0-based (as in RunState).</summary>
        public void Show(int actIndex, int stageInAct, bool isBoss)
        {
            this.isBoss = isBoss;
            bossDecor.SetActive(isBoss);
            normalDecor.SetActive(!isBoss);
            bandImage.color = isBoss ? theme.BossTint : theme.StageBandColor;
            if (isBoss)
            {
                var boss = acts[actIndex].Rules.bossType;
                headerLabel.SetKey(LocKeys.StageIntro.BossStage);
                headerLabel.Color = theme.Accent;
                subtitleLabel.SetKey(LocKeys.Enemy.Name(boss));
                bossIcon.sprite = enemies.Get(boss).Icon;
            }
            else
            {
                headerLabel.SetKey(LocKeys.StageIntro.Header, actIndex + 1, stageInAct + 1);
                headerLabel.Color = theme.TextPrimary;
                subtitleLabel.SetKey(LocKeys.Act.Name(actIndex));
            }
        }

        public override void ViewDidBecomeTopView(bool afterPush)
        {
            base.ViewDidBecomeTopView(afterPush);
            if (started) return;
            started = true;
            RunAsync().Forget();
        }

        #endregion

        #region Helpers

        async UniTaskVoid RunAsync()
        {
            var ct = destroyCancellationToken;
            UiTheme.PlaySfx(isBoss ? theme.BossIntroSfx : theme.StageIntroSfx);
            PlayIntro();
            await UniTask.Delay(TimeSpan.FromSeconds(pacing.StageIntroDuration), DelayType.UnscaledDeltaTime, cancellationToken: ct);
            // A pause pushed on top meanwhile: close once we are the top view again.
            await UniTask.WaitUntil(() => IsActive, cancellationToken: ct);
            await PopSelf();
        }

        void PlayIntro()
        {
            band.localScale = new Vector3(1f, 0f, 1f);
            var slide = theme.BannerSlideDistance;
            var basePosition = textGroup.anchoredPosition;
            textGroup.anchoredPosition = basePosition + new Vector2(slide, 0f);
            DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(band.DOScaleY(1f, theme.BannerSlideDuration).SetEase(Ease.OutBack))
                .Join(textGroup.DOAnchorPos(basePosition, theme.BannerSlideDuration * 1.5f).SetEase(Ease.OutCubic));
        }

        #endregion
    }
}
