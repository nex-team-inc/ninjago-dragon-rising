#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jazz;
using Nex.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Scene entry point of Main.unity (inactive until SingletonSpawner activates it, like MainInitializerExample):
    /// splash on the first app start, frame rate, locale restore, blocker hide, then the coordinator's main flow.
    /// </summary>
    public sealed class BilliardRogueInitializer : MonoBehaviour
    {
        const int TargetFrameRate = 60;

        [Header("Boot")]
        [Tooltip("Editor only: play the Nex splash even though the Editor normally skips it.")]
        [SerializeField] bool debugShowSplash;
        [Tooltip("Addressable NexSplashScreen prefab; an empty reference skips the splash.")]
        [SerializeField] AssetReferenceGameObject nexSplashScreenReference = null!;
        [SerializeField] BilliardRogueCoordinator coordinator = null!;

        readonly bool alwaysSkipSplash = PlatformUtils.IsEditor;

        #region Life Cycle

        void Start()
        {
#if UNITY_STANDALONE_OSX
            GlobalOptions.shared.performanceModeOptions.autoEnable = false;
            Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
#endif
            Application.targetFrameRate = TargetFrameRate;
            GlobalOptions.shared.frameResolution = (1920, 1080);
#if UNITY_EDITOR
            // Editor CLI sessions drive play mode from an unfocused Editor, which otherwise pauses the player loop
            // (the starter examples do the same; Android TV ignores this flag).
            Application.runInBackground = true;
#endif
            BootAsync(destroyCancellationToken).Forget();
        }

        #endregion

        #region Boot

        async UniTaskVoid BootAsync(CancellationToken ct)
        {
            if (ShouldPlaySplash())
            {
                await PlaySplashAsync();
            }

            await coordinator.Initialize();
            await LocaleRestore.ApplyAsync(PlayerDataManager.Instance.PlayerPreference, ct);

            // ScreenBlockerManager.Hide misbehaves right after another feedback finished (see MainInitializerExample);
            // the short delay keeps the hide animation correct.
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: ct);
            await ScreenBlockerManager.Instance.Hide();
            await coordinator.StartMain();
        }

        bool ShouldPlaySplash()
        {
            var application = ApplicationManager.Instance;
            if (!application.FirstAppStart) return false;
            application.FirstAppStart = false;
            if (!nexSplashScreenReference.RuntimeKeyIsValid()) return false;
            return debugShowSplash || !alwaysSkipSplash;
        }

        async UniTask PlaySplashAsync()
        {
            var player = AddressableSplashScreenPlayer.Create(nexSplashScreenReference);
            await player.Prepare();
            await player.Play();
            await player.DismissDestroy();
        }

        #endregion
    }
}
