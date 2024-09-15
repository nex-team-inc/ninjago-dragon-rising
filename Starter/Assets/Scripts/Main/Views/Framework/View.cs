using System;
using Cysharp.Threading.Tasks;
using Nex.KeyboardNavigation;
using UnityEngine;

namespace Nex
{
    // This is the base class for all "View".
    // A view dictates how to interact with the top level controls (such as back and what not).
    // It tells us what kind of controls are needed, and also handles them when they are clicked.
    // A view also handles the animation when they are presented or dismissed.
    // However, there are actually two ways they can be presented / dismissed.
    // The first way is the simple Present() and Dismiss(), called when the View is first introduced
    // and popped (say after Back). The second way is EnterBackground and EnterForeground, which are
    // called when something new is presented on top of it, or when something on top of it is presented.
    // In case of the latter, there is a ViewIdentifier of the child view.
    public abstract class View : MonoBehaviour
    {
        /// <summary>
        /// The ViewIdentifier is an enum to indicate what kind of view it is.
        /// This is mostly used for customization during background / foreground traversal.
        /// </summary>
        public enum ViewIdentifier
        {
            Invalid = -1, // This is just to mark some view states as being invalid.

            Empty,  // This is the empty view which has nothing. This is mostly just an anchor.

            // Main UI
            WelcomeScreen,

            // Debug Specific.
            DebugSettings,
        }

        /// <summary>
        /// The identifier for this view, so that we know how to signal the previous top view to enter background.
        /// </summary>
        public abstract ViewIdentifier Identifier { get; }

        public virtual string AnalyticsScreenName => "";

        protected virtual void Awake()
        {
        }

        #region View Management

        /// <summary>
        /// The manager managing this view, which is responsible for pushing and popping further views.
        /// </summary>
        internal ViewManager Manager { get; set; }

        internal bool IsActive { get; set; }

        internal virtual Camera ViewCamera { get; set; }

        // This is made virtual so that simple canvases can use this view order to set their sort order.
        /// <summary>
        /// The view order is basically the sorting order of the canvas of the view.
        /// Since there is always an empty root view within the ViewManager, the view order effectively starts
        /// at 1. It can be further offset by setting the rootViewOrder in the ViewManager.
        /// </summary>
        internal virtual int ViewOrder { get; set; }

        const float maximumPlaneDistance = 300;
        const float planeDistanceDelta = 10;

        protected static float GetPlaneDistance(int viewOrder)
        {
            return maximumPlaneDistance - planeDistanceDelta * viewOrder;
        }

        protected UniTask PushViewPrefab(View view, bool animate = true)
        {
            return Manager.PushViewPrefab(view, animate);
        }

        protected UniTask PushView(View view, bool animate = true)
        {
            return Manager.PushView(view, animate);
        }

        protected UniTask PopSelf(bool animate = true, bool keepAlive = false)
        {
            return Manager.PopView(animate, keepAlive);
        }

        #endregion

        #region Life Cycle

        /// <summary>
        /// Presents this view (for the first time).
        /// </summary>
        /// <param name="animate">true if this is presented with animation.</param>
        /// <returns>A UniTask to indicate when the presentation is completed.</returns>
        public abstract UniTask Present(bool animate = true);

        public async UniTask PresentWithDuration(float duration, bool animate = true)
        {
            if (animate && duration > PresentDuration)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration - PresentDuration), DelayType.UnscaledDeltaTime);
            }

            await Present(animate);
        }

        /// <summary>
        /// Dismisses this view (for the last time).
        /// </summary>
        /// <param name="animate">true if this is dismissed with animation.</param>
        /// <returns>A UniTask to indicate when the dismissal is done.</returns>
        public abstract UniTask Dismiss(bool animate = true);

        /// <summary>
        /// Enters the background, because new child is being pushed to the stack.
        /// </summary>
        /// <param name="childViewIdentifier">The identifier of the child view, to customize the background behaviour.</param>
        /// <param name="animate">true if this is hidden with animation.</param>
        /// <returns>A UniTask to indicate when the transition is completed.</returns>
        public virtual UniTask EnterBackground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Enters the foreground, because the child views are being popped from the stack.
        /// </summary>
        /// <param name="childViewIdentifier">The identifier of the last popped child view. This should corresponds to the same childViewIdentifier passed in EnterBackground.</param>
        /// <param name="animate">true if this is shown with animation.</param>
        /// <returns>A UniTask to indicate when the transition is completed.</returns>
        public virtual UniTask EnterForeground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            return UniTask.CompletedTask;
        }

        public async UniTask EnterForegroundWithDuration(
            float duration, ViewIdentifier childViewIdentifier, bool animate = true)
        {
            if (animate && duration > ForegroundDuration)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration - ForegroundDuration), DelayType.UnscaledDeltaTime);
            }
            await EnterForeground(childViewIdentifier, animate);
        }

        /// <summary>
        /// Blur self, because new child is being pushed to the stack.
        /// </summary>
        /// <param name="childViewIdentifier">The identifier of the child view, to customize the background behaviour.</param>
        /// <param name="animate">true if this is hidden with animation.</param>
        /// <returns>A UniTask to indicate when the transition is completed.</returns>
        public virtual UniTask PerformBlur(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Unblur self, because the child views are being popped from the stack.
        /// </summary>
        /// <param name="childViewIdentifier">The identifier of the last popped child view. This should corresponds to the same childViewIdentifier passed in EnterBackground.</param>
        /// <param name="animate">true if this is shown with animation.</param>
        /// <returns>A UniTask to indicate when the transition is completed.</returns>
        public virtual UniTask PerformUnblur(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            return UniTask.CompletedTask;
        }

        public async UniTask PerformUnblurWithDuration(
            float duration, ViewIdentifier childViewIdentifier, bool animate = true)
        {
            if (animate && duration > UnblurDuration)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration - UnblurDuration), DelayType.UnscaledDeltaTime);
            }
            await PerformUnblur(childViewIdentifier, animate);
        }

        public virtual float PresentDuration => 0.5f;
        public virtual float DismissDuration => 0.5f;
        public virtual float BackgroundDuration => 0.5f;
        public virtual float ForegroundDuration => 0.5f;

        public virtual float BlurDuration => 0f;
        public virtual float UnblurDuration => 0f;

        /// <summary>
        /// Callback when the view has just become the top view / activated.
        /// </summary>
        /// <param name="afterPush">Whether the view is being activated because it is first pushed. false if it is just becoming foreground again.</param>
        public virtual void ViewDidBecomeTopView(bool afterPush)
        {
            Debug.Log($"[Become top view] {name}");
        }

        /// <summary>
        /// Callback when the view is no longer the top.
        /// </summary>
        /// <param name="dismissing">Whether the view is being dismissed. false if it is going to background.</param>
        public virtual void ViewDidLoseTopView(bool dismissing)
        {
            Debug.Log($"[Lose top view] {name}");
        }

        #endregion

        #region Controls and Configurations

        /// <summary>
        /// The controls that we want the top level control panel to provide.
        /// </summary>
        public abstract TopLevelControlPanel.ControlConfig Controls { get; }

        public virtual bool RequiresAdditionalBackgroundBlur => false;

        #endregion

        #region Event Handling

        public virtual void OnBackButton()
        {
        }

        public virtual bool OnControlButton(TopLevelControlPanel.ButtonKind buttonKind)
        {
            return false;
        }

        #endregion

        #region KeyboardNavigation

        // Returns the key responder of this view, if it has one.
        public virtual KeyResponder KeyResponder => default;

        #endregion
    }
}
