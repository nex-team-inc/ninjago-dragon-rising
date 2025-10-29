using System;
using System.Collections.Generic;
using Jazz;
using UnityEngine;
using UnityEngine.Events;

#pragma warning disable CS0618

namespace Nex
{
    public enum SetupIssueType
    {
        None,
        NoPose,
        ChestTooHigh,
        ChestTooLow,
        ChestTooLeft,
        ChestTooRight,
        TooFar,
        TooClose,
        TooFarInPlayArea,
        TooCloseInPlayArea,
        NotAtCenter,
    }

    public struct SetupIssueInfo
    {
        // Using strict/loose threshold to avoid jumping warnings.
        public bool hasIssueUnderStrictCondition;
        public bool hasIssueUnderLooseCondition;
        public bool hasData;
    }

    public struct SetupDetection
    {
        public bool hasEnoughData;
        public SetupIssueType currentIssue;
    }

    public class SetupDetector : MonoBehaviour
    {
        BodyPoseDetectionManager bodyDetector;
        int playerIndex;

        float ppi;
        float distanceRatio;
        float distanceRatioInPlayArea;
        SetupIssueType currentIssue;

        bool hasEnoughData;
        float startDetectionTime;

        #region SetupDetectorWarningConfig

        SetupDetectorMode setupDetectorMode;
        SetupDetectorWarningConfig setupDetectorWarningConfig;

        readonly Dictionary<SetupDetectorMode, WarningConfig> cachedWarningConfig = new();
        WarningConfig CurrentConfig
        {
            get
            {
                if (!cachedWarningConfig.ContainsKey(setupDetectorMode))
                {
                    cachedWarningConfig[setupDetectorMode] = setupDetectorWarningConfig.GetWarningConfig(setupDetectorMode);
                }
                return cachedWarningConfig[setupDetectorMode];
            }
        }

        float ChestStrictLooseHalfMarginInches => CurrentConfig.chestStrictLooseHalfMarginInches;
        float ChestToTopMinInches => CurrentConfig.chestToTopMinInches;
        float ChestToBottomMinInches => CurrentConfig.chestToBottomMinInches;
        float ChestToLeftMinInches => CurrentConfig.chestToLeftMinInches;
        float ChestToRightMinInches => CurrentConfig.chestToRightMinInches;
        float ChestXToCenterMaxInches => CurrentConfig.chestXToCenterMaxInches;
        float IssueEvaluationTimeWindow => CurrentConfig.issueEvaluationTimeWindow;
        float IssueMinRequiredDataTime => CurrentConfig.issueMinRequiredDataTime;
        float IssueEntryStateRatio => CurrentConfig.issueEntryStateRatio;
        float IssueCancelStateRatio => CurrentConfig.issueCancelStateRatio;

        float DistanceRatioStrictLooseHalfMarginForDistanceIssue =>
            CurrentConfig.distanceRatioStrictLooseHalfMarginForDistanceIssue;

        float FrameHeightMinInches => CurrentConfig.frameHeightMinInches;
        float FrameHeightMaxInches => CurrentConfig.frameHeightMaxInches;
        float PlayAreaHeightMinInches => CurrentConfig.playAreaHeightMinInches;
        float PlayAreaHeightMaxInches => CurrentConfig.playAreaHeightMaxInches;

        #endregion

        readonly Dictionary<SetupIssueType, bool> isIssueActivatedByType = new Dictionary<SetupIssueType, bool>();
        readonly Dictionary<SetupIssueType, SetupIssueInfo> issueInfoByType = new Dictionary<SetupIssueType, SetupIssueInfo>();
        readonly Dictionary<SetupIssueType, History<SetupIssueInfo>> issueInfoHistoryByType = new Dictionary<SetupIssueType, History<SetupIssueInfo>>();

        List<SetupIssueType> issueTypesSortedByDisplayPriority;

        BasePlayAreaController playAreaController = null!;

        public event UnityAction<SetupDetection> captureDetection;

        // MARK - Public

        // MARK - Life Cycle

