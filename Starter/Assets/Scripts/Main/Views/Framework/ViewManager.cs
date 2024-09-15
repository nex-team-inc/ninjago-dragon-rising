using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.KeyboardNavigation;
using UnityEngine;
using UnityEngine.Events;

#nullable enable

namespace Nex
{
    public class ViewManager : MonoBehaviour
    {
        // All camera should by default be deactivated.
        [SerializeField] CameraChainItem[] cameraChains = null!;

        CancellationTokenSource cancellationTokenSource = null!;
        CancellationToken cancellationToken;

        static int disableKeyboardNavigationCount;

        readonly struct ViewConfig
        {
            public readonly View view;
            public readonly int hierarchyLevel;

            public ViewConfig(View view, int hierarchyLevel)
            {
                this.view = view;
                this.hierarchyLevel = hierarchyLevel;
            }
        }

        readonly Stack<ViewConfig> viewStack = new();

        [Tooltip("The sorting order of the empty root view")]
        [SerializeField] int rootViewOrder;
        [SerializeField] TopLevelControlPanel controlPanel = null!;

        [SerializeField] DebugSettingsView debugSettingsPrefab = null!;

        const float blurDuration = 0.5f;

        public event UnityAction? PauseViewHomeClicked;
        public event UnityAction? PauseViewResumeClicked;
        public event UnityAction? Paused;

        void Awake()
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;

            // The control panel will be on overlay, with no view camera.
            controlPanel.OnButton += HandleTopLevelButton;

            SetUpKeyboardNavigation();

            var emptyRootView = gameObject.AddComponent<EmptyView>();
            // We are assuming that the first camera chain item is already activated.

            // We will now initialize the view stack so that it is always non-empty.
            viewStack.Push(new ViewConfig(emptyRootView, 0));

            #if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            if (gameObject.TryGetComponent<SecretCodeSequenceDetector>(out var detector))
            {
                detector.AddListener(0, OpenDebugSettings);
            }
            #endif
        }

        void OnDestroy()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        static TopLevelControlPanel.ControlConfig ComputeControlConfig(View view)
        {
            var viewConfig = view.Controls;
            #if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            if (view.Identifier != View.ViewIdentifier.DebugSettings)
            {
                viewConfig |= TopLevelControlPanel.ControlConfig.DebugSettings;
            }
            #endif
            return viewConfig;
        }

        public View TopView => viewStack.Peek().view;
        public View.ViewIdentifier TopViewIdentifier => TopView.Identifier;
        public TopLevelControlPanel ControlPanel => controlPanel;

        bool lastViewChangeWasPush;

        bool isInTransition;
        public bool IsInTransition => isInTransition;

        /// <summary>
        /// Pushes a new view to the stack.
        /// If there is a new BackgroundAudioSource, the current one will also be faded away.
        /// This is basically the same as instantiating the prefab and calling push view directly.
        /// </summary>
        /// <param name="viewPrefab">The new view (prefab) to be pushed to the top.</param>
        /// <param name="animate">Whether we should animate the transition or not.</param>
        /// <exception cref="InvalidOperationException">Thrown when there are other views pushing / popping at the same time.</exception>
        public async UniTask<T> PushViewPrefab<T>(T viewPrefab, bool animate = true) where T : View
        {
            if (isInTransition) throw new InvalidOperationException("Cannot push / pop view while in transition.");
            var newView = Instantiate(viewPrefab);
            await PushView(newView, animate);
            return newView;
        }

