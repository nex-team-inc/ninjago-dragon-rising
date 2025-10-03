#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using NaughtyAttributes;
using Newtonsoft.Json;
using Nex.Platform;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Nex
{
    public class RemoteConfigManager : Singleton<RemoteConfigManager>
    {
        #region Variables

        [SerializeField] RemoteConfig defaultValue = null!;
        [SerializeField] ConfigOrigin ConfigOrigin = ConfigOrigin.Default;

        [SerializeField] AsyncReactiveProperty<RemoteConfig> value = null!;

        public IReadOnlyAsyncReactiveProperty<RemoteConfig?> RemoteConfig { get; private set; } = null!;
        public IReadOnlyAsyncReactiveProperty<RemoteConfig> RemoteConfigOrDefault => value;

        public IUniTaskAsyncEnumerable<RemoteConfig> OnUpdate => value.WithoutCurrent();
        public IUniTaskAsyncEnumerable<RemoteConfig> CurrentAndOnUpdate => RemoteConfig.Value is null ? value.WithoutCurrent() : value;

        public readonly UniTaskCompletionSource configFetchedTaskCompletionSource = new();

        #endregion

        #region Singleton

        protected override RemoteConfigManager GetThis() => this;

        #endregion

        #region Lifecycle

        protected override void Awake()
        {
            value = new AsyncReactiveProperty<RemoteConfig>(defaultValue);
            RemoteConfig = new ReadOnlyAsyncReactiveProperty<RemoteConfig?>(
                null,
                value.WithoutCurrent().Select(s => (RemoteConfig?) s),
                Application.exitCancellationToken
            );

            base.Awake();
        }

        void Start()
        {
            InitializeAsync().Forget();
        }

        #endregion

        #region Initialization

        struct UserAttributes
        {
        }

        struct AppAttributes
        {
        }

        async UniTask InitializeAsync()
        {
            var options = new InitializationOptions();
            if (Env.isProduction)
            {
                options.SetEnvironmentName(EnvironmentInfo.ProductionEnvironmentName);
            }
            else
            {
                options.SetEnvironmentName(EnvironmentInfo.StagingEnvironmentName);
            }
            await UnityServices.InitializeAsync(options);

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                try
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                catch
                {
                    // Do nothing.
                }
            }

            RemoteConfigService.Instance.FetchCompleted += ApplyRemoteConfig;
            FetchConfigs();
        }

        #endregion

        #region Fetch Configs

        [Button]
        void FetchConfigs()
        {
            RemoteConfigService.Instance.FetchConfigs(new UserAttributes(), new AppAttributes());
        }

        void ApplyRemoteConfig(ConfigResponse configResponse)
        {
            ConfigOrigin = configResponse.requestOrigin;
            Debug.Log($"RemoteConfigManager: Got from {ConfigOrigin}: {JsonConvert.SerializeObject(RemoteConfigService.Instance.appConfig.config)}");

            value.Value = Nex.RemoteConfig.Create(RemoteConfigService.Instance.appConfig, defaultValue);

            configFetchedTaskCompletionSource.TrySetResult();
        }

        public async UniTask<RemoteConfig> GetRemoteConfigAsync(CancellationToken cancellationToken)
        {
            return RemoteConfig.Value ?? await value.WithoutCurrent().FirstAsync(cancellationToken);
        }

        #endregion
    }
}
