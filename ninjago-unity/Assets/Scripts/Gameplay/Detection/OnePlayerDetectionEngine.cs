using Jazz;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class OnePlayerDetectionEngine : BaseOnePlayerDetectionEngine
    {
        const float totalHeightInInches = 58;

        [Header("Reference Frame")]
        [SerializeField] RectTransform referenceTransform = null!;

        #region Initialization

        public new void Initialize(
            int aPlayerIndex,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            bool aEnableOriginalNodes = true,
            bool aEnableSmoothedNodes = true
        )
        {
            base.Initialize(aPlayerIndex, aBodyPoseDetectionManager, aEnableOriginalNodes, aEnableSmoothedNodes);
        }

        #endregion

        #region Transform

        protected override Vector3 AspectNormalSpaceToWorldSpace(Vector2 vec)
        {
            var aspectNormalizedFrameSize = DetectionUtils.AspectNormalizedFrameSize;
            var xRate = (vec.x - aspectNormalizedFrameSize.x * 0.5f) / aspectNormalizedFrameSize.x;
            var yRate = (vec.y - aspectNormalizedFrameSize.y * 0.5f) / aspectNormalizedFrameSize.y;
            var newX = xRate * referenceTransform.rect.width + referenceTransform.localPosition.x;
            var newY = yRate * referenceTransform.rect.height + referenceTransform.localPosition.y;
            return new Vector3(newX, newY, 0f) + referenceTransform.localPosition;
        }

        protected override float ConvertRawPpiToDpi(float rawPpi)
        {
            return referenceTransform.rect.height / totalHeightInInches;
        }

        protected override BodyPose? CloneAndProcessPoseIfNeeded(BodyPose? rawPose)
        {
            var pose = (BodyPose?)rawPose?.Clone();
            if (pose != null && RawPpi > 0)
            {
                NormalizePose(pose, RawPpi);
            }

            return pose;
        }

        #endregion

        #region Normalization

        void NormalizePose(BodyPose pose, float ppi)
        {
            // Add more normalization logic if needed.
            var chestPt = pose.Chest().ToVector2();
            var scale = 1 / (ppi * totalHeightInInches);
            ScalePose(pose, scale, chestPt);
            pose.InvalidatePpi();
        }

        void ScalePose(BodyPose pose, float scale, Vector2 pivot)
        {
            for (var i = 0; i < BodyPose.nodeNumber; i++)
            {
                var node = pose.nodes[i];
                var dx = node.x - pivot.x;
                var dy = node.y - pivot.y;
                node.x =  dx * scale + pivot.x;
                node.y = dy * scale + pivot.y;
                pose.nodes[i] = node;
            }
        }

        #endregion
    }
}
