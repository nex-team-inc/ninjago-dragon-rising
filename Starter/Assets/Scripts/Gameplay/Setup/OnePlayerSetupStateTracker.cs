using System;
using System.Collections.Generic;
using Jazz;
using UnityEngine;
using UnityEngine.Events;

#nullable enable

namespace Nex
{
    public enum SetupCheckType
    {
        GoodPosition,
        RaisingHand
    }

    public struct SetupHistoryItem
    {
        public SetupIssueType setupIssue;
        public bool isRaisingLeftHand;
        public bool isRaisingRightHand;

        #region Public

        public void UpdateStateWithPose(BodyPose? pose)
        {
            if (pose == null)
            {
                isRaisingLeftHand = false;
                isRaisingRightHand = false;
                return;
            }

            isRaisingLeftHand = IsRaisingHand(pose.LeftWrist(), pose.LeftShoulder());
            isRaisingRightHand = IsRaisingHand(pose.RightWrist(), pose.RightShoulder());
        }

        public bool Check(SetupCheckType checkType)
        {
            return checkType switch
            {
                SetupCheckType.GoodPosition => setupIssue == SetupIssueType.None,
                SetupCheckType.RaisingHand => isRaisingLeftHand || isRaisingRightHand,
                _ => throw new ArgumentOutOfRangeException(nameof(checkType), checkType, null)
            };
        }

        #endregion

        #region Helper

        bool IsRaisingHand(PoseNode wrist, PoseNode shoulder)
        {
            if (!wrist.isDetected || !shoulder.isDetected)
            {
                return false;
            }

            return wrist.y > shoulder.y;
        }

        #endregion
    }

    public enum SetupStateType
    {
        Preparing = 0,
        WaitingForGoodPlayerPosition = 1,
        WaitingForRaisingHand = 2,
        Playing = 3,
        PlayingButNoPose = 4
    }

    public struct SetupSummary
    {
        public SetupStateType setupStateType;
        public SetupIssueType currentSetupIssue;
        public bool isStateChanged;

        public float goodPositionProgress;
        public float raiseHandProgress;
        public float noPlayerDuration;

        public static SetupSummary CreateDummy()
        {
            return new SetupSummary
            {
                setupStateType = SetupStateType.Preparing,
                currentSetupIssue = SetupIssueType.None,
            };
        }
    }

    public class OnePlayerSetupStateTracker : MonoBehaviour
    {
        const float state0GoodPositionRatioThresholdStrict = 0.7f;
        const float state0GoodPositionRatioThresholdLoose = 0.5f;
        const float state0GoodPositionCheckDuration = 1.5f;
        const float state1RaiseHandRatioThreshold = 0.7f;
        const float state1RaiseHandCheckDuration = 1f;
        const float state2NoPlayerDurationThreshold = 2;
        const float historyDurationInSeconds = 4f;

        [SerializeField] SetupDetector setupDetectorPrefab = null!;

        SetupDetector setupDetector = null!;
        History<SetupHistoryItem> setupHistory = null!;

        SetupStateType curState = SetupStateType.Preparing;
        bool allowPassingRaisingHandState;

        int playerIndex;
        BodyPose? lastPose;
        float lastPlayerIsSeenTimestamp;
        float lastStateStartTime;
        bool isTracking;

        public event UnityAction<SetupSummary>? Updated;

        #region Public

        public void Initialize(
            int aPlayerIndex,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            BasePlayAreaController playAreaController,
            SetupDetectorWarningConfig aSetupDetectorWarningConfig
        )
        {
            playerIndex = aPlayerIndex;

            setupHistory = new History<SetupHistoryItem>(historyDurationInSeconds);

            setupDetector = Instantiate(setupDetectorPrefab, transform);
            InitializeSetupDetector(setupDetector, aBodyPoseDetectionManager, playerIndex, playAreaController, aSetupDetectorWarningConfig);
            setupDetector.captureDetection += SetupDetectorOnCaptureDetection;

            aBodyPoseDetectionManager.processed.captureAspectNormalizedDetection += ProcessedOnCaptureAspectNormalizedDetection;
        }

        public void SetIsTracking(bool value)
        {
            isTracking = value;
        }

        public void SetAllowPassingRaisingHandState(bool value)
        {
            allowPassingRaisingHandState = value;
        }

        #endregion

        #region Setup Detector

        static void InitializeSetupDetector(
            SetupDetector setupDetector,
            BodyPoseDetectionManager bodyPoseDetectionManager,
            int playerIndex,
            BasePlayAreaController playAreaController,
            SetupDetectorWarningConfig aSetupDetectorWarningConfig
        )
        {
            setupDetector.Initialize(
                playerIndex,
                bodyPoseDetectionManager,
                playAreaController,
                aSetupDetectorWarningConfig,
                new List<SetupIssueType>
                {
                    // NOTE: if we will notify users to move closer or step back
                    // we should enable the below warnings.
                    SetupIssueType.NotAtCenter,
                    SetupIssueType.TooClose,
                    SetupIssueType.TooFar,
                    SetupIssueType.TooCloseInPlayArea,
                    SetupIssueType.TooFarInPlayArea,
                    SetupIssueType.ChestTooHigh,
                    SetupIssueType.ChestTooLow,
                    SetupIssueType.NoPose,
                });
        }

