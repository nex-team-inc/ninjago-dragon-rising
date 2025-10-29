#nullable enable

using UnityEngine;

namespace Nex
{
    public interface PlayerAttachmentDataSource
    {
        public float DistancePerInch { get; }
        public Vector3? GetNodePosition(PoseNodeIndex nodeIndex, bool smoothed);
    }
}
