#nullable enable

using System;
using Nex.KeyboardNavigation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>1 Player / 2 Players cards (left/right + Enter). Starts on the remembered PlayerPreference.numPlayers.</summary>
    public sealed class PlayerModeView : RogueView
    {
        [Header("Cards")]
        [SerializeField] UnityEngine.UI.Button onePlayerButton = null!;
        [SerializeField] UnityEngine.UI.Button twoPlayersButton = null!;
        [SerializeField] GroupKeyResponder cardsGroup = null!;

        /// <summary>Number of players (1 or 2); already saved to PlayerPreference.numPlayers.</summary>
        public event Action<int>? PlayersChosen;

        public override ViewIdentifier Identifier => ViewIdentifier.PlayerMode;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "player_mode";

        protected override void Awake()
        {
            base.Awake();
            onePlayerButton.onClick.AddListener(HandleOnePlayer);
            twoPlayersButton.onClick.AddListener(HandleTwoPlayers);
            var remembered = Mathf.Clamp(PlayerDataManager.Instance.PlayerPreference.numPlayers, 1, 2);
            cardsGroup.SetInitialActiveIndex(remembered - 1);
        }

        public override void OnBackButton()
        {
            if (!IsActive) return;
            TrackBack();
            base.OnBackButton();
        }

        void HandleOnePlayer() => Choose(1, "one_player");

        void HandleTwoPlayers() => Choose(2, "two_players");

        void Choose(int players, string button)
        {
            if (!IsActive) return;
            TrackButton(button, players);
            PlayerDataManager.Instance.ScopedPlayerPreferenceUpdate(preference => preference.numPlayers = players);
            PlayersChosen?.Invoke(players);
        }
    }
}
