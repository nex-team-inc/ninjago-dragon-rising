#nullable enable

using Cysharp.Threading.Tasks;
using Jazz;
using MoreMountains.Feedbacks;
using Nex.Utils;
using TMPro;
using UnityEngine;

namespace Nex
{
    public class PreviewsManager : MonoBehaviour
    {
        [SerializeField] SetupInfoPreviewFrame setupInfoPreviewFramePrefab = null!;
        [SerializeField] GameObject previewsContainer = null!;
        [SerializeField] TMP_Text setupPromptText = null!;

        [Header("Animations")]
        [SerializeField] MMFeedbacks moveInAnimator = null!;
        [SerializeField] MMFeedbacks moveOutAnimator = null!;

        #region Public

        public void Initialize(
            int aNumOfPlayers,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            BasePlayAreaController playAreaController,
            SetupStateManager setupStateManager
        )
        {
            // This only works for showing preview for individual players.
            for (var playerIndex = 0; playerIndex < aNumOfPlayers; playerIndex++)
            {
                var previewFrame = Instantiate(setupInfoPreviewFramePrefab, previewsContainer.transform);
                previewFrame.Initialize(playerIndex, aNumOfPlayers, aCvDetectionManager, aBodyPoseDetectionManager, playAreaController, setupStateManager);
            }

            MoveOut(false).Forget();
        }

        public void SetPromptText(string text)
        {
            setupPromptText.text = text;
        }

        public async UniTask MoveIn(bool animated)
        {
            await moveInAnimator.PlayAsUniTask(animated);
        }

        public async UniTask MoveOut(bool animated)
        {
            await moveOutAnimator.PlayAsUniTask(animated);
        }

        #endregion
    }
}
