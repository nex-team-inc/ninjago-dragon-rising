#nullable enable

using System.Collections.Generic;
using Jazz;
using UnityEngine;

namespace Nex
{
    public class PlayerPhotoManager : MonoBehaviour
    {
        int numOfPlayers;
        CvDetectionManager cvDetectionManager = null!;
        readonly List<OnePlayerPhotoTracker> playerPhotoTrackers = new();

        #region Public

        public void Initialize(
            int aNumOfPlayers,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager bodyPoseDetectionManager
            )
        {
            numOfPlayers = aNumOfPlayers;

            for (var i = 0; i < numOfPlayers; i++)
            {
                playerPhotoTrackers.Add(new OnePlayerPhotoTracker(i, bodyPoseDetectionManager));
            }

            cvDetectionManager = aCvDetectionManager;
            cvDetectionManager.captureCameraFrame += CvDetectionManagerOnCaptureCameraFrame;
        }

        void OnDestroy()
        {
            foreach (var tracker in playerPhotoTrackers)
            {
                tracker.CleanUp();
            }
            playerPhotoTrackers.Clear();

            cvDetectionManager.captureCameraFrame -= CvDetectionManagerOnCaptureCameraFrame;
        }

        public OnePlayerPhotoTracker GetTrackerByPlayerIndex(int playerIndex)
        {
            return playerPhotoTrackers[playerIndex];
        }

        public void TakePhoto(int playerIndex)
        {
            playerPhotoTrackers[playerIndex].TakePhoto();
        }

        public void ClearPhoto(int playerIndex)
        {
            playerPhotoTrackers[playerIndex].ClearPhoto();
        }

        #endregion

        #region Raw Input

        void CvDetectionManagerOnCaptureCameraFrame(FrameInformation frameInformation)
        {
            foreach (var tracker in playerPhotoTrackers)
            {
                tracker.SetPreviewImageTexture((Texture2D?)frameInformation.texture);
            }
        }

        #endregion
    }
}