        /// <summary>
        /// Pushes a new view to the stack.
        /// If there is a new BackgroundAudioSource, the current one will also be faded away.
        /// </summary>
        /// <param name="view">The new view to be pushed to the top.</param>
        /// <param name="animate">Whether we should animate the transition or not.</param>
        /// <exception cref="InvalidOperationException">Thrown when there are other views pushing / popping at the same time.</exception>
        public async UniTask PushView(View view, bool animate = true)
        {
            if (isInTransition) throw new InvalidOperationException("Cannot push / pop view while in transition.");
            view.Manager = this;
            controlPanel.Interactable = false;
            isInTransition = true;
            var currViewConfig = viewStack.Peek();
            var oldView = currViewConfig.view;
            DeactivateView(oldView, false);

            var prevHierarchyLevel = viewStack.Peek().hierarchyLevel;
            var currHierarchyLevel = view.RequiresAdditionalBackgroundBlur && prevHierarchyLevel + 1 < cameraChains.Length
                ? prevHierarchyLevel + 1
                : prevHierarchyLevel;

            view.ViewCamera = cameraChains[currHierarchyLevel].GetCamera();
            view.ViewOrder = rootViewOrder + viewStack.Count;
            // Update the view stack after the view order is set.
            viewStack.Push(new ViewConfig(view, currHierarchyLevel));

            var duration = animate ? view.PresentDuration : 0;
            UniTask backgroundTask;
            if (currHierarchyLevel > prevHierarchyLevel)
            {
                // We are going to activate blurring. There is no background animation.
                if (animate)
                    duration = Mathf.Max(duration, Math.Max(blurDuration, oldView.BlurDuration));
                backgroundTask =
                    UniTask.WhenAll(
                        cameraChains[currHierarchyLevel]
                            .Activate(cameraChains[prevHierarchyLevel], duration, cancellationToken),
                        oldView.PerformBlur(view.Identifier, animate));
            }
            else
            {
                if (animate) duration = Mathf.Max(duration, oldView.BackgroundDuration);
                backgroundTask = oldView.EnterBackground(view.Identifier, animate);
            }

            var newControls = ComputeControlConfig(view);
            await UniTask.WhenAll(backgroundTask, view.PresentWithDuration(duration, animate),
                controlPanel.ConfigureControls(newControls, duration, animate));
            isInTransition = false;
            controlPanel.Interactable = true;
            if (transactionCount == 0)
            {
                ActivateView(view, true);
            }
            else
            {
                lastViewChangeWasPush = true;
            }
        }

        /// <summary>
        /// Pops the current top view from the stack.
        /// </summary>
        /// <param name="animate">Whether we should animate the transition or not.</param>
        /// <param name="keepPoppedViewAlive">Whether to keep the popped view alive.</param>
        /// <exception cref="InvalidOperationException">Thrown when there are other views pushing / popping at the same time.</exception>
        public async UniTask PopView(bool animate = true, bool keepPoppedViewAlive = false)
        {
            if (isInTransition) throw new InvalidOperationException("Cannot push / pop view while in transition.");
            controlPanel.Interactable = false;
            isInTransition = true;
            var prevTopViewConfig = viewStack.Pop();
            var prevTopView = prevTopViewConfig.view;
            DeactivateView(prevTopView, true);
            var currTopViewConfig = viewStack.Peek();
            var currTopView = currTopViewConfig.view;
            var newControls = ComputeControlConfig(currTopView);

            // Deactivate the last camera chain item, if needed.
            var duration = animate ? prevTopView.DismissDuration : 0;
            UniTask foregroundTask;
            if (prevTopViewConfig.hierarchyLevel > currTopViewConfig.hierarchyLevel)
            {
                // The previous view didn't go background, so there is no foreground.
                // The foregroundTask is just deactivating camera.
                if (animate)
                    duration = Mathf.Max(duration,
                        Mathf.Max(blurDuration, currTopView.UnblurDuration));
                foregroundTask =
                    UniTask.WhenAll(
                        cameraChains[prevTopViewConfig.hierarchyLevel].Deactivate(duration, cancellationToken),
                        currTopView.PerformUnblurWithDuration(duration, prevTopView.Identifier, animate));
            }
            else
            {
                if (animate) duration = Mathf.Max(duration, currTopView.ForegroundDuration);
                foregroundTask = currTopView.EnterForegroundWithDuration(duration, prevTopView.Identifier, animate);
            }

            await UniTask.WhenAll(prevTopView.Dismiss(animate), foregroundTask,
                controlPanel.ConfigureControls(newControls, duration, animate));
            isInTransition = false;
            controlPanel.Interactable = true;
            if (transactionCount == 0)
            {
                ActivateView(currTopView, false);
            }
            else
            {
                lastViewChangeWasPush = false;
            }
            if (!keepPoppedViewAlive) Destroy(prevTopView.gameObject);
        }

