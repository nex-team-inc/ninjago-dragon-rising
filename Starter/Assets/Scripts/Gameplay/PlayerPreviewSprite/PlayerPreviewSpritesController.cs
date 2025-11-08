#nullable enable

using Jazz;
using UnityEngine;

namespace Nex
{
    public class PlayerPreviewSpritesController : MonoBehaviour
    {
        #region Initialization

        public void Initialize(
            int aPlayerIndex,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager)
        {
            var previewSprites = gameObject.GetComponentsInChildren<PlayerPreviewSprite>();
            foreach (var sprite in previewSprites)
            {
                sprite.Initialize(
                    aPlayerIndex,
                    aCvDetectionManager,
                    aBodyPoseDetectionManager);
            }
        }

        #endregion
    }
}
