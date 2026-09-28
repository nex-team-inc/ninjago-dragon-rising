#nullable enable

using Jazz;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Corner camera preview during gameplay (TDD D5): AreaPreviewFrame + PlayerIndicatorsManager on their own
    /// Screen Space Overlay canvas, so the feed stays out of the world post-processing. Instantiated as a scene root
    /// by GameplayView and destroyed with it.
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

        public void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
        }
    }
}
