#nullable enable

using System;
using Nex.KeyboardNavigation;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>1 Player / 2 Players, after a mini-game is picked. Starts focused on the remembered count.</summary>
    public class PlayerCountView : SimpleCanvasView
    {
        [Header("One Player Button")]
        [SerializeField] Button onePlayerButton = null!;
        [Header("Two Players Button")]
        [SerializeField] Button twoPlayersButton = null!;
        [Header("Keyboard Navigation")]
        [SerializeField] GroupKeyResponder buttonsGroup = null!;

        /// <summary>Number of players chosen (1 or 2).</summary>
        public event Action<int>? PlayersChosen;

        bool chosen;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.PlayerCount;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "player-count";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            base.OnBackButton();
        }

        #endregion

        #region Initialization

        public void Initialize(int rememberedPlayers)
        {
            onePlayerButton.onClick.AddListener(HandleOnePlayer);
            twoPlayersButton.onClick.AddListener(HandleTwoPlayers);
            buttonsGroup.SetInitialActiveIndex(Mathf.Clamp(rememberedPlayers, 1, 2) - 1);
        }

        #endregion

        #region Unity Events

        void HandleOnePlayer() => Choose(1);

        void HandleTwoPlayers() => Choose(2);

        void Choose(int players)
        {
            // A second press during the scene transition must not load the game twice.
            if (!IsActive || chosen) return;
            chosen = true;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, players == 1 ? "one_player" : "two_players", players);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
            PlayersChosen?.Invoke(players);
        }

        #endregion
    }
}
