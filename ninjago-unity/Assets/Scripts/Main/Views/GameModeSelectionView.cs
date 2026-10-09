#nullable enable

using System;
using System.Collections.Generic;
using Nex.KeyboardNavigation;
using UnityEngine;
using UnityEngine.UI;

namespace Nex
{
    public class GameModeSelectionView : SimpleCanvasView
    {
        [Header("Config")]
        [SerializeField] GameModeConfig gameModeConfig = null!;

        [Header("Grid")]
        [SerializeField] GridLayoutGroup gridLayout = null!;
        [SerializeField] GridKeyResponder gridKeyResponder = null!;
        [SerializeField] GameModeSelectionButton buttonPrefab = null!;

        public event Action<GameModeType>? ModeSelected;
        public event Action? ExitRequested;

        bool modeChosen;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.GameModeSelection;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Exit;
        public override string AnalyticsScreenName => "game-mode-selection";

        public override bool OnControlButton(TopLevelControlPanel.ButtonKind buttonKind)
        {
            if (buttonKind != TopLevelControlPanel.ButtonKind.Exit) return base.OnControlButton(buttonKind);
            if (!IsActive) return false;

            ExitRequested?.Invoke();
            return true;
        }

        #endregion

        #region Initialization

        public void Initialize()
        {
            gridKeyResponder.NumColumns = gridLayout.constraintCount;

            var responders = new List<KeyResponder>(gameModeConfig.ModeOrders.Count);
            foreach (var mode in gameModeConfig.ModeOrders)
            {
                var button = Instantiate(buttonPrefab, gridLayout.transform);
                button.Initialize(mode, gameModeConfig.GetMode(mode).displayName);
                button.Clicked += HandleModeClicked;
                responders.Add(button.KeyResponder);
            }

            gridKeyResponder.ReinitializeResponders(responders);
        }

        #endregion

        #region Unity Events

        void HandleModeClicked(GameModeType mode)
        {
            // A second press during the scene transition must not load the game twice.
            if (!IsActive || modeChosen) return;
            modeChosen = true;

            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
            ModeSelected?.Invoke(mode);
        }

        #endregion
    }
}
