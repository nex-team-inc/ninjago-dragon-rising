#nullable enable

using System;
using DG.Tweening;
using Nex.BilliardRogue.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// End of run: Victory/Defeat, stage reached, new-record flag, run stats (count up), balls unlocked by this run,
    /// Play Again / Title. Usage: Instantiate → Initialize → Show → ReplaceView(gameplay).
    /// </summary>
    public sealed class SummaryView : RogueView
    {
        // Order of statValues: turns, shots, hits, kills, damage dealt, damage taken, best combo, bosses, time.
        const int StatCount = 9;
        const int TimeStat = 8;

        [Header("Header")]
        [SerializeField] TextLabel titleLabel = null!;
        [SerializeField] TextLabel stageLabel = null!;
        [SerializeField] RectTransform newRecordChip = null!;

        [Header("Stats")]
        [Tooltip("Values in order: turns, shots, hits, kills, damage dealt, damage taken, best combo, bosses, time.")]
        [SerializeField] TextLabel[] statValues = null!;

        [Header("Unlocks")]
        [SerializeField] GameObject unlockGroup = null!;
        [Tooltip("Unlock slots (icon + name), hidden when unused.")]
        [SerializeField] GameObject[] unlockSlots = null!;
        [SerializeField] Image[] unlockIcons = null!;
        [SerializeField] TextLabel[] unlockNames = null!;

        [Header("Buttons")]
        [SerializeField] Button playAgainButton = null!;
        [SerializeField] Button titleButton = null!;

        readonly int[] targets = new int[StatCount];
        BallCatalog balls = null!;
        bool newRecord;
        bool started;

        public event Action? PlayAgainRequested;
        public event Action? TitleRequested;

        public override ViewIdentifier Identifier => ViewIdentifier.Summary;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.None;
        public override string AnalyticsScreenName => "summary";

        #region Life Cycle

        protected override void Awake()
        {
            base.Awake();
            playAgainButton.onClick.AddListener(HandlePlayAgain);
            titleButton.onClick.AddListener(HandleTitle);
        }

        #endregion

        #region Public Methods

        public void Initialize(BallCatalog balls)
        {
            this.balls = balls;
        }

        /// <summary>
        /// unlockTierBefore = MetaProgressData.highestUnlockTier before RunPersistence.CompleteRun; balls whose unlock tier
        /// lies in (before, meta.highestUnlockTier] are shown as unlocked. -1 hides the unlock row.
        /// </summary>
        public void Show(RunState run, MetaProgressData meta, bool newRecord, int unlockTierBefore = -1)
        {
            this.newRecord = newRecord;
            var victory = run.outcome == RunOutcome.Victory;
            titleLabel.SetKey(victory ? LocKeys.Summary.Victory : LocKeys.Summary.Defeat);
            titleLabel.Color = victory ? theme.Accent : theme.Danger;
            stageLabel.SetKey(LocKeys.Summary.StageReached, run.actIndex + 1, run.stageInAct + 1);
            newRecordChip.gameObject.SetActive(newRecord);

            var stats = run.stats;
            targets[0] = stats.turns;
            targets[1] = stats.shots;
            targets[2] = stats.hits;
            targets[3] = stats.kills;
            targets[4] = stats.damageDealt;
            targets[5] = stats.damageTaken;
            targets[6] = stats.bestCombo;
            targets[7] = stats.bossesDefeated;
            targets[TimeStat] = Mathf.RoundToInt(stats.playSeconds);
            for (var i = 0; i < StatCount; i++)
            {
                SetStat(i, 0);
            }

            ShowUnlocks(unlockTierBefore, meta.highestUnlockTier);
        }

        public override void ViewDidBecomeTopView(bool afterPush)
        {
            base.ViewDidBecomeTopView(afterPush);
            if (started) return;
            started = true;
            CountUp();
        }

        #endregion

        #region Helpers

        void ShowUnlocks(int tierBefore, int tierAfter)
        {
            var shown = 0;
            if (tierBefore >= 0 && tierAfter > tierBefore)
            {
                for (var type = 0; type < SimConstants.BallTypeCount && shown < unlockSlots.Length; type++)
                {
                    var definition = balls.Get((BallType)type);
                    var tier = definition.Rules.unlockTier;
                    if (tier <= tierBefore) continue;
                    if (tier > tierAfter) continue;
                    unlockIcons[shown].sprite = definition.Icon;
                    unlockNames[shown].SetKey(LocKeys.Ball.Name((BallType)type));
                    shown++;
                }
            }

            unlockGroup.SetActive(shown > 0);
            for (var i = 0; i < unlockSlots.Length; i++)
            {
                unlockSlots[i].SetActive(i < shown);
            }
        }

        void CountUp()
        {
            var duration = theme.SummaryCountUpDuration;
            var progress = 0f;
            DOTween.To(() => progress, value =>
                {
                    progress = value;
                    for (var i = 0; i < StatCount; i++)
                    {
                        SetStat(i, Mathf.RoundToInt(targets[i] * value));
                    }
                }, 1f, duration).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);

            if (!newRecord) return;
            UiTheme.PlaySfx(theme.NewRecordSfx);
            newRecordChip.DOScale(theme.ChipPulseScale, theme.ChipPulseDuration).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }

        void SetStat(int index, int value)
        {
            if (index == TimeStat)
            {
                statValues[index].SetNumbers("{0}:{1:00}", value / 60, value % 60);
                return;
            }

            statValues[index].SetNumber(value);
        }

        void HandlePlayAgain()
        {
            if (!IsActive) return;
            TrackButton("play_again");
            PlayAgainRequested?.Invoke();
        }

        void HandleTitle()
        {
            if (!IsActive) return;
            TrackButton("title");
            TitleRequested?.Invoke();
        }

        #endregion
    }
}
