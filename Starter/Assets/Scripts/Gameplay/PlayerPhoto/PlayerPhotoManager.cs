#nullable enable

using System.Collections.Generic;
using Jazz;
using UnityEngine;

namespace Nex
{
    public class PlayerPhotoManager : MonoBehaviour, IPreviewTextureHandler
    {
        int numOfPlayers;
        CvDetectionManager cvDetectionManager = null!;
        readonly List<OnePlayerPhotoTracker> playerPhotoTrackers = new();

        #region Public

        public Rect GetPreviewRegion()
        {
            return new Rect();
        }

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
            CvDetectionManager.previewController.AddPreviewTextureHandler(this);
        }

        void OnDestroy()
        {
            foreach (var tracker in playerPhotoTrackers)
            {
                tracker.CleanUp();
            }
            playerPhotoTrackers.Clear();

            CvDetectionManager.previewController.RemovePreviewTextureHandler(this);
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