        public void Initialize(
            int aPlayerIndex,
            BodyPoseDetectionManager aBodyDetector,
            BasePlayAreaController aPlayAreaController,
            SetupDetectorWarningConfig aSetupDetectorWarningConfig,
            List<SetupIssueType> aIssueTypesSortedByDisplayPriority = null)
        {
            playerIndex = aPlayerIndex;
            bodyDetector = aBodyDetector;

            playAreaController = aPlayAreaController;
            setupDetectorWarningConfig = aSetupDetectorWarningConfig;

            issueTypesSortedByDisplayPriority = aIssueTypesSortedByDisplayPriority ?? new List<SetupIssueType>
            {
                SetupIssueType.TooClose,
                SetupIssueType.TooFar,
                SetupIssueType.TooCloseInPlayArea,
                SetupIssueType.TooFarInPlayArea,
                SetupIssueType.ChestTooHigh,
                SetupIssueType.ChestTooLow,
                SetupIssueType.ChestTooLeft,
                SetupIssueType.ChestTooRight,
                SetupIssueType.NoPose,
                SetupIssueType.NotAtCenter,
            };

            ResetAllStates();

            bodyDetector.captureBodyPoseDetection += BodyDetectorOnCapturePoseDetectionWithoutPostProcess;
        }

        void OnDestroy()
        {
            bodyDetector.captureBodyPoseDetection -= BodyDetectorOnCapturePoseDetectionWithoutPostProcess;
        }

        // MARK - Events

        void BodyDetectorOnCapturePoseDetectionWithoutPostProcess(BodyPoseDetection poseDetection)
        {
            ProcessPoseDetection(poseDetection);
            AnalyzeDetectionHistory();
            AnnounceDetection();
        }

        // MARK - Helper

        public void SetDetectorMode(SetupDetectorMode mode)
        {
            setupDetectorMode = mode;
        }

        void AnnounceDetection()
        {
            var detection = new SetupDetection
            {
                hasEnoughData = hasEnoughData,
                currentIssue = currentIssue
            };

            // DebugPrinter.Instance.Print($"Setup[{playerIndex}] issue", currentIssue.ToString());

            captureDetection?.Invoke(detection);
        }

        void ResetAllStates()
        {
            // Initialise the values of hasIssueByType
            foreach (SetupIssueType type in issueTypesSortedByDisplayPriority)
            {
                isIssueActivatedByType[type] = false;
                issueInfoByType[type] = new SetupIssueInfo();
                issueInfoHistoryByType[type] = new History<SetupIssueInfo>(IssueEvaluationTimeWindow);
            }

            startDetectionTime = -1;
            hasEnoughData = false;
        }

