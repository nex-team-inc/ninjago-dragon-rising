#nullable enable

using Jazz;
using UnityEngine;

namespace Nex
{
    public class PlayerPhotoSprite : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteRenderer = null!;

        [SerializeField]
        OnePlayerPhotoTracker.TrackedBodyNode trackedBodyNode =
            new()
            {
                trackedNodeIndex = BodyPose.NodeIndex.Nose,
                nodeRotation = new OnePlayerPhotoTracker.NodeRotation(BodyPose.NodeIndex.Nose, BodyPose.NodeIndex.Nose),
                zoomInFactor = 0.8f,
                topMarginInInches = 3.9f,
                bottomMarginInInches = 3.5f,
                leftMarginInInches = 3.7f,
                rightMarginInInches = 3.7f,
            };

        OnePlayerPhotoTracker playerPhotoTracker = null!;

        Sprite? sprite;
        int trackedBodyNodeIndex;

        public void Initialize(
            OnePlayerPhotoTracker aPlayerPhotoTracker
            )
        {
            playerPhotoTracker = aPlayerPhotoTracker;
            trackedBodyNodeIndex = playerPhotoTracker.AddTrackedBodyNode(trackedBodyNode);
            playerPhotoTracker.PhotoUpdated += PlayerPhotoTrackerOnPhotoUpdated;
        }

        void OnDestroy()
        {
            playerPhotoTracker.PhotoUpdated -= PlayerPhotoTrackerOnPhotoUpdated;
        }

        void PlayerPhotoTrackerOnPhotoUpdated(OnePlayerPhotoTracker tracker)
        {
            SetPlayerPhotoData(tracker.GetPlayerPhotoData(trackedBodyNodeIndex));
        }

        void SetPlayerPhotoData(PlayerPhotoData playerPhotoData)
        {
            var texture = playerPhotoData.texture;

            if (texture != null && playerPhotoData.uvRect != Rect.zero)
            {
                if (sprite != null)
                {
                    Destroy(sprite);
                }

                var rect = TextureUtils.ComputeTextureRect(texture, playerPhotoData.uvRect);
                sprite = Sprite.Create(
                    playerPhotoData.texture,
                    rect,
                    new Vector2(0.5f, 0.5f),
                    rect.height
                );
                spriteRenderer.sprite = sprite;
            }
        }

        public void TakePhoto()
        {
            playerPhotoTracker.TakePhoto(trackedBodyNodeIndex);
            if (sprite != null)
            {
                var clonedSprite = Sprite.Create(
                    sprite.texture,
                    sprite.rect,
                    new Vector2(0.5f, 0.5f),
                    sprite.pixelsPerUnit
                );
                spriteRenderer.sprite = clonedSprite;
            }
        }

        public void ClearPhoto()
        {
            playerPhotoTracker.ClearPhoto();
        }
    }
}