        void SetupDetectorOnCaptureDetection(SetupDetection detection)
        {
            if (!isTracking)
            {
                return;
            }

            var setupState = new SetupHistoryItem
            {
                setupIssue = detection.currentIssue
            };

            setupState.UpdateStateWithPose(lastPose);

            setupHistory.UpdateCurrentFrameTime(Time.fixedTime);
            setupHistory.Add(setupState, Time.fixedTime);

            var summary = new SetupSummary
            {
                currentSetupIssue = detection.currentIssue
            };

            if (detection.hasEnoughData && detection.currentIssue != SetupIssueType.NoPose)
            {
                lastPlayerIsSeenTimestamp = Time.fixedTime;
            }

            switch (curState)
            {
                case SetupStateType.Preparing:
                {
                    if (detection.hasEnoughData)
                    {
                        summary.isStateChanged = true;
                        ChangeState(SetupStateType.WaitingForGoodPlayerPosition);
                    }

                    break;
                }
                case SetupStateType.WaitingForGoodPlayerPosition:
                {
                    // Check forward: good pose.
                    var goodPositionRatio = CheckRatio(SetupCheckType.GoodPosition, state0GoodPositionCheckDuration);
                    summary.goodPositionProgress = goodPositionRatio / state0GoodPositionRatioThresholdStrict;
                    if (goodPositionRatio > state0GoodPositionRatioThresholdStrict)
                    {
                        summary.isStateChanged = true;
                        ChangeState(SetupStateType.WaitingForRaisingHand);
                    }

                    break;
                }
                case SetupStateType.WaitingForRaisingHand:
                {
                    // Check forward: raise hand.
                    var raiseHandRatio = CheckRatio(SetupCheckType.RaisingHand, state1RaiseHandCheckDuration, lastStateStartTime);
                    summary.raiseHandProgress = Mathf.Min(raiseHandRatio / state1RaiseHandRatioThreshold, 1f);
                    if (raiseHandRatio > state1RaiseHandRatioThreshold && allowPassingRaisingHandState)
                    {
                        summary.isStateChanged = true;
                        ChangeState(SetupStateType.Playing);
                    }
                    else
                    {
                        // Check backward: bad pose.
                        var goodPositionRatio = CheckRatio(SetupCheckType.GoodPosition, state0GoodPositionCheckDuration);
                        if (goodPositionRatio < state0GoodPositionRatioThresholdLoose)
                        {
                            summary.isStateChanged = true;
                            ChangeState(SetupStateType.WaitingForGoodPlayerPosition);
                        }
                    }

                    break;
                }
                case SetupStateType.Playing:
                {
                    // Check backward: no pose.
                    var noPlayerDuration = Time.fixedTime - Math.Max(lastStateStartTime, lastPlayerIsSeenTimestamp);
                    summary.noPlayerDuration = noPlayerDuration;
                    if (noPlayerDuration > state2NoPlayerDurationThreshold)
                    {
                        summary.isStateChanged = true;
                        ChangeState(SetupStateType.PlayingButNoPose);
                    }

                    break;
                }
                case SetupStateType.PlayingButNoPose:
                {
                    // Check whether to get back to playing
                    var goodPositionRatio = CheckRatio(SetupCheckType.GoodPosition, state0GoodPositionCheckDuration);
                    summary.goodPositionProgress = goodPositionRatio / state0GoodPositionRatioThresholdStrict;
                    if (goodPositionRatio > state0GoodPositionRatioThresholdStrict)
                    {
                        summary.isStateChanged = true;
                        ChangeState(SetupStateType.Playing);
                    }
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            summary.setupStateType = curState;
            Updated?.Invoke(summary);
        }

        #endregion

        #region Pose Detection

        void ProcessedOnCaptureAspectNormalizedDetection(BodyPoseDetectionResult detectionResult)
        {
            if (!isTracking)
            {
                return;
            }

            var playerPose = detectionResult.processed.GetPlayerPose(playerIndex);
            lastPose = playerPose?.bodyPose;
        }

        #endregion

        #region State

        float CheckRatio(SetupCheckType checkType, float duration, float startTime = 0)
        {
            if (setupHistory.items.Count == 0)
            {
                return 0;
            }

            var totalYesTime = 0f;
            var minTimestamp = Math.Max(Time.fixedTime - duration, startTime);
            for (var i = 0; i < setupHistory.items.Count - 1; i++)
            {
                TimedItem<SetupHistoryItem> curItem = setupHistory.items[i];
                TimedItem<SetupHistoryItem> prevItem = setupHistory.items[i + 1];

                if (curItem.frameTime < minTimestamp)
                {
                    // Too old
                    break;
                }

                if (curItem.item.Check(checkType))
                {
                    totalYesTime += (float)curItem.frameTime - Math.Max(minTimestamp, (float)prevItem.frameTime);
                }
            }

            return totalYesTime / duration;
        }

        void ChangeState(SetupStateType stateType)
        {
            if (curState == stateType)
            {
                return;
            }

            curState = stateType;
            lastStateStartTime = Time.fixedTime;
        }

        #endregion

        #region Setup Detector Mode

        public void SetSetupDetectorMode(SetupDetectorMode mode)
        {
            setupDetector.SetDetectorMode(mode);
        }

        #endregion
    }
}
