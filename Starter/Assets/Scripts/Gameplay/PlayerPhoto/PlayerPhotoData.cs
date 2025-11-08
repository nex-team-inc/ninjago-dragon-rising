#nullable enable

using UnityEngine;

namespace Nex
{
    public struct PlayerPhotoData
    {
        public Texture2D? texture;
        public Rect uvRect;

        public PlayerPhotoData(
            Texture2D? texture,
            Rect uvRect
            )
        {
            this.texture = texture;
            this.uvRect = uvRect;
        }
    }
}
