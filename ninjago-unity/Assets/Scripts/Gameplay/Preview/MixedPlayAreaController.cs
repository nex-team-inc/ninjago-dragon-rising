#nullable enable

using Jazz;
using NaughtyAttributes;
using UnityEngine;

namespace Nex.ImageFilter
{
    /// <summary>
    /// This is a wrapper class for switching between different PlayAreaControllers.
    /// This is meant for testing and should be finalized on which PlayAreaController to use.
    /// </summary>
    public class MixedPlayAreaController : BasePlayAreaController
    {
        [SerializeField] BasePlayAreaController controllerA = null!;
        [SerializeField] BasePlayAreaController controllerB = null!;

        BasePlayAreaController currentController = null!;

        public override void Initialize(int aNumOfPlayers, CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager)
        {
            base.Initialize(aNumOfPlayers, aCvDetectionManager, aBodyPoseDetectionManager);

            controllerA.Initialize(aNumOfPlayers, aCvDetectionManager, aBodyPoseDetectionManager);
            controllerB.Initialize(aNumOfPlayers, aCvDetectionManager, aBodyPoseDetectionManager);

            currentController = controllerA;
            Debug.Log($"Current PlayAreaController = {currentController.gameObject.name}");
        }

        public override Rect GetPlayAreaInNormalizedSpace()
        {
            return currentController.GetPlayAreaInNormalizedSpace();
        }

        public override Rect GetPlayAreaInAspectNormalizedSpace()
        {
            return currentController.GetPlayAreaInAspectNormalizedSpace();
        }

        public override void RefreshPlayArea()
        {
            currentController.RefreshPlayArea();
        }

        public override void ForceSetPlayArea(Rect rect)
        {
            currentController.ForceSetPlayArea(rect);
        }

        [Button]
        public void ToggleController()
        {
            currentController = currentController == controllerA ? controllerB : controllerA;
            Debug.Log($"Current PlayAreaController = {currentController.gameObject.name}");
        }
    }
}