        void ProcessPoseDetection(BodyPoseDetection poseDetection)
        {
            var playerPose = poseDetection.GetPlayerPose(playerIndex);

            var time = Time.fixedTime;
            startDetectionTime = startDetectionTime < 0 ? time : startDetectionTime; // Only set it once when it's negative.
            hasEnoughData = time - startDetectionTime > IssueEvaluationTimeWindow;

            var pose = playerPose?.bodyPose;

            var tooCloseInfo = new SetupIssueInfo();
            var tooFarInfo = new SetupIssueInfo();
            var chestTooHighInfo = new SetupIssueInfo();
            var chestTooLowInfo = new SetupIssueInfo();
            var chestTooLeftInfo = new SetupIssueInfo();
            var chestTooRightInfo = new SetupIssueInfo();
            var noPoseInfo = new SetupIssueInfo();
            var notAtCenterInfo = new SetupIssueInfo();
            var tooCloseInPlayAreaInfo = new SetupIssueInfo();
            var tooFarInPlayAreaInfo = new SetupIssueInfo();

            noPoseInfo.hasData = true;

            if (pose == null)
            {
                noPoseInfo.hasIssueUnderLooseCondition = true;
                noPoseInfo.hasIssueUnderStrictCondition = true;
            }
            else
            {
                ppi = pose.pixelsPerInch;

                var chestPt = pose.Chest().ToVector2();
                var frameSize = poseDetection.frameSize;

                // Distance in Raw Frame
                var frameHeightInInches = frameSize.y / ppi;

                distanceRatio = (frameHeightInInches - FrameHeightMinInches) /
                                (FrameHeightMaxInches - FrameHeightMinInches);

                tooCloseInfo.hasData = true;
                tooCloseInfo.hasIssueUnderLooseCondition =
                    distanceRatio < DistanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooCloseInfo.hasIssueUnderStrictCondition =
                    distanceRatio < -DistanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooFarInfo.hasData = true;
                tooFarInfo.hasIssueUnderLooseCondition =
                    distanceRatio > 1 - DistanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooFarInfo.hasIssueUnderStrictCondition =
                    distanceRatio > 1 + DistanceRatioStrictLooseHalfMarginForDistanceIssue;

                // Play Area
                var playAreaInNormalizedSpace = playAreaController.GetPlayAreaInNormalizedSpace();

                // Distance in Play Area
                var playAreaHeightInRawFrameSpace = playAreaInNormalizedSpace.height * frameSize.y;
                var playAreaHeightInInches = playAreaHeightInRawFrameSpace / ppi;

                distanceRatioInPlayArea = (playAreaHeightInInches - PlayAreaHeightMinInches) /
                                              (PlayAreaHeightMaxInches - PlayAreaHeightMinInches);

                tooCloseInPlayAreaInfo.hasData = true;
                tooCloseInPlayAreaInfo.hasIssueUnderLooseCondition =
                    distanceRatioInPlayArea < DistanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooCloseInPlayAreaInfo.hasIssueUnderStrictCondition =
                    distanceRatioInPlayArea < -DistanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooFarInPlayAreaInfo.hasData = true;
                tooFarInPlayAreaInfo.hasIssueUnderLooseCondition =
                    distanceRatioInPlayArea > 1 - DistanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooFarInPlayAreaInfo.hasIssueUnderStrictCondition =
                    distanceRatioInPlayArea > 1 + DistanceRatioStrictLooseHalfMarginForDistanceIssue;

                var safeAreaX1 = ppi * ChestToLeftMinInches;
                var safeAreaX2 = frameSize.x - ppi * ChestToRightMinInches;
                var safeAreaY1 = ppi * ChestToTopMinInches;
                var safeAreaY2 = frameSize.y - ppi * ChestToBottomMinInches;

                var xRatio = PlayerPositionDefinition.GetXRatioForPlayer(playerIndex, poseDetection.NumOfPlayers());
                var playerCenterX = frameSize.x * (playAreaInNormalizedSpace.x + playAreaInNormalizedSpace.width * xRatio);

                var chestStrictLooseHalfMarginPixels = ChestStrictLooseHalfMarginInches * ppi;
                var safeMaxXDistance = ppi * ChestXToCenterMaxInches;
                var toCenterXDistance = Math.Abs(chestPt.x - playerCenterX);

                chestTooHighInfo.hasData = true;
                chestTooHighInfo.hasIssueUnderLooseCondition =
                    chestPt.y < safeAreaY1 + chestStrictLooseHalfMarginPixels;
                chestTooHighInfo.hasIssueUnderStrictCondition =
                    chestPt.y < safeAreaY1 - chestStrictLooseHalfMarginPixels;
                chestTooLowInfo.hasData = true;
                chestTooLowInfo.hasIssueUnderLooseCondition =
                    chestPt.y > safeAreaY2 - chestStrictLooseHalfMarginPixels;
                chestTooLowInfo.hasIssueUnderStrictCondition =
                    chestPt.y > safeAreaY2 + chestStrictLooseHalfMarginPixels;
                chestTooLeftInfo.hasData = true;
                chestTooLeftInfo.hasIssueUnderLooseCondition =
                    chestPt.x < safeAreaX1 + chestStrictLooseHalfMarginPixels;
                chestTooLeftInfo.hasIssueUnderStrictCondition =
                    chestPt.x < safeAreaX1 - chestStrictLooseHalfMarginPixels;
                chestTooRightInfo.hasData = true;
                chestTooRightInfo.hasIssueUnderLooseCondition =
                    chestPt.x > safeAreaX2 - chestStrictLooseHalfMarginPixels;
                chestTooRightInfo.hasIssueUnderStrictCondition =
                    chestPt.x > safeAreaX2 + chestStrictLooseHalfMarginPixels;
                notAtCenterInfo.hasData = true;
                notAtCenterInfo.hasIssueUnderLooseCondition =
                    toCenterXDistance > safeMaxXDistance - chestStrictLooseHalfMarginPixels;
                notAtCenterInfo.hasIssueUnderStrictCondition =
                    toCenterXDistance > safeMaxXDistance + chestStrictLooseHalfMarginPixels;
            }

            issueInfoByType[SetupIssueType.TooClose] = tooCloseInfo;
            issueInfoByType[SetupIssueType.TooFar] = tooFarInfo;
            issueInfoByType[SetupIssueType.ChestTooHigh] = chestTooHighInfo;
            issueInfoByType[SetupIssueType.ChestTooLow] = chestTooLowInfo;
            issueInfoByType[SetupIssueType.ChestTooLeft] = chestTooLeftInfo;
            issueInfoByType[SetupIssueType.ChestTooRight] = chestTooRightInfo;
            issueInfoByType[SetupIssueType.NoPose] = noPoseInfo;
            issueInfoByType[SetupIssueType.NotAtCenter] = notAtCenterInfo;
            issueInfoByType[SetupIssueType.TooCloseInPlayArea] = tooCloseInPlayAreaInfo;
            issueInfoByType[SetupIssueType.TooFarInPlayArea] = tooFarInPlayAreaInfo;

            // Update history
            foreach (var type in issueTypesSortedByDisplayPriority)
            {
                History<SetupIssueInfo> history = issueInfoHistoryByType[type];
                history.Add(issueInfoByType[type], time);
                history.UpdateCurrentFrameTime(time);
            }
        }

