#nullable enable

using System;
using Cysharp.Threading.Tasks;
using Jazz;
using Nex.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Nex
{
    public class MainInitializerExample : MonoBehaviour
    {
        readonly bool alwaysSkipSplash = PlatformUtils.IsEditor;

        [SerializeField] AssetReferenceGameObject nexSplashScreenReference = null!;
        [SerializeField] AssetReferenceGameObject mainCoordinatorReference = null!;

        AsyncOperationHandle<GameObject> mainCoordinatorHandle;
        AddressableSplashScreenPlayer? nexSplashScreenPlayer;

        void OnDestroy()
        {
            Addressables.Release(mainCoordinatorHandle);
        }

        void Start()
        {
#if UNITY_STANDALONE_OSX
            GlobalOptions.shared.performanceModeOptions.autoEnable = false;
            Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
#endif
            if (ApplicationManager.Instance.FirstAppStart)
            {
                ApplicationManager.Instance.FirstAppStart = false;
                if (alwaysSkipSplash)
                {
                    StartWithoutSplash().Forget();
                }
                else
                {
                    StartWithSplash().Forget();
                }
            }
            else
            {
                StartWithoutSplash().Forget();
            }

            ConfigMdkGlobalSettings();
        }

        async UniTaskVoid StartWithSplash()
        {
            nexSplashScreenPlayer = AddressableSplashScreenPlayer.Create(nexSplashScreenReference);

            await nexSplashScreenPlayer.Prepare();
            await nexSplashScreenPlayer.Play();
            await nexSplashScreenPlayer.DismissDestroy();

            var coordinator = await LoadMainCoordinator();
            await coordinator.Initialize();

            // Wait a bit here because ScreenBlockerManager.Instance.Hide() is buggy (PlayAsUniTask just finished within 0.1s)
            // A short delay helps making MMFeedback behaviour correctly.
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f));
            await ScreenBlockerManager.Instance.Hide();
            await coordinator.StartMain();
        }

        async UniTaskVoid StartWithoutSplash()
        {
            var coordinator = await LoadMainCoordinator();
            await coordinator.Initialize();

            // Wait a bit here because ScreenBlockerManager.Instance.Hide() is buggy (PlayAsUniTask just finished within 0.1s)
            // A short delay helps making MMFeedback behaviour correctly.
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f));
            await ScreenBlockerManager.Instance.Hide();
            await coordinator.StartMain();
        }

        async UniTask<MainCoordinator> LoadMainCoordinator()
        {
            mainCoordinatorHandle = mainCoordinatorReference.InstantiateAsync();
            var coordinatorObject = await mainCoordinatorHandle;
            var coordinator = coordinatorObject.GetComponent<MainCoordinator>();
            return coordinator;
        }

        void ConfigMdkGlobalSettings()
        {
            GlobalOptions.shared.frameResolution = (1920, 1080);
        }
    }
}
