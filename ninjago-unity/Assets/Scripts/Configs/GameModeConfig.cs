#nullable enable

using System;
using System.Collections.Generic;
using Nex.Util;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace Nex
{
    [CreateAssetMenu(fileName = "GameModeConfig", menuName = "Nex/Configs/GameModeConfig")]
    public class GameModeConfig : ScriptableObject
    {
        [Serializable]
        public class ModeDefinition
        {
            public LocalizedString displayName = new();
            [Tooltip("Addressable prefab with a BaseGame component on its root.")]
            public AssetReferenceGameObject gamePrefab = null!;
            [Tooltip("Above 1, the menu asks how many players before loading the game.")]
            [Range(1, 2)] public int maxPlayers = 1;
        }

        [Header("Modes")]
        [SerializeField] EnumDictionary<GameModeType, ModeDefinition> modes = new();

        [Header("Menu Order")]
        [SerializeField] List<GameModeType> modeOrders = new();

        public IReadOnlyList<GameModeType> ModeOrders => modeOrders;

        #region Public API

        public ModeDefinition GetMode(GameModeType mode) => modes[mode];

        #endregion
    }
}
