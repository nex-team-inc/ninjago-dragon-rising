#nullable enable

using UnityEngine;

namespace Nex
{
    public class SetupUI : MonoBehaviour
    {
        [SerializeField] OnePlayerSetupUI onePlayerSetupUIPrefab = null!;
        [SerializeField] GameObject fullFrameSetupUIContainer = null!;
        [SerializeField] GameObject p1SetupUIContainer = null!;
        [SerializeField] GameObject p2SetupUIContainer = null!;

        #region Public

        public void Initialize(
            int aNumOfPlayers,
            SetupStateManager setupStateManager
        )
        {
            for (var playerIndex = 0; playerIndex < aNumOfPlayers; playerIndex++)
            {
                var container = aNumOfPlayers == 1
                    ? fullFrameSetupUIContainer
                    : playerIndex == 0
                        ? p1SetupUIContainer
                        : p2SetupUIContainer;

                var setupUI = Instantiate(onePlayerSetupUIPrefab, container.transform);
                setupUI.Initialize(playerIndex, setupStateManager);
            }
        }

        #endregion
    }
}
