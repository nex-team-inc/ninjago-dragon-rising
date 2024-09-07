using UnityEngine;
using UnityEngine.UI;

namespace Nex
{
    public abstract class PreviewFrameBase : MonoBehaviour
    {
        [SerializeField] protected RawImage rawImage = null!;
        [SerializeField] protected CanvasGroup canvasGroup = null!;

        protected Rect previewRectInWorldSpace;
        protected float previewRectInWorldSpaceAspectRatio;
        bool isPreviewRectInWorldSpaceInfoValid;

        public abstract Rect PreviewRectInNormalizedSpace();

        public Rect PreviewRectInWorldSpace()
        {
            return previewRectInWorldSpace;
        }

        protected void UpdatePreviewRectInWorldSpaceInfoIfNeeded(bool forceUpdate = false)
        {
            if (!isPreviewRectInWorldSpaceInfoValid || forceUpdate)
            {
                var corners = new Vector3[4];
                rawImage.GetComponent<RectTransform>().GetWorldCorners(corners);
                previewRectInWorldSpace = new Rect(corners[0], corners[2] - corners[0]);
                previewRectInWorldSpaceAspectRatio = previewRectInWorldSpace.height > 0 ? previewRectInWorldSpace.width / previewRectInWorldSpace.height : 0;
                isPreviewRectInWorldSpaceInfoValid = true;
            }
        }

        protected static Rect FlipRectIfNeeded(Rect rect, bool flip)
        {
            if (flip)
            {
                rect.x = 1 - rect.x;
                rect.width = -rect.width;
            }

            return rect;
        }
    }
}