        public async UniTask<View> ReplaceView(View replacementView, bool animate = true, bool keepPoppedViewAlive = false)
        {
            if (isInTransition) throw new InvalidOperationException("Cannot push / pop view while in transition.");
            controlPanel.Interactable = false;
            isInTransition = true;
            var prevTopViewConfig = viewStack.Pop();
            var prevTopView = prevTopViewConfig.view;
            DeactivateView(prevTopView, true);

            replacementView.Manager = this;

            var replacementHierarchyLevel = Mathf.Min(cameraChains.Length - 1,
                (replacementView.RequiresAdditionalBackgroundBlur ? 1 : 0) + viewStack.Peek().hierarchyLevel);
            replacementView.ViewCamera = cameraChains[replacementHierarchyLevel].GetCamera();
            replacementView.ViewOrder = rootViewOrder + viewStack.Count;

            var duration = animate ? Mathf.Max(prevTopView.DismissDuration, replacementView.PresentDuration) : 0;

            UniTask transitionTask;
            if (replacementHierarchyLevel == prevTopViewConfig.hierarchyLevel)
            {
                // The simple case. Nothing has changed.
                transitionTask = UniTask.CompletedTask;
            }
            else if (replacementHierarchyLevel > prevTopViewConfig.hierarchyLevel)
            {
                // That means we are going to add blurring.
                // Moreover, the parent view should be "foreground" again.
                var parentView = viewStack.Peek().view;
                if (animate)
                {
                    duration = Mathf.Max(duration,
                        Mathf.Max(parentView.ForegroundDuration, blurDuration));
                }
                transitionTask = UniTask.WhenAll(
                    cameraChains[replacementHierarchyLevel]
                        .Activate(cameraChains[replacementHierarchyLevel - 1], duration, cancellationToken),
                    parentView.EnterForegroundWithDuration(duration, replacementView.Identifier, animate)
                );
            }
            else
            {
                // replacementHierarchyLevel < prevTopViewConfig.hierarchyLevel.
                var parentView = viewStack.Peek().view;
                if (animate)
                {
                    duration = Mathf.Max(duration,
                        Mathf.Max(parentView.BackgroundDuration, blurDuration));
                }

                transitionTask = UniTask.WhenAll(
                    cameraChains[prevTopViewConfig.hierarchyLevel].Deactivate(duration, cancellationToken),
                    parentView.EnterBackground(replacementView.Identifier, animate));
            }
            viewStack.Push(new ViewConfig(replacementView, replacementHierarchyLevel));

            var newControls = ComputeControlConfig(replacementView);
            await UniTask.WhenAll(
                prevTopView.Dismiss(animate),
                replacementView.PresentWithDuration(duration, animate),
                controlPanel.ConfigureControls(newControls, duration, animate),
                transitionTask
            );
            isInTransition = false;
            controlPanel.Interactable = true;
            if (transactionCount == 0)
            {
                ActivateView(replacementView, false);
            }
            else
            {
                lastViewChangeWasPush = true;
            }
            if (!keepPoppedViewAlive) Destroy(prevTopView.gameObject);
            return prevTopView;
        }

        void ActivateTopView()
        {
            if (viewStack.Count == 0) return;
            ActivateView(viewStack.Peek().view, lastViewChangeWasPush);
        }

        void ActivateView(View view, bool afterPush)
        {
            view.IsActive = true;

            var keyResponder = view.KeyResponder;
            if (keyResponder != null)
            {
                keyResponder.EnableHighlighting = PlayerDataManager.Instance.appViewState.enableHighlighting;
                keyResponder.Activate(keyboardNavigationContext, true);
                activeKeyResponder = keyResponder;
            }

            var screenName = view.AnalyticsScreenName;
            if (!string.IsNullOrEmpty(screenName))
            {
                AnalyticsManager.Instance.TrackScreen(screenName);
            }

            view.ViewDidBecomeTopView(afterPush);
        }

        void DeactivateView(View view, bool dismissing)
        {
            view.ViewDidLoseTopView(dismissing);

            var keyResponder = view.KeyResponder;
            if (keyResponder != null && keyResponder == activeKeyResponder)
            {
                keyResponder.Deactivate(keyboardNavigationContext);
                activeKeyResponder = null;
            }

            view.IsActive = false;

            if (dismissing)
            {
                view.Manager = null;
            }
        }

        #region Transaction Support

        public Transaction CreateTransaction() => new(this);

        int transactionCount;

        void IncrementTransactionCount()
        {
            ++transactionCount;
        }

        void DecrementTransactionCount()
        {
            if (--transactionCount == 0)
            {
                ActivateTopView();
            }
        }

        public class Transaction : IDisposable
        {
            readonly ViewManager host;

            public Transaction(ViewManager host)
            {
                this.host = host;
                host.IncrementTransactionCount();
            }

            public void Dispose()
            {
                host.DecrementTransactionCount();
            }
        }

        #endregion

        #region Event Handling

