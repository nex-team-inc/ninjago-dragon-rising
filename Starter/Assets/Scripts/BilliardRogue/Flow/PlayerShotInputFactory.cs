#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Shot inputs (Input module): one PlayerShotInput.prefab instance per player whose ShotInputRouter picks between
    /// the paw, mouse/keyboard and bot inputs. Every instance shares one ShotInputContext; its providers are polled,
    /// so preference and debug changes apply mid-run. Without the Input module prefab there is no paw input.
    /// </summary>
    public sealed class PlayerShotInputFactory
    {
        readonly GameObject? prefab;
        readonly CameraSession cameraSession;
        readonly ShotInputContext context;

        public PlayerShotInputFactory(GameObject? aPrefab, CameraSession aCameraSession, ControlConfig control, GameRules rules,
            Camera worldCamera, ArenaLayout layout, Func<RunState?> activeRun)
        {
            prefab = aPrefab;
            cameraSession = aCameraSession;
            context = new ShotInputContext
            {
                control = control,
                rules = rules,
                leftHanded = () => PlayerDataManager.Instance.PlayerPreference.leftHandedCue,
                forceDebugInput = () => PlayerDataManager.Instance.DebugSettings.forceDebugInput,
                run = activeRun,
                worldCamera = worldCamera,
                layout = layout,
            };
        }

        /// <summary>CalibrationShotInputFactory: null when the Input module prefab is missing.</summary>
        public IShotInput? CreateCalibrationShotInput(int playerIndex, OnePlayerDetectionEngine engine, Transform parent)
        {
            return CreateShotInput(playerIndex, engine, parent);
        }

        /// <summary>ShotInputsFactory: one input per player of the running camera session (a null input without the prefab).</summary>
        public IShotInput[] CreateShotInputs(int numPlayers, Transform parent)
        {
            var inputs = new IShotInput[numPlayers];
            for (var playerIndex = 0; playerIndex < numPlayers; playerIndex++)
            {
                inputs[playerIndex] = CreateShotInput(playerIndex, cameraSession.GetEngine(playerIndex), parent) ?? NullShotInput.Instance;
            }

            return inputs;
        }

        IShotInput? CreateShotInput(int playerIndex, OnePlayerDetectionEngine engine, Transform parent)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[PlayerShotInputFactory] PlayerShotInput prefab missing (Input module): no paw input.");
                return null;
            }

            var instance = Object.Instantiate(prefab, parent);
            instance.name = $"{prefab.name}_P{playerIndex + 1}";
            var router = instance.GetComponent<ShotInputRouter>();
            router.Initialize(playerIndex, engine, context);
            return router;
        }
    }
}
