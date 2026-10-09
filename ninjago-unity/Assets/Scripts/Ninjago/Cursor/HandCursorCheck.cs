#nullable enable

using System.Collections.Generic;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// The setup step of the hand cursor games: each calibrated player raises a hand and parks one of their own cursors
    /// in their ring until it fills, which proves that player's hands drive a cursor. The camera preview stays up.
    /// </summary>
    public class HandCursorCheck : MonoBehaviour
    {
        [Header("Check Root")]
        [Tooltip("Hidden until the body calibration is done.")]
        [SerializeField] GameObject root = null!;
        [Header("Cursor Layer")]
        [SerializeField] HandCursorLayer cursorLayer = null!;
        [Header("Target Prefab")]
        [SerializeField] HandCursorTarget targetPrefab = null!;
        [Header("Targets Root")]
        [SerializeField] RectTransform targetsRoot = null!;
        [Header("Target Height")]
        [Tooltip("Screen height (0 bottom, 1 top) of each ring: above the chest, so the player raises a hand.")]
        [SerializeField, Range(0f, 1f)] float targetHeight = 0.6f;
        [Header("Prompt Label")]
        [SerializeField] NexLocalizedString promptLabel = null!;
        [Header("Prompt: Raise A Hand")]
        [SerializeField] LocalizedString raisePrompt = new();
        [Header("Prompt: Ready")]
        [SerializeField] LocalizedString readyPrompt = new();
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;

        readonly List<HandCursorTarget> targets = new();
        readonly List<float> progress = new();
        IReadOnlyList<PlayerBody> players = null!;
        HandCursorTracker tracker = null!;

        #region Public API

        public void Show(IReadOnlyList<PlayerBody> aPlayers, HandCursorTracker aTracker, DetectionManager detection, int requestedPlayers)
        {
            players = aPlayers;
            tracker = aTracker;
            root.SetActive(true);
            promptLabel.StringReference = raisePrompt;
            var indicatorPlayers = new List<int>(players.Count);
            for (var slot = 0; slot < players.Count; slot++)
            {
                var player = players[slot];
                var region = SplitScreen.Region(slot, players.Count);
                tracker.SetRegion(player.PlayerIndex, region);
                var target = Instantiate(targetPrefab, targetsRoot);
                target.Initialize(player.PlayerIndex, player.Color, cursorLayer.ToLocal(new Vector2(region.center.x, targetHeight)));
                targets.Add(target);
                progress.Add(0f);
                indicatorPlayers.Add(player.PlayerIndex);
            }

            cursorLayer.Initialize(tracker, players);
            pipFrame.Initialize(detection.PlayAreaController);
            pipIndicators.Initialize(requestedPlayers, indicatorPlayers, pipFrame, detection.BodyPoseDetectionManager);
        }

        /// <summary>Advances one player's ring; true on the frame it fills.</summary>
        public bool Tick(int slot, float deltaTime, float holdSeconds)
        {
            var inside = IsInside(players[slot].PlayerIndex, targets[slot]);
            var step = deltaTime / holdSeconds;
            progress[slot] = Mathf.Clamp01(progress[slot] + (inside ? step : -2f * step));
            targets[slot].SetFill(progress[slot]);
            if (progress[slot] < 1f) return false;
            targets[slot].ShowDone();
            return true;
        }

        public void ShowReady()
        {
            promptLabel.StringReference = readyPrompt;
        }

        #endregion

        #region Helpers

        bool IsInside(int playerIndex, HandCursorTarget target)
        {
            for (var hand = 0; hand < 2; hand++)
            {
                var cursor = tracker.GetCursor(playerIndex, hand);
                if (cursor.IsVisible && target.Contains(cursorLayer.ToLocal(cursor.ScreenPosition))) return true;
            }

            return false;
        }

        #endregion
    }
}
