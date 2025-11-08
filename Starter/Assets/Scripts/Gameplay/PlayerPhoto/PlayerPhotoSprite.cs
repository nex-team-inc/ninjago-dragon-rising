#nullable enable

using UnityEngine;

namespace Nex
{
    public class PlayerPhotoSprite : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteRenderer = null!;
        OnePlayerPhotoTracker playerPhotoTracker = null!;

        Sprite? sprite;

        public void Initialize(
            OnePlayerPhotoTracker aPlayerPhotoTracker
            )
        {
            playerPhotoTracker = aPlayerPhotoTracker;
            playerPhotoTracker.PhotoUpdated += PlayerPhotoTrackerOnPhotoUpdated;
        }

        void OnDestroy()
        {
            playerPhotoTracker.PhotoUpdated -= PlayerPhotoTrackerOnPhotoUpdated;
        }

        void PlayerPhotoTrackerOnPhotoUpdated(OnePlayerPhotoTracker tracker)
        {
            SetPlayerPhotoData(tracker.GetPlayerPhotoData());
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
    }
}
