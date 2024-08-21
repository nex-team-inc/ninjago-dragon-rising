using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Nex.CameraGuard;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class CherryIntegrationManager : Singleton<CherryIntegrationManager>
    {
        #region Life Cycle

        protected override CherryIntegrationManager GetThis() => this;

        protected override void Awake()
        {
            base.Awake();
            SetupPreferGameStoppedBindings();
            SetupKeyboardNavigation();
        }

#if UNITY_EDITOR
        bool isDebugCameraMuted;
        void Update()
        {
            if (Input.GetKey(KeyCode.LeftApple) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.M))
            {
                isDebugCameraMuted = !isDebugCameraMuted;
                SetIsCameraMutedForDebug(isDebugCameraMuted);
            }
        }
#endif

        #endregion

        #region Camera Mute Button

        static readonly AsyncReactiveProperty<bool> fakeIsMutedProperty = new(false);

        public static IReadOnlyAsyncReactiveProperty<bool> IsCameraMutedProperty
        {
            get
            {
                var cameraGuardController = CameraGuardController.Instance;
                // The guard controller is null if the game is played from the game scene directly.
                return cameraGuardController != null ? cameraGuardController.isMuted : fakeIsMutedProperty;
            }
        }

        public void SetIsCameraMutedForDebug(bool value)
        {
            if (IsCameraMutedProperty.Value == value) return;

            if (typeof(CameraGuardController).GetField("mutableMuted", BindingFlags.NonPublic | BindingFlags.Instance)
                    is { } mutableMutedField &&
                mutableMutedField.GetValue(CameraGuardController.Instance) is AsyncReactiveProperty<bool> mutableMuted)
            {
                mutableMuted.Value = value;
            }
        }

        #endregion

        #region Prefer Game Stopped

        static readonly AsyncReactiveProperty<bool> rawPreferGameStoppedProperty = new(false);
        readonly AsyncReactiveProperty<bool> preferGameStopped = new(false);

        // Always depends on the value of IsCameraMuted.
        public IReadOnlyAsyncReactiveProperty<bool> PreferGameStopped => preferGameStopped;

        void SetupPreferGameStoppedBindings()
        {
            rawPreferGameStoppedProperty.CombineLatest(IsCameraMutedProperty, (a, b) => a || b)
                .Subscribe(value =>
                {
                    if (value == preferGameStopped.Value) return;
                    preferGameStopped.Value = value;
                });
        }

        public async UniTask WaitForResumeIfNeeded(CancellationToken cancellationToken)
        {
            if (!preferGameStopped.Value) return;

            bool pauseValue;
            do
            {
                pauseValue = await preferGameStopped.WaitAsync(cancellationToken);
            } while (pauseValue);
        }

        #endregion

        #region Keyboard Navigation

        static readonly AsyncReactiveProperty<bool> keyboardControlDisabledProperty = new(false);

        public bool IsKeyboardControlDisabled() => keyboardControlDisabledProperty.Value;

        void SetupKeyboardNavigation()
        {
            keyboardControlDisabledProperty.DistinctUntilChanged().SkipWhile(disabled => !disabled).Subscribe(
                disabled =>
                {
                    if (disabled) ViewManager.SuspendKeyboardNavigation();
                    else ViewManager.ResumeKeyboardNavigation();
                }, destroyCancellationToken);
        }

        #endregion

        #region GameActionDelegate Integration

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeBeforeSceneLoad()
        {
            Platform.GameActionDelegate.Instance = new GameActionDelegate();
        }

        sealed class GameActionDelegate : Nex.Platform.GameActionDelegate
        {
            public override void StopPlayingGameIfNeeded()
            {
                rawPreferGameStoppedProperty.Value = true;
            }

            public override void ResumeStoppedGameIfNeeded()
            {
                rawPreferGameStoppedProperty.Value = false;
            }

            public override void BackToInitialScreen()
            {
                // TODO: To be implemented.
            }

            public override bool IsInitialScreen()
            {
                // TODO: To be implemented.
                return true;
            }

            bool allKeyboardControlDisabled;

            public override void DisableAllKeyboardControl()
            {
                keyboardControlDisabledProperty.Value = true;
            }

            public override void RestoreDisabledKeyboardControl()
            {
                keyboardControlDisabledProperty.Value = false;
            }

            public override string GetActiveScreenName()
            {
                // TODO: To be implemented.
                return "";
            }
        }

        #endregion
    }
}
