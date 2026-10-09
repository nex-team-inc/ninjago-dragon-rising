#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex
{
    public class MainCoordinator : MonoBehaviour
    {
        [SerializeField] ViewManager viewManager = null!;
        [SerializeField] WelcomeScreenView welcomeScreenViewPrefab = null!;

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
            await viewManager.PushView(CreateWelcomeScreenView(), animate: true);
        }

        #endregion

        #region Welcome

        WelcomeScreenView CreateWelcomeScreenView()
        {
            var welcomeScreenView = Instantiate(welcomeScreenViewPrefab);
            welcomeScreenView.Initialize();
            welcomeScreenView.OnStartARGameButton += WelcomeScreenOnStartARGameButton;
            welcomeScreenView.OnStartNonARGameButton += WelcomeScreenOnStartNonARGameButton;
            welcomeScreenView.OnExitButton += WelcomeScreenOnExitButton;
            return welcomeScreenView;
        }

        void WelcomeScreenOnStartARGameButton()
        {
            SceneManager.LoadScene(GameConfigsManager.Instance.ARGameScene);
        }

        void WelcomeScreenOnStartNonARGameButton()
        {
            SceneManager.LoadScene(GameConfigsManager.Instance.NonARGameScene);
        }

        void WelcomeScreenOnExitButton()
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
