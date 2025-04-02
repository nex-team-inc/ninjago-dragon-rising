#nullable enable

using Jazz;
using UnityEngine;

namespace Nex
{
    public abstract class BasePlayAreaController : MonoBehaviour
    {
        protected CvDetectionManager cvDetectionManager = null!;
        protected BodyPoseDetectionManager bodyPoseDetectionManager = null!;

        // Configs
        protected int numOfPlayers;
        protected bool locked;

        public virtual void Initialize(
            int aNumOfPlayers,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager
        )
        {
            numOfPlayers = aNumOfPlayers;
            cvDetectionManager = aCvDetectionManager;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
        }

        public virtual void SetPlayAreaLocked(bool value)
        {
            locked = value;
        }

        public abstract Rect GetPlayAreaInNormalizedSpace();

        public abstract Rect GetPlayAreaInAspectNormalizedSpace();

        public abstract void RefreshPlayArea();

        public abstract void ForceSetPlayArea(Rect rect);

        protected virtual void UpdateTrackingPosition()
        {
            // XXX: this is a hack. MDK doesn't have a API to set the player tracking position. So we need to hack it
            // by setting the element in the list during runtime.
            while(cvDetectionManager.playerPositions.Count < numOfPlayers)
            {
                cvDetectionManager.playerPositions.Add(Vector2.zero);
            }

            for (var playerIndex = 0; playerIndex < numOfPlayers; playerIndex++)
            {
                var ratioInPlayerArea = PlayerPositionDefinition.GetXRatioForPlayer(playerIndex, numOfPlayers);
                var playAreaInNormalizedSpace = GetPlayAreaInNormalizedSpace();
                var ratioInRawFrame = playAreaInNormalizedSpace.x + playAreaInNormalizedSpace.width * ratioInPlayerArea;
                cvDetectionManager.playerPositions[playerIndex] = new Vector2(ratioInRawFrame, 0.5f);
            }
        }
    }
}
