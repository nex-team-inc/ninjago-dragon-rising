#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex
{
    public class MainCoordinator : MonoBehaviour
    {
        [SerializeField] ViewManager viewManager = null!;
        [Header("Game Selection")]
        [SerializeField] GameModeSelectionView gameModeSelectionViewPrefab = null!;

        bool prepared;

        UniTaskCompletionSource? preparationSource;

        #region Life Cycle

        void OnEnable()
        {
            if (prepared) return;

            prepared = true;
            preparationSource?.TrySetResult();
            preparationSource = null;
        }

        #endregion

        #region Public

        public async UniTask Initialize()
        {
            if (!prepared)
            {
                preparationSource = new UniTaskCompletionSource();
                await preparationSource.Task;
            }
        }

        public async UniTask StartMain()
        {
            await viewManager.PushView(CreateGameModeSelectionView(), animate: true);
        }

        #endregion

        #region Game Selection

        GameModeSelectionView CreateGameModeSelectionView()
        {
            var gameModeSelectionView = Instantiate(gameModeSelectionViewPrefab);
            gameModeSelectionView.Initialize();
            gameModeSelectionView.ModeSelected += GameModeSelectionOnModeSelected;
            gameModeSelectionView.ExitRequested += GameModeSelectionOnExitRequested;
            return gameModeSelectionView;
        }

        void GameModeSelectionOnModeSelected(GameModeType mode)
        {
            GameConfigsManager.Instance.SelectedMode = mode;
            LoadGameSceneAsync().Forget();
        }

        async UniTaskVoid LoadGameSceneAsync()
        {
            await ScreenBlockerManager.Instance.Show();
            SceneManager.LoadScene(GameConfigsManager.Instance.GameScene);
        }

        void GameModeSelectionOnExitRequested()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Nex.Platform.DeviceActionDelegate.Instance.ExitGame();
#endif
        }

        #endregion
    }
}
