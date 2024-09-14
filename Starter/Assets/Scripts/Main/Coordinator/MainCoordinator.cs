#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

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
            return welcomeScreenView;
        }

        #endregion
    }
}
