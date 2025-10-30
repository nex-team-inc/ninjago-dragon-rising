#nullable enable

using Jazz;
using UnityEngine;

namespace Nex
{
    public class OnePlayerPreviewPoseEngine : BaseOnePlayerDetectionEngine
    {
        PreviewFrameBase previewFrame = null!;

        #region Initialization

        public void Initialize(
            int aPlayerIndex,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            PreviewFrameBase aPreviewFrame,
            bool aEnableOriginalNodes = true,
            bool aEnableSmoothedNodes = true
        )
        {
            base.Initialize(aPlayerIndex, aBodyPoseDetectionManager, aEnableOriginalNodes, aEnableSmoothedNodes);

            previewFrame = aPreviewFrame;
        }

        #endregion

        #region Transform

        protected override Vector3 AspectNormalSpaceToWorldSpace(Vector2 vec)
        {
            var aspectNormalFrameSize = DetectionUtils.AspectNormalizedFrameSize;
            var previewRectInNormalizedSpace = previewFrame.PreviewRectInNormalizedSpace();
            var previewRectInAspectNormalizedSpace = new Rect(
                previewRectInNormalizedSpace.x * aspectNormalFrameSize.x,
                previewRectInNormalizedSpace.y * aspectNormalFrameSize.y,
                previewRectInNormalizedSpace.width * aspectNormalFrameSize.x,
                previewRectInNormalizedSpace.height * aspectNormalFrameSize.y
            );
            var previewRectInWorldSpace = previewFrame.PreviewRectInWorldSpace();

            var newX = (vec.x - previewRectInAspectNormalizedSpace.x) / previewRectInAspectNormalizedSpace.width * previewRectInWorldSpace.width + previewRectInWorldSpace.x;
            var newY = (vec.y - previewRectInAspectNormalizedSpace.y) / previewRectInAspectNormalizedSpace.height * previewRectInWorldSpace.height + previewRectInWorldSpace.y;

            return new Vector2(newX, newY);
        }

        protected override float ConvertRawPpiToDpi(float rawPpi)
        {
            var aspectNormalFrameSize = DetectionUtils.AspectNormalizedFrameSize;
            var previewRectInNormalizedSpace = previewFrame.PreviewRectInNormalizedSpace();
            var previewRectHeightInAspectNormalizedSpace =
                previewRectInNormalizedSpace.height * aspectNormalFrameSize.y;
            var previewRectInWorldSpace = previewFrame.PreviewRectInWorldSpace();
            return RawPpi / previewRectHeightInAspectNormalizedSpace * previewRectInWorldSpace.height;
        }

        protected override BodyPose? CloneAndProcessPoseIfNeeded(BodyPose? pose)
        {
            return pose;
        }

        #endregion
    }
}
