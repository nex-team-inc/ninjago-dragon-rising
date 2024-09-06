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
        TooFarInProcessFrame,
        TooCloseInProcessFrame,
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
        [SerializeField] public BodyPoseDetectionManager bodyDetector;
        [SerializeField] public int playerIndex;

        [SerializeField] public float chestStrictLooseHalfMarginInches = 1;
        [SerializeField] public float chestToTopMinInches = 12;
        [SerializeField] public float chestToBottomMinInches = 18;
        [SerializeField] public float chestToLeftMinInches = 12;
        [SerializeField] public float chestToRightMinInches = 12;
        [SerializeField] public float chestXToCenterMaxInches = 1000;

        [SerializeField] public float issueEvaluationTimeWindow = 1f;
        [SerializeField] public float issueMinRequiredDataTime = 0.2f;
        [SerializeField] public float issueEntryStateRatio = 0.8f;
        [SerializeField] public float issueCancelStateRatio = 0.4f;

        [SerializeField] public float distanceRatioStrictLooseHalfMarginForDistanceIssue = 0.03f;
        [SerializeField] public float frameHeightMinInches = 40;
        [SerializeField] public float frameHeightMaxInches = 90;
        [SerializeField] public float processFrameHeightMinInches = 40;
        [SerializeField] public float processFrameHeightMaxInches = 90;

        float ppi;
        float distanceRatio;
        float distanceRatioInProcessFrame;
        SetupIssueType currentIssue;

        bool hasEnoughData;
        float startDetectionTime;

        readonly Dictionary<SetupIssueType, bool> isIssueActivatedByType = new Dictionary<SetupIssueType, bool>();
        readonly Dictionary<SetupIssueType, SetupIssueInfo> issueInfoByType = new Dictionary<SetupIssueType, SetupIssueInfo>();
        readonly Dictionary<SetupIssueType, History<SetupIssueInfo>> issueInfoHistoryByType = new Dictionary<SetupIssueType, History<SetupIssueInfo>>();

        List<SetupIssueType> issueTypesSortedByDisplayPriority;

        PlayAreaController playAreaController = null!;

        public event UnityAction<SetupDetection> captureDetection;

        // MARK - Public

        // MARK - Life Cycle

        public void Initialize(
            PlayAreaController aPlayAreaController,
            List<SetupIssueType> aIssueTypesSortedByDisplayPriority = null)
        {
            playAreaController = aPlayAreaController;

            if (aIssueTypesSortedByDisplayPriority == null)
            {
                issueTypesSortedByDisplayPriority = new List<SetupIssueType>
                {
                    SetupIssueType.TooClose,
                    SetupIssueType.TooFar,
                    // SetupIssueType.TooCloseInProcessFrame,
                    // SetupIssueType.TooFarInProcessFrame,
                    SetupIssueType.ChestTooHigh,
                    SetupIssueType.ChestTooLow,
                    SetupIssueType.ChestTooLeft,
                    SetupIssueType.ChestTooRight,
                    SetupIssueType.NoPose,
                    SetupIssueType.NotAtCenter,
                };
            }

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

        void AnnounceDetection()
        {
            var detection = new SetupDetection
            {
                hasEnoughData = hasEnoughData,
                currentIssue = currentIssue
            };

            captureDetection?.Invoke(detection);
        }

        void ResetAllStates()
        {
            // Initialise the values of hasIssueByType
            foreach (SetupIssueType type in issueTypesSortedByDisplayPriority)
            {
                isIssueActivatedByType[type] = false;
                issueInfoByType[type] = new SetupIssueInfo();
                issueInfoHistoryByType[type] = new History<SetupIssueInfo>(issueEvaluationTimeWindow);
            }

            startDetectionTime = -1;
            hasEnoughData = false;
        }

        void ProcessPoseDetection(BodyPoseDetection poseDetection)
        {
            var playerPose = poseDetection.GetPlayerPose(playerIndex);

            var time = Time.fixedTime;
            startDetectionTime = startDetectionTime < 0 ? time : startDetectionTime; // Only set it once when it's negative.
            hasEnoughData = time - startDetectionTime > issueEvaluationTimeWindow;

            var pose = playerPose?.bodyPose;

            var tooCloseInfo = new SetupIssueInfo();
            var tooFarInfo = new SetupIssueInfo();
            var chestTooHighInfo = new SetupIssueInfo();
            var chestTooLowInfo = new SetupIssueInfo();
            var chestTooLeftInfo = new SetupIssueInfo();
            var chestTooRightInfo = new SetupIssueInfo();
            var noPoseInfo = new SetupIssueInfo();
            var notAtCenterInfo = new SetupIssueInfo();
            var tooCloseInProcessFrameInfo = new SetupIssueInfo();
            var tooFarInProcessFrameInfo = new SetupIssueInfo();

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
                var processFrameCrop = poseDetection.GetProcessFrameTransformInfo().processFrameCrop;

                // Distance in Raw Frame
                var frameHeightInInches = frameSize.y / ppi;

                distanceRatio = (frameHeightInInches - frameHeightMinInches) /
                                (frameHeightMaxInches - frameHeightMinInches);

                tooCloseInfo.hasData = true;
                tooCloseInfo.hasIssueUnderLooseCondition =
                    distanceRatio < distanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooCloseInfo.hasIssueUnderStrictCondition =
                    distanceRatio < -distanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooFarInfo.hasData = true;
                tooFarInfo.hasIssueUnderLooseCondition =
                    distanceRatio > 1 - distanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooFarInfo.hasIssueUnderStrictCondition =
                    distanceRatio > 1 + distanceRatioStrictLooseHalfMarginForDistanceIssue;

                // Distance in Process Frame
                var processFrameHeightInInches = processFrameCrop.height / ppi;

                distanceRatioInProcessFrame = (processFrameHeightInInches - processFrameHeightMinInches) /
                                              (processFrameHeightMaxInches - processFrameHeightMinInches);

                tooCloseInProcessFrameInfo.hasData = true;
                tooCloseInProcessFrameInfo.hasIssueUnderLooseCondition =
                    distanceRatioInProcessFrame < distanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooCloseInProcessFrameInfo.hasIssueUnderStrictCondition =
                    distanceRatioInProcessFrame < -distanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooFarInProcessFrameInfo.hasData = true;
                tooFarInProcessFrameInfo.hasIssueUnderLooseCondition =
                    distanceRatioInProcessFrame > 1 - distanceRatioStrictLooseHalfMarginForDistanceIssue;
                tooFarInProcessFrameInfo.hasIssueUnderStrictCondition =
                    distanceRatioInProcessFrame > 1 + distanceRatioStrictLooseHalfMarginForDistanceIssue;

                var safeAreaX1 = ppi * chestToLeftMinInches;
                var safeAreaX2 = frameSize.x - ppi * chestToRightMinInches;
                var safeAreaY1 = ppi * chestToTopMinInches;
                var safeAreaY2 = frameSize.y - ppi * chestToBottomMinInches;

                var playAreaInNormalizedSpace = playAreaController.GetPlayAreaInNormalizedSpace();
                var xRatio = PlayerPositionDefinition.GetXRatioForPlayer(playerIndex, poseDetection.NumOfPlayers());
                var playerCenterX = frameSize.x * (playAreaInNormalizedSpace.x + playAreaInNormalizedSpace.width * xRatio);

                var chestStrictLooseHalfMarginPixels = chestStrictLooseHalfMarginInches * ppi;
                var safeMaxXDistance = ppi * chestXToCenterMaxInches;
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
            issueInfoByType[SetupIssueType.TooCloseInProcessFrame] = tooCloseInProcessFrameInfo;
            issueInfoByType[SetupIssueType.TooFarInProcessFrame] = tooFarInProcessFrameInfo;

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

                if (allTimeSum >= issueMinRequiredDataTime)
                {
                    var changeStateRatio = isIssueActivated ? issueCancelStateRatio : issueEntryStateRatio;
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
