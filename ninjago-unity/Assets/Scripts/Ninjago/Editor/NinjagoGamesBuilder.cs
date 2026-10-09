#nullable enable

using System.Collections.Generic;
using Nex.KeyboardNavigation;
using Nex.Util;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The two Addressable game prefabs, their GameModeConfig entries and menu order, the MainCoordinator's player
    /// count step, and the secret code that opens Debug Settings (up up down down left right left right).
    /// </summary>
    public static class NinjagoGamesBuilder
    {
        internal const string GamesRoot = PrefabsRoot + "/Games";
        const string SlipAndSpinPath = GamesRoot + "/SlipAndSpinGame.prefab";
        const string ChestChasePath = GamesRoot + "/ChestChaseGame.prefab";
        internal const string GameModeConfigPath = "Assets/Configs/GameModeConfig.asset";

        #region Entry Point

        public static void Build()
        {
            BuildSlipAndSpin();
            BuildChestChase();
            RegisterModes();
            WireMainCoordinator();
            AddSecretCode();
        }

        #endregion

        #region Game Prefabs

        static void BuildSlipAndSpin()
        {
            var root = new GameObject("SlipAndSpinGame");
            var game = root.AddComponent<SlipAndSpinGame>();
            WireShared(game, root.transform);
            var stormsRoot = Empty("HandStorm", root.transform, Vector3.zero);
            var storms = stormsRoot.gameObject.AddComponent<HandStormSampler>();
            var handPrefab = Load<GameObject>("Packages/team.nex.mdk.hand/Prefabs/HandPoseDetector.prefab");
            var hand = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab, stormsRoot);
            hand.SetActive(false);
            Set(storms, "handDetector", hand.GetComponent<Jazz.HandDetectionManager>());

            Set(game, "fightViewPrefab", LoadComponent<FightView>(NinjagoViewsBuilder.FightViewPath));
            Set(game, "fightConfig", Load<FightConfig>(NinjagoAssetsBuilder.FightConfigPath));
            Set(game, "storms", storms);
            SetLocalized(game, "victoryTitle", "ninjago.result.victory");
            SetLocalized(game, "defeatTitle", "ninjago.result.defeat");
            SetLocalized(game, "playerLine", "ninjago.result.fight_player");
            SetLocalized(game, "bestLine", "ninjago.result.fight_best");
            SetLocalized(game, "newBestLine", "ninjago.result.new_best");
            SavePrefab(root, SlipAndSpinPath);
        }

        static void BuildChestChase()
        {
            var root = new GameObject("ChestChaseGame");
            var game = root.AddComponent<ChestChaseGame>();
            WireShared(game, root.transform);
            Set(game, "runnerViewPrefab", LoadComponent<RunnerView>(NinjagoViewsBuilder.RunnerViewPath));
            Set(game, "chaseConfig", Load<ChaseConfig>(NinjagoAssetsBuilder.ChaseConfigPath));
            SetLocalized(game, "title", "ninjago.result.chase_title");
            SetLocalized(game, "carLine", "ninjago.result.chase_car");
            SetLocalized(game, "skycraftLine", "ninjago.result.chase_sky");
            SetLocalized(game, "steerLine", "ninjago.result.chase_steer");
            SetLocalized(game, "bestLine", "ninjago.result.chase_best");
            SetLocalized(game, "newBestLine", "ninjago.result.new_best");
            SavePrefab(root, ChestChasePath);
        }

        internal static void WireShared(NinjagoGame game, Transform root)
        {
            var sun = new GameObject("Sun");
            sun.transform.SetParent(root, false);
            sun.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 0.75f;
            root.gameObject.AddComponent<SimulatedBodyKeyboard>();
            var engines = Empty("Engines", root, Vector3.zero);
            Set(game, "detectionEnginePrefab", LoadComponent<OnePlayerDetectionEngine>(NinjagoWorldBuilder.EnginePath));
            Set(game, "enginesRoot", engines);
            Set(game, "setupViewPrefab", LoadComponent<NinjagoSetupView>(NinjagoViewsBuilder.SetupViewPath));
            Set(game, "resultViewPrefab", LoadComponent<NinjagoResultView>(NinjagoViewsBuilder.ResultViewPath));
            Set(game, "playersConfig", Load<NinjagoPlayersConfig>(NinjagoAssetsBuilder.PlayersConfigPath));
        }

        #endregion

        #region Menu

        static void RegisterModes()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var config = Load<GameModeConfig>(GameModeConfigPath);
            var serialized = new SerializedObject(config);
            var modes = serialized.FindProperty("modes");
            Mode(settings, modes, GameModeType.SlipAndSpin, SlipAndSpinPath, "ninjago.mode.slip_and_spin");
            Mode(settings, modes, GameModeType.ChestChase, ChestChasePath, "ninjago.mode.chest_chase");
            // The menu shows the Ninjago mini-games; the PressButtonToWin test entry stays defined but unlisted. The
            // StoneKick and EarthSeal definitions come from CursorGamesBuilder.
            var order = serialized.FindProperty("modeOrders");
            var listed = new[] { GameModeType.SlipAndSpin, GameModeType.ChestChase, GameModeType.StoneKick, GameModeType.EarthSeal };
            order.arraySize = listed.Length;
            for (var i = 0; i < listed.Length; i++) order.GetArrayElementAtIndex(i).intValue = (int)listed[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        internal static void Mode(UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings, SerializedProperty modes,
            GameModeType mode, string prefabPath, string nameKey)
        {
            var guid = AssetDatabase.AssetPathToGUID(prefabPath);
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            var value = EnumDictionaryEditorUtils.GetValueProperty(modes, (int)mode);
            WriteLocalized(value.FindPropertyRelative("displayName"), nameKey);
            value.FindPropertyRelative("gamePrefab.m_AssetGUID").stringValue = guid;
            value.FindPropertyRelative("maxPlayers").intValue = 2;
        }

        static void WireMainCoordinator()
        {
            const string path = "Assets/Prefabs/Coordinators/MainCoordinator.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var coordinator = root.GetComponent<MainCoordinator>();
                Set(coordinator, "gameModeConfig", Load<GameModeConfig>(GameModeConfigPath));
                Set(coordinator, "playerCountViewPrefab", LoadComponent<PlayerCountView>(NinjagoViewsBuilder.PlayerCountViewPath));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void AddSecretCode()
        {
            const string path = "Assets/Prefabs/Coordinators/MainViewManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var detector = root.GetComponent<SecretCodeSequenceDetector>();
                if (detector == null) detector = root.AddComponent<SecretCodeSequenceDetector>();
                var keys = new List<KeyboardNavigationController.Key>
                {
                    KeyboardNavigationController.Key.Up, KeyboardNavigationController.Key.Up,
                    KeyboardNavigationController.Key.Down, KeyboardNavigationController.Key.Down,
                    KeyboardNavigationController.Key.Left, KeyboardNavigationController.Key.Right,
                    KeyboardNavigationController.Key.Left, KeyboardNavigationController.Key.Right,
                };
                Set(detector, "configs", configs =>
                {
                    configs.arraySize = 1;
                    var sequence = configs.GetArrayElementAtIndex(0).FindPropertyRelative("sequence");
                    sequence.arraySize = keys.Count;
                    for (var i = 0; i < keys.Count; i++) sequence.GetArrayElementAtIndex(i).enumValueIndex = (int)keys[i];
                });
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        #endregion
    }
}
