using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex
{
    [RequireComponent(typeof(Canvas))]
    public abstract class SimpleCanvasView: SimpleView
    {
        protected Canvas canvas;
        [SerializeField] protected bool disableCanvasOnBackground;

        protected override void Awake()
        {
            base.Awake();
            canvas = GetComponent<Canvas>();
        }

        internal override Camera ViewCamera
        {
            set
            {
                base.ViewCamera = value;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = value;
            }
        }

        internal override int ViewOrder
        {
            set
            {
                base.ViewOrder = value;
                if (ViewCamera == null)
                {
                    canvas.sortingOrder = value;
                }
                else
                {
                    canvas.planeDistance = GetPlaneDistance(value);
                }
            }
        }

        public override async UniTask EnterBackground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            await base.EnterBackground(childViewIdentifier, animate);
            if (disableCanvasOnBackground)
            {
                canvas.enabled = false;
            }
        }

        public override async UniTask EnterForeground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            if (disableCanvasOnBackground)
            {
                canvas.enabled = true;
            }
            await base.EnterForeground(childViewIdentifier, animate);
        }
    }
}
