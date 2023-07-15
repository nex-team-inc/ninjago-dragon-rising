using System.Collections.Generic;
using Jazz;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class PoseNodeSmoothingState
    {
        public Vector2 LastValidPos;
        public bool Started;
        public ComposedFilter2D<OneEuroFilter> Filter;

        public PoseNodeSmoothingState(Vector2 lastValidPos, bool started, ComposedFilter2D<OneEuroFilter> filter)
        {
            LastValidPos = lastValidPos;
            Started = started;
            Filter = filter;
        }
    }

    public class BodyPoseSmoothHelper : MonoBehaviour
    {
        [SerializeField] List<BodyPose.NodeIndex> poseNodeToSmooth = null!;

        readonly Dictionary<BodyPose.NodeIndex, PoseNodeSmoothingState> stateByNodeIndex = new();

        #region Public

        public void Initialize()
        {
            foreach (var nodeIndex in poseNodeToSmooth)
            {
                stateByNodeIndex[nodeIndex] = new PoseNodeSmoothingState
                (
                    Vector2.zero,
                    false,
                    new ComposedFilter2D<OneEuroFilter>(new OneEuroFilter(), new OneEuroFilter())
                );
            }
        }

        public PlayerPose? Smooth(PlayerPose? playerPose)
        {
            var retPlayerPose = (PlayerPose?)playerPose?.Clone();

            if (retPlayerPose?.bodyPose != null)
            {
                var retPose = retPlayerPose.bodyPose;
                foreach (var nodeIndex in poseNodeToSmooth)
                {
                    var state = stateByNodeIndex[nodeIndex];
                    var newNode = retPose.nodes[(int)nodeIndex];
                    Vector2 newPos;

                    if (newNode.isDetected)
                    {
                        newPos = state.Filter.Filter(newNode.x, newNode.y, Time.fixedTime);
                        state.Started = true;
                        state.LastValidPos = newPos;
                    }
                    else if (state.Started)
                    {
                        // No valid new node, but started. So we can use last valid pos to update filter.
                        newPos = state.Filter.Filter(state.LastValidPos.x, state.LastValidPos.y, Time.fixedTime);
                    }
                    else
                    {
                        // No started, no node is detected. Don't start. Just skip.
                        continue;
                    }

                    // Update node x & y.
                    // NOTE: we don't change isDetected here. Just to keep invalid node to be invalid.
                    newNode.x = (int)newPos.x;
                    newNode.y = (int)newPos.y;
                    retPose.nodes[(int)nodeIndex] = newNode;
                }
            }

            return retPlayerPose;
        }

        #endregion
    }
}
