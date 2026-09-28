#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    // Integration seam: PlayerShotInput.prefab (PawShotInput + DebugShotInput + AutoAimBot + ShotInputRouter) is built
    // by the Input module. Until its Initialize signatures exist this partial instantiates the prefab and takes its
    // first IShotInput, or falls back to NullShotInput. Replace once the Input module lands:
    //   paw.Initialize(playerIndex, engine, config.Control, preference.leftHandedCue); router.Initialize(...)
    public sealed partial class BilliardRogueCoordinator
    {
        [Header("Input (Input module prefab, wired by FlowPrefabsBuilder)")]
        [Tooltip("One instance per player: PawShotInput + DebugShotInput + AutoAimBot + ShotInputRouter.")]
        [SerializeField] GameObject? playerShotInputPrefab;

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
            // TODO(integration): initialize the paw / debug / bot inputs and the router with (playerIndex, engine,
            // config.Control, PlayerDataManager.Instance.PlayerPreference.leftHandedCue).
            return instance.GetComponent<IShotInput>();
        }
    }
}
