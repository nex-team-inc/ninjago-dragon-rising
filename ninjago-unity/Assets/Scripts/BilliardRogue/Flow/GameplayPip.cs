#nullable enable

using DG.Tweening;
using Jazz;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Corner camera feed during gameplay (TDD D5): AreaPreviewFrame + PlayerIndicatorsManager on their own
    /// Screen Space Overlay canvas, so the feed stays out of the world post-processing. It covers the screen of the
    /// HUD's camera panel (UiHudBuilder.PipFeedScreenPosition). Instantiated as a scene root by GameplayView and
    /// destroyed with it; the overlay draws above every view, so GameplayView hides it under its overlays.
    /// While alive it sets how often the camera refreshes its preview textures: the feed is the only consumer.
    /// </summary>
    public sealed class GameplayPip : MonoBehaviour
    {
        [Header("Preview")]
        [SerializeField] AreaPreviewFrame previewFrame = null!;
        [SerializeField] PlayerIndicatorsManager indicators = null!;
        [Tooltip("Faded instead of scaled: the preview frame caches its aspect ratio on the first texture.")]
        [SerializeField] CanvasGroup canvasGroup = null!;

        DetectionManager detection = null!;
        float visiblePreviewInterval;

        /// <summary>previewInterval: seconds between camera preview refreshes while the feed shows (0 = every camera frame).</summary>
        public void Initialize(int numPlayers, DetectionManager aDetection, float previewInterval)
        {
            detection = aDetection;
            visiblePreviewInterval = previewInterval;
            previewFrame.Initialize(detection.PlayAreaController);
            indicators.Initialize(numPlayers, previewFrame, detection.BodyPoseDetectionManager);
        }

        void OnDestroy()
        {
            // A calibration after this run reuses the camera session and needs the full preview rate. The session may
            // already be torn down on scene unload.
            if (detection != null)
            {
                detection.SetPreviewTextureInterval(0f);
            }
        }

        /// <summary>Highlights the shooter's indicator; -1 shows every indicator neutral.</summary>
        public void SetActivePlayer(int playerIndex)
        {
            indicators.SetActivePlayer(playerIndex);
        }

        /// <summary>Fades the feed (duration 0 = instant); unscaled time, so it also runs while the game is paused.</summary>
        public void SetVisible(bool visible, float duration = 0f)
        {
            // A hidden feed needs no camera texture uploads.
            detection.SetPreviewTextureInterval(visible ? visiblePreviewInterval : DetectionManager.PreviewTextureOff);
            canvasGroup.DOKill();
            var alpha = visible ? 1f : 0f;
            if (duration <= 0f)
            {
                canvasGroup.alpha = alpha;
                return;
            }

            canvasGroup.DOFade(alpha, duration).SetUpdate(true).SetLink(gameObject);
        }
    }
}
