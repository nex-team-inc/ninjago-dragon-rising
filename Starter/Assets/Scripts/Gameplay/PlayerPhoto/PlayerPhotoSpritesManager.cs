#nullable enable

using UnityEngine;

namespace Nex
{
    public class PlayerPhotoSpritesManager : MonoBehaviour
    {
        #region Initialization

        public void Initialize(
            int playerIndex,
            PlayerPhotoManager playerPhotoManager
            )
        {
            var tracker = playerPhotoManager.GetTrackerByPlayerIndex(playerIndex);

            var photoSprites = gameObject.GetComponentsInChildren<PlayerPhotoSprite>(true);
            foreach (var photoSprite in photoSprites)
            {
                photoSprite.Initialize(tracker);
            }
        }

        #endregion
    }
}