        void AnalyzeDetectionHistory()
        {
            foreach (var type in issueTypesSortedByDisplayPriority)
            {
                var isIssueActivated = isIssueActivatedByType[type];
                History<SetupIssueInfo> history = issueInfoHistoryByType[type];

                float oppositeStateTimeSum = 0;
                float allTimeSum = 0;
                for (var i = 1; i < history.items.Count; i++)
                {
                    TimedItem<SetupIssueInfo> curItem = history.items[i];
                    var timeInterval = (float)Math.Abs(curItem.frameTime - history.items[i - 1].frameTime);

                    // This design is to smooth the state change.
                    // The state range is:
                    // hasIssueUnderStrict --- hasIssueUnderLoose --- no issue.
                    // If the issue is already activated, when the state goes to no issue, then deactivate it.
                    // If the issue is not activated, when the state goes to hasIssueUnderStrict, then activate it.
                    // In this way, when the state is between strict & loose, it won't be activated & deactivated.
                    // And we only change state when the opposite state occupies significant portion of the short history.

                    // There is a case "60% no-pose + 40% too-close" which is pretty common in
                    // a 2P situation (because a too-close pose move from P1 spot to P2 spot). In this
                    // case we want it to be either "no-pose" or "too-close" rather than "no issue". So
                    // So we need the hasData flag to make sure "too-close" is still true even if
                    // 60% data is no-pose.
                    if (curItem.item.hasData)
                    {
                        var isOppositeState = isIssueActivated
                            ? !curItem.item.hasIssueUnderLooseCondition
                            : curItem.item.hasIssueUnderStrictCondition;

                        if (isOppositeState)
                        {
                            oppositeStateTimeSum += timeInterval;
                        }

                        allTimeSum += timeInterval;
                    }
                }

                if (allTimeSum >= IssueMinRequiredDataTime)
                {
                    var changeStateRatio = isIssueActivated ? IssueCancelStateRatio : IssueEntryStateRatio;
                    if (oppositeStateTimeSum > allTimeSum * changeStateRatio)
                    {
                        isIssueActivatedByType[type] = !isIssueActivated;
                    }
                }
                else
                {
                    // Cancel the issue because the data is not enough.
                    isIssueActivatedByType[type] = false;
                }
            }

            currentIssue = SetupIssueType.None;
            // NOTE: no LINQ for memory optimisation.
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var type in issueTypesSortedByDisplayPriority)
            {
                if (isIssueActivatedByType[type]) {
                    currentIssue = type;
                    break;
                }
            }
        }
    }
}
