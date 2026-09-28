#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    // Shot inputs (Input module): one PlayerShotInput.prefab instance per player whose ShotInputRouter picks between
    // the paw, mouse/keyboard and bot inputs. Every instance shares one ShotInputContext; its providers are polled,
    // so preference and debug changes apply mid-run.
    public sealed partial class BilliardRogueCoordinator
    {
        [Header("Input (Input module prefab, wired by FlowPrefabsBuilder)")]
        [Tooltip("One instance per player: PawShotInput + DebugShotInput + AutoAimBot + ShotInputRouter.")]
        [SerializeField] GameObject? playerShotInputPrefab;

        ShotInputContext? shotInputContext;

        IShotInput? CreateCalibrationShotInput(int playerIndex, OnePlayerDetectionEngine engine, Transform parent)
        {
            return CreateShotInput(playerIndex, engine, parent);
        }

        IShotInput[] CreateShotInputs(int numPlayers, Transform parent)
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
            if (playerShotInputPrefab == null)
            {
                Debug.LogWarning("[BilliardRogueCoordinator] PlayerShotInput prefab missing (Input module): no paw input.");
                return null;
            }

            var instance = Instantiate(playerShotInputPrefab, parent);
            instance.name = $"{playerShotInputPrefab.name}_P{playerIndex + 1}";
            var router = instance.GetComponent<ShotInputRouter>();
            router.Initialize(playerIndex, engine, shotInputContext ??= CreateShotInputContext());
            return router;
        }

        ShotInputContext CreateShotInputContext()
        {
            return new ShotInputContext
            {
                control = config.Control,
                rules = rules,
                leftHanded = () => PlayerDataManager.Instance.PlayerPreference.leftHandedCue,
                forceDebugInput = () => PlayerDataManager.Instance.DebugSettings.forceDebugInput,
                run = ActiveRun,
                worldCamera = worldCameraRig.WorldCamera,
                layout = arenaLayout,
            };
        }

        /// <summary>The run being played, for the bot; null during calibration and on the title.</summary>
        RunState? ActiveRun()
        {
            var gameplay = runFlow.ActiveGameplay;
            return gameplay != null ? gameplay.Run : null;
        }
    }
}
