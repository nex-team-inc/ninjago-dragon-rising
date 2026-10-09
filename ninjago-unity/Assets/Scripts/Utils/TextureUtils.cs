#nullable enable

using UnityEngine;

namespace Nex
{
    public static class TextureUtils
    {
        public static Rect ComputeTextureRect(Texture2D texture2D, Rect normalizedRect)
        {
            var x = normalizedRect.x * texture2D.width;
            var y = normalizedRect.y * texture2D.height;

            return new Rect(
                x,
                y,
                Mathf.Min(normalizedRect.width * texture2D.width, texture2D.width - x),
                Mathf.Min(normalizedRect.height * texture2D.height, texture2D.height - y)
            );
        }
    }
}
