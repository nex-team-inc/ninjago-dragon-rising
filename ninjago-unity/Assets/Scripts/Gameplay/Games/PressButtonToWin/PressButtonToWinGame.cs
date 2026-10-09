#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Platform;
using UnityEngine;

namespace Nex
{
    public class PressButtonToWinGame : BaseGame
    {
        [Header("Views")]
        [SerializeField] PressButtonToWinView viewPrefab = null!;

        ViewManager viewManager = null!;

        public override int NumOfPlayers => 1;

        #region Initialization

        public override void Initialize(DetectionManager detectionManager, ViewManager aViewManager)
        {
            viewManager = aViewManager;
        }

        #endregion

        #region Public API

        public override async UniTask RunAsync(CancellationToken cancellationToken)
        {
            var resultSource = new UniTaskCompletionSource<string>();
            var view = Instantiate(viewPrefab);
            view.Initialize();
            view.WinPressed += () => resultSource.TrySetResult("win");
            view.QuitRequested += () => resultSource.TrySetResult("quit");

            await viewManager.PushView(view);
            AnalyticsManager.Instance.TrackGameStart(view.AnalyticsScreenName, NumOfPlayers, nameof(GameModeType.PressButtonToWin));

            var result = await resultSource.Task.AttachExternalCancellation(cancellationToken);

            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
            AnalyticsManager.Instance.TrackGameStop(new GameAnalyticsProperties { ["result"] = result });
        }

        #endregion
    }
}
