using System;
using NaughtyAttributes;
using Nex.Util;
using UnityEngine;

namespace Nex
{
    [CreateAssetMenu(fileName = "SetupDetectorWarningConfig", menuName = "Nex/Setup/SetupDetectorWarningConfig", order = 0)]
    public class SetupDetectorWarningConfig : ScriptableObject
    {
        [SerializeField] EnumDictionary<SetupDetectorMode, WarningConfig> warningConfigs;

        public WarningConfig GetWarningConfig(SetupDetectorMode mode)
        {
            var baseConfig = warningConfigs[SetupDetectorMode.Base];
            var targetConfig = warningConfigs[mode];
            var result = new WarningConfig
            {
                overrideWarningConfig = targetConfig.overrideWarningConfig,
                chestStrictLooseHalfMarginInches =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig
                        .OverrideChestStrictLooseHalfMarginInches)
                        ? targetConfig.chestStrictLooseHalfMarginInches
                        : baseConfig.chestStrictLooseHalfMarginInches,
                chestToTopMinInches =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverrideChestToTopMinInches)
                        ? targetConfig.chestToTopMinInches
                        : baseConfig.chestToTopMinInches,
                chestToBottomMinInches =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverrideChestToBottomMinInches)
                        ? targetConfig.chestToBottomMinInches
                        : baseConfig.chestToBottomMinInches,
                chestToLeftMinInches =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverrideChestToLeftMinInches)
                        ? targetConfig.chestToLeftMinInches
                        : baseConfig.chestToLeftMinInches,
                chestToRightMinInches =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverrideChestToRightMinInches)
                        ? targetConfig.chestToRightMinInches
                        : baseConfig.chestToRightMinInches,
                chestXToCenterMaxInches =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverrideChestXToCenterMaxInches)
                        ? targetConfig.chestXToCenterMaxInches
                        : baseConfig.chestXToCenterMaxInches,
                issueEvaluationTimeWindow =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverrideIssueEvaluationTimeWindow)
                        ? targetConfig.issueEvaluationTimeWindow
                        : baseConfig.issueEvaluationTimeWindow,
                issueMinRequiredDataTime =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverrideIssueMinRequiredDataTime)
                        ? targetConfig.issueMinRequiredDataTime
                        : baseConfig.issueMinRequiredDataTime,
                issueEntryStateRatio =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverrideIssueEntryStateRatio)
                        ? targetConfig.issueEntryStateRatio
                        : baseConfig.issueEntryStateRatio,
                issueCancelStateRatio =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverrideIssueCancelStateRatio)
                        ? targetConfig.issueCancelStateRatio
                        : baseConfig.issueCancelStateRatio,
                distanceRatioStrictLooseHalfMarginForDistanceIssue =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig
                        .OverrideDistanceRatioStrictLooseHalfMarginForDistanceIssue)
                        ? targetConfig.distanceRatioStrictLooseHalfMarginForDistanceIssue
                        : baseConfig.distanceRatioStrictLooseHalfMarginForDistanceIssue,
                frameHeightMinInches =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverrideFrameHeightMinInches)
                        ? targetConfig.frameHeightMinInches
                        : baseConfig.frameHeightMinInches,
                frameHeightMaxInches =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverrideFrameHeightMaxInches)
                        ? targetConfig.frameHeightMaxInches
                        : baseConfig.frameHeightMaxInches,
                playAreaHeightMinInches =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverridePlayAreaHeightMinInches)
                        ? targetConfig.playAreaHeightMinInches
                        : baseConfig.playAreaHeightMinInches,
                playAreaHeightMaxInches =
                    targetConfig.overrideWarningConfig.HasFlag(OverrideWarningConfig.OverridePlayAreaHeightMaxInches)
                        ? targetConfig.playAreaHeightMaxInches
                        : baseConfig.playAreaHeightMaxInches,
            };
            return result;
        }
    }

    [Serializable]
    public class WarningConfig
    {
        [EnumFlags, AllowNesting] public OverrideWarningConfig overrideWarningConfig = OverrideWarningConfig.Everything;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideChestStrictLooseHalfMarginInches)]
        public float chestStrictLooseHalfMarginInches = 1f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideChestToTopMinInches)]
        public float chestToTopMinInches = 12f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideChestToBottomMinInches)]
        public float chestToBottomMinInches = 18f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideChestToLeftMinInches)]
        public float chestToLeftMinInches = 12f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideChestToRightMinInches)]
        public float chestToRightMinInches = 12f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideChestXToCenterMaxInches)]
        public float chestXToCenterMaxInches = 10f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideIssueEvaluationTimeWindow)]
        public float issueEvaluationTimeWindow = 0.5f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideIssueMinRequiredDataTime)]
        public float issueMinRequiredDataTime = 0.2f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideIssueEntryStateRatio)]
        public float issueEntryStateRatio = 0.8f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideIssueCancelStateRatio)]
        public float issueCancelStateRatio = 0.4f;

        [AllowNesting,
         ShowIf("overrideWarningConfig",
             OverrideWarningConfig.OverrideDistanceRatioStrictLooseHalfMarginForDistanceIssue)]
        public float distanceRatioStrictLooseHalfMarginForDistanceIssue = 0.03f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideFrameHeightMinInches)]
        public float frameHeightMinInches = 40f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverrideFrameHeightMaxInches)]
        public float frameHeightMaxInches = 280f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverridePlayAreaHeightMinInches)]
        public float playAreaHeightMinInches = 40f;

        [AllowNesting, ShowIf("overrideWarningConfig", OverrideWarningConfig.OverridePlayAreaHeightMaxInches)]
        public float playAreaHeightMaxInches = 150f;
    }

    public enum SetupDetectorMode
    {
        Base = 0,
        Gameplay = 1,
    }

    [Flags]
    public enum OverrideWarningConfig
    {
        None = 0,
        OverrideChestStrictLooseHalfMarginInches = 1 << 0,
        OverrideChestToTopMinInches = 1 << 1,
        OverrideChestToBottomMinInches = 1 << 2,
        OverrideChestToLeftMinInches = 1 << 3,
        OverrideChestToRightMinInches = 1 << 4,
        OverrideChestXToCenterMaxInches = 1 << 5,
        OverrideIssueEvaluationTimeWindow = 1 << 6,
        OverrideIssueMinRequiredDataTime = 1 << 7,
        OverrideIssueEntryStateRatio = 1 << 8,
        OverrideIssueCancelStateRatio = 1 << 9,
        OverrideDistanceRatioStrictLooseHalfMarginForDistanceIssue = 1 << 10,
        OverrideFrameHeightMinInches = 1 << 11,
        OverrideFrameHeightMaxInches = 1 << 12,
        OverridePlayAreaHeightMinInches = 1 << 13,
        OverridePlayAreaHeightMaxInches = 1 << 14,
        Everything = ~0,
    }
}
