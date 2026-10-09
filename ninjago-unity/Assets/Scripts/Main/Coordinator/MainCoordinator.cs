#nullable enable

using Cysharp.Threading.Tasks;
using Nex.Ninjago;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex
{
    public class MainCoordinator : MonoBehaviour
    {
        [SerializeField] ViewManager viewManager = null!;
        [Header("Game Selection")]
        [SerializeField] GameModeSelectionView gameModeSelectionViewPrefab = null!;
        [Header("Game Selection Config")]
        [SerializeField] GameModeConfig gameModeConfig = null!;
        [Header("Player Count")]
        [SerializeField] PlayerCountView playerCountViewPrefab = null!;

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
            gameModeSelectionView.Initialize(PlayerDataManager.Instance.NinjagoProgress.lastMiniGame);
            gameModeSelectionView.ModeSelected += GameModeSelectionOnModeSelected;
            gameModeSelectionView.ExitRequested += GameModeSelectionOnExitRequested;
            return gameModeSelectionView;
        }

        void GameModeSelectionOnModeSelected(GameModeType mode)
        {
            GameConfigsManager.Instance.SelectedMode = mode;
            NinjagoAnalytics.UiAction(gameModeSelectionViewPrefab.AnalyticsScreenName, mode.ToString());
            if (gameModeConfig.GetMode(mode).maxPlayers > 1)
            {
                viewManager.PushView(CreatePlayerCountView()).Forget();
                return;
            }

            LoadGameSceneAsync().Forget();
        }

        PlayerCountView CreatePlayerCountView()
        {
            var playerCountView = Instantiate(playerCountViewPrefab);
            playerCountView.Initialize(PlayerDataManager.Instance.PlayerPreference.numPlayers);
            playerCountView.PlayersChosen += PlayerCountOnPlayersChosen;
            return playerCountView;
        }

        void PlayerCountOnPlayersChosen(int numPlayers)
        {
            PlayerDataManager.Instance.ScopedPlayerPreferenceUpdate(preference => preference.numPlayers = numPlayers);
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
