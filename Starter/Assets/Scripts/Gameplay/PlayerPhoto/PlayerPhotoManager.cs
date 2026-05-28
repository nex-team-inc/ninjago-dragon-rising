#nullable enable

using System.Collections.Generic;
using Jazz;
using UnityEngine;

namespace Nex
{
    public class PlayerPhotoManager : MonoBehaviour
    {
        int numOfPlayers;
        readonly List<OnePlayerPhotoTracker> playerPhotoTrackers = new();

        #region Public

        public void Initialize(
            int aNumOfPlayers,
            BodyPoseDetectionManager bodyPoseDetectionManager
            )
        {
            numOfPlayers = aNumOfPlayers;

            for (var i = 0; i < numOfPlayers; i++)
            {
                var tracker = new GameObject();
                tracker.transform.SetParent(transform);
                var onePlayerPhotoTracker = tracker.AddComponent<OnePlayerPhotoTracker>();
                onePlayerPhotoTracker.Initialize(i, bodyPoseDetectionManager);
                playerPhotoTrackers.Add(onePlayerPhotoTracker);
            }


            foreach (var tracker in playerPhotoTrackers)
            {
                CvDetectionManager.previewController.AddPreviewTextureHandler(tracker);
            }
        }

        void OnDestroy()
        {
            foreach (var tracker in playerPhotoTrackers)
            {
                CvDetectionManager.previewController.RemovePreviewTextureHandler(tracker);
                tracker.CleanUp();
            }
            playerPhotoTrackers.Clear();
        }

        public OnePlayerPhotoTracker GetTrackerByPlayerIndex(int playerIndex)
        {
            return playerPhotoTrackers[playerIndex];
        }

        public void TakePhoto(int playerIndex)
        {
            playerPhotoTrackers[playerIndex].TakePhoto(0);
        }

        public void ClearPhoto(int playerIndex)
        {
            playerPhotoTrackers[playerIndex].ClearPhoto();
        }

        #endregion

        #region Raw Input

        public void OnTextureUpdated(Texture2D newTexture, Rect newUV)
        {
            foreach (var tracker in playerPhotoTrackers)
            {
                tracker.SetPreviewImageTexture(newTexture);
            }
        }

        #endregion
    }
}