        void HandleTopLevelButton(TopLevelControlPanel.ButtonKind buttonKind)
        {
            if (isInTransition) return;
            switch (buttonKind)
            {
                // Centralized handling for settings / debug settings.
                case TopLevelControlPanel.ButtonKind.DebugSettings:
                    OpenDebugSettings();
                    break;

                // Special handling for back.
                case TopLevelControlPanel.ButtonKind.Back:
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.GenericExit);
                    if (viewStack.Count > 0)
                    {
                        viewStack.Peek().view.OnBackButton();
                    }
                    break;

                // General buttons.
                case TopLevelControlPanel.ButtonKind.Exit:
                case TopLevelControlPanel.ButtonKind.Help:
                    if (viewStack.Count > 0)
                    {
                        viewStack.Peek().view.OnControlButton(buttonKind);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttonKind), buttonKind, null);
            }
        }

        void OpenDebugSettings()
        {
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.GenericEnter);
            PushViewPrefab(debugSettingsPrefab).Forget();
        }

        #endregion

        #region Keyboard Navigation

        public static void SuspendKeyboardNavigation()
        {
            ++disableKeyboardNavigationCount;
        }

        public static void ResumeKeyboardNavigation()
        {
            --disableKeyboardNavigationCount;
        }

        KeyboardNavigationController keyboardNavigationController = null!;
        ViewManagerKeyboardNavigationContext keyboardNavigationContext = null!;
        KeyResponder? activeKeyResponder;

        void SetUpKeyboardNavigation()
        {
            keyboardNavigationContext = new ViewManagerKeyboardNavigationContext(this);
            keyboardNavigationController = gameObject.AddComponent<KeyboardNavigationController>();
            keyboardNavigationController.OnKey += HandleKey;
        }

        void EnableHighlightingIfNeeded()
        {
            var appViewState = PlayerDataManager.Instance.appViewState;
            if (appViewState.enableHighlighting) return;
            appViewState.enableHighlighting = true;
            if (activeKeyResponder != null) activeKeyResponder.EnableHighlighting = true;
        }

        void DisableHighlightingIfNeeded()
        {
            var appViewState = PlayerDataManager.Instance.appViewState;
            if (!appViewState.enableHighlighting) return;
            appViewState.enableHighlighting = false;
            if (activeKeyResponder != null) activeKeyResponder.EnableHighlighting = false;
        }

        void Update()
        {
            // This is just for debugging highlight state.
            if (!Input.GetKeyDown(KeyCode.Period)) return;
            if (PlayerDataManager.Instance.appViewState.enableHighlighting)
            {
                DisableHighlightingIfNeeded();
            }
            else
            {
                EnableHighlightingIfNeeded();
            }
        }

        void HandleKey(KeyboardNavigationController.Key key)
        {
            // Skip events while views are transitioning.
            if (isInTransition || activeKeyResponder == null) return;
            if (CherryIntegrationManager.Instance.IsKeyboardControlDisabled()) return;
            if (disableKeyboardNavigationCount > 0) return;
            switch (key)
            {
                case KeyboardNavigationController.Key.Escape:
                    activeKeyResponder.HandleBack();
                    break;
                case KeyboardNavigationController.Key.Enter:
                    EnableHighlightingIfNeeded();
                    activeKeyResponder.HandleEnter();
                    break;
                // Here, we ignore the result from the handle navigation call, because
                // there is really no further navigation on top-level.
                case KeyboardNavigationController.Key.Up:
                    EnableHighlightingIfNeeded();
                    activeKeyResponder.HandleNavigation(KeyResponder.NavigationKey.Up);
                    break;
                case KeyboardNavigationController.Key.Down:
                    EnableHighlightingIfNeeded();
                    activeKeyResponder.HandleNavigation(KeyResponder.NavigationKey.Down);
                    break;
                case KeyboardNavigationController.Key.Left:
                    EnableHighlightingIfNeeded();
                    activeKeyResponder.HandleNavigation(KeyResponder.NavigationKey.Left);
                    break;
                case KeyboardNavigationController.Key.Right:
                    EnableHighlightingIfNeeded();
                    activeKeyResponder.HandleNavigation(KeyResponder.NavigationKey.Right);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(key), key, null);
            }
        }

        internal KeyResponder? GetTopLevelControlKeyResponder(TopLevelControlPanel.ControlConfig control)
        {
            return controlPanel.GetActiveKeyResponder(control);
        }

        #endregion

        #region Pause

        public void AnnouncePauseViewHomeClicked()
        {
            PauseViewHomeClicked?.Invoke();
        }

        public void AnnouncePauseViewResumeClicked()
        {
            PauseViewResumeClicked?.Invoke();
        }

        public void AnnouncePaused()
        {
            Paused?.Invoke();
        }

        #endregion
    }
}
