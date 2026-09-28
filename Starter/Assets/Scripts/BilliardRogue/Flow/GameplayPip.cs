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
    /// </summary>
    public sealed class GameplayPip : MonoBehaviour
    {
        [Header("Preview")]
        [SerializeField] AreaPreviewFrame previewFrame = null!;
        [SerializeField] PlayerIndicatorsManager indicators = null!;
        [Tooltip("Faded instead of scaled: the preview frame caches its aspect ratio on the first texture.")]
        [SerializeField] CanvasGroup canvasGroup = null!;

        public void Initialize(int numPlayers, BasePlayAreaController playArea, BodyPoseDetectionManager bodyPoseDetectionManager)
        {
            previewFrame.Initialize(playArea);
            indicators.Initialize(numPlayers, previewFrame, bodyPoseDetectionManager);
        }

        /// <summary>Highlights the shooter's indicator; -1 shows every indicator neutral.</summary>
        public void SetActivePlayer(int playerIndex)
        {
            indicators.SetActivePlayer(playerIndex);
        }

        /// <summary>Fades the feed (duration 0 = instant); unscaled time, so it also runs while the game is paused.</summary>
        public void SetVisible(bool visible, float duration = 0f)
        {
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
