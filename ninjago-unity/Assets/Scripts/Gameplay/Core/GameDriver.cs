#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Nex
{
    public class GameDriver : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] GameModeConfig gameModeConfig = null!;

        [Header("Detection")]
        [SerializeField] DetectionManager detectionManager = null!;

        [Header("Views")]
        [SerializeField] ViewManager viewManager = null!;

        AsyncOperationHandle<GameObject> gamePrefabHandle;

        #region Life Cycle

        // GameScene's composition root: activated by SingletonSpawner once the singletons exist.
        void Start()
        {
            RunSelectedGameAsync().Forget();
        }

        void OnDestroy()
        {
            if (gamePrefabHandle.IsValid())
            {
                Addressables.Release(gamePrefabHandle);
            }
        }

        #endregion

        #region Game Flow

        async UniTaskVoid RunSelectedGameAsync()
        {
            var mode = GameConfigsManager.Instance.SelectedMode;
            gamePrefabHandle = Addressables.LoadAssetAsync<GameObject>(gameModeConfig.GetMode(mode).gamePrefab);
            var gamePrefab = await gamePrefabHandle;

            var game = Instantiate(gamePrefab.GetComponent<BaseGame>(), transform);
            detectionManager.Initialize(game.NumOfPlayers);
            game.Initialize(detectionManager, viewManager);

            await ScreenBlockerManager.Instance.Hide();
            await game.RunAsync(destroyCancellationToken);
            await ScreenBlockerManager.Instance.Show();

            SceneManager.LoadScene(GameConfigsManager.Instance.MainScene);
        }

        #endregion
    }
}
