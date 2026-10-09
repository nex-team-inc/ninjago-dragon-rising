#nullable enable

using System;
using Nex.Util;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Stone Kick and Earth Seal: tuning assets, audio registry entries, worlds, views, the Addressable game prefabs and
    /// their GameModeConfig entries (the menu order itself is written by NinjagoGamesBuilder).
    /// </summary>
    public static class CursorGamesBuilder
    {
        public const string HandCursorConfigPath = ConfigsRoot + "/HandCursorConfig.asset";
        public const string StoneKickConfigPath = ConfigsRoot + "/StoneKickConfig.asset";
        public const string EarthSealConfigPath = ConfigsRoot + "/EarthSealConfig.asset";
        const string StoneKickGamePath = NinjagoGamesBuilder.GamesRoot + "/StoneKickGame.prefab";
        const string EarthSealGamePath = NinjagoGamesBuilder.GamesRoot + "/EarthSealGame.prefab";
        const string SfxRoot = "Assets/Audio/Sfx/Ninjago";
        const string BgmRoot = "Assets/Audio/Bgm/Ninjago";

        #region Entry Point

        public static void Build()
        {
            BuildConfigs();
            BuildAudioRegistry();
            CursorGamesWorldBuilder.Build();
            CursorGamesViewsBuilder.Build();
            BuildGames();
            RegisterModes();
        }

        #endregion

        #region Configs

        public static void BuildConfigs()
        {
            var cursor = LoadOrCreate<HandCursorConfig>(HandCursorConfigPath);
            var kick = LoadOrCreate<StoneKickConfig>(StoneKickConfigPath);
            var sealIsNew = AssetDatabase.LoadAssetAtPath<EarthSealConfig>(EarthSealConfigPath) == null;
            var seal = LoadOrCreate<EarthSealConfig>(EarthSealConfigPath);
            // Rebuilds keep a designer's wave tuning; only a new asset gets the starting waves.
            if (sealIsNew) WriteWaves(seal);

            const string managerPath = "Assets/Prefabs/Singletons/GameConfigsManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(managerPath);
            try
            {
                var manager = root.GetComponent<GameConfigsManager>();
                Set(manager, "handCursorConfig", cursor);
                Set(manager, "stoneKickConfig", kick);
                Set(manager, "earthSealConfig", seal);
                PrefabUtility.SaveAsPrefabAsset(root, managerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void WriteWaves(EarthSealConfig seal)
        {
            var serialized = new SerializedObject(seal);
            var waves = serialized.FindProperty("waves");
            Wave(waves, EarthSealWave.Wave1, 3, 20f, 0.07f, 1, 1, 2.2f, 2f, 4f);
            Wave(waves, EarthSealWave.Wave2, 3, 20f, 0.09f, 2, 1, 1.6f, 2f, 3.6f);
            Wave(waves, EarthSealWave.Wave3, 3, 20f, 0.11f, 2, 2, 1.5f, 1.6f, 3.4f);
            Wave(waves, EarthSealWave.Wave4, 4, 22f, 0.12f, 3, 1, 1f, 1.3f, 3.2f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seal);
        }

        static void Wave(SerializedProperty waves, EarthSealWave wave, int grid, float seconds, float chance, int maxCracks, int spacing, float warning,
            float sealTime, float breakthrough)
        {
            var value = EnumDictionaryEditorUtils.GetValueProperty(waves, (int)wave);
            value.FindPropertyRelative("gridSize").intValue = grid;
            value.FindPropertyRelative("waveSeconds").floatValue = seconds;
            value.FindPropertyRelative("beatSeconds").floatValue = 1f;
            value.FindPropertyRelative("crackChance").floatValue = chance;
            value.FindPropertyRelative("maxCracks").intValue = maxCracks;
            value.FindPropertyRelative("minCrackSpacing").intValue = spacing;
            value.FindPropertyRelative("warningSeconds").floatValue = warning;
            value.FindPropertyRelative("sealSeconds").floatValue = sealTime;
            value.FindPropertyRelative("breakthroughSeconds").floatValue = breakthrough;
        }

        #endregion

        #region Audio

        public static void BuildAudioRegistry()
        {
            NinjagoAssetsBuilder.EditPrefab<SfxManager>("Assets/Prefabs/Singletons/SfxManager.prefab", "soundEffectDict", dict =>
            {
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.RockSlash, SfxRoot, "RockSlash_1", "RockSlash_2");
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.KickThud, SfxRoot, "KickThud_1", "KickThud_2");
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.PieceHit, SfxRoot, "PieceHit_1");
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.SealComplete, SfxRoot, "SealComplete_1");
                NinjagoAssetsBuilder.Clips(dict, SfxManager.SoundEffect.Breakthrough, SfxRoot, "Breakthrough_1", "Breakthrough_2");
            });
            NinjagoAssetsBuilder.EditPrefab<BgmManager>("Assets/Prefabs/Singletons/BgmManager.prefab", "bgmDict", dict =>
            {
                EnumDictionaryEditorUtils.GetValueProperty(dict, (int)BgmManager.BgmType.StoneKick).objectReferenceValue = Load<AudioClip>($"{BgmRoot}/StoneKick.ogg");
                EnumDictionaryEditorUtils.GetValueProperty(dict, (int)BgmManager.BgmType.EarthSeal).objectReferenceValue = Load<AudioClip>($"{BgmRoot}/EarthSeal.ogg");
            });
        }

        #endregion

        #region Game Prefabs

        public static void BuildGames()
        {
            BuildGame<StoneKickGame>("StoneKickGame", StoneKickGamePath, game =>
            {
                Set(game, "viewPrefab", LoadComponent<StoneKickView>(CursorGamesViewsBuilder.StoneKickViewPath));
                Set(game, "config", Load<StoneKickConfig>(StoneKickConfigPath));
                SetLocalized(game, "victoryTitle", "ninjago.result.victory");
                SetLocalized(game, "defeatTitle", "ninjago.result.defeat");
                SetLocalized(game, "playerLine", "ninjago.result.kick_player");
                SetLocalized(game, "kicksLine", "ninjago.result.kick_kicks");
                SetLocalized(game, "kicksLineNoGap", "ninjago.result.kick_kicks_no_gap");
                SetLocalized(game, "bestLine", "ninjago.result.kick_best");
                SetLocalized(game, "newBestLine", "ninjago.result.new_best");
            });
            BuildGame<EarthSealGame>("EarthSealGame", EarthSealGamePath, game =>
            {
                Set(game, "viewPrefab", LoadComponent<EarthSealView>(CursorGamesViewsBuilder.EarthSealViewPath));
                Set(game, "config", Load<EarthSealConfig>(EarthSealConfigPath));
                SetLocalized(game, "heldTitle", "ninjago.result.seal_held");
                SetLocalized(game, "brokeTitle", "ninjago.result.seal_broke");
                SetLocalized(game, "wallLine", "ninjago.result.seal_wall");
                SetLocalized(game, "teamworkLine", "ninjago.result.seal_team");
                SetLocalized(game, "playerLine", "ninjago.result.seal_player");
                SetLocalized(game, "bestLine", "ninjago.result.seal_best");
                SetLocalized(game, "newBestLine", "ninjago.result.new_best");
            });
        }

        static void BuildGame<T>(string name, string path, Action<T> wire) where T : HandCursorGame
        {
            var root = new GameObject(name);
            var game = root.AddComponent<T>();
            NinjagoGamesBuilder.WireShared(game, root.transform);
            Set(game, "setupViewPrefab", LoadComponent<NinjagoSetupView>(CursorGamesViewsBuilder.SetupViewPath));
            var cursorsRoot = Empty("HandCursors", root.transform, Vector3.zero);
            var tracker = cursorsRoot.gameObject.AddComponent<HandCursorTracker>();
            var handPrefab = Load<GameObject>("Packages/team.nex.mdk.hand/Prefabs/HandPoseDetector.prefab");
            var hand = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab, cursorsRoot);
            hand.SetActive(false);
            Set(tracker, "handDetector", hand.GetComponent<Jazz.HandDetectionManager>());
            Set(game, "cursors", tracker);
            Set(game, "cursorConfig", Load<HandCursorConfig>(HandCursorConfigPath));
            wire(game);
            SavePrefab(root, path);
        }

        #endregion

        #region Menu

        static void RegisterModes()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var config = Load<GameModeConfig>(NinjagoGamesBuilder.GameModeConfigPath);
            var serialized = new SerializedObject(config);
            var modes = serialized.FindProperty("modes");
            NinjagoGamesBuilder.Mode(settings, modes, GameModeType.StoneKick, StoneKickGamePath, "ninjago.mode.stone_kick");
            NinjagoGamesBuilder.Mode(settings, modes, GameModeType.EarthSeal, EarthSealGamePath, "ninjago.mode.earth_seal");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        #endregion
    }
}
