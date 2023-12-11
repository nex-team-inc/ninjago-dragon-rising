using Nex.Dev;
using UnityEngine;

namespace Nex
{
    public class DebugSettingsView : SimpleCanvasView
    {
        public override ViewIdentifier Identifier => ViewIdentifier.DebugSettings;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;

        public override bool RequiresAdditionalBackgroundBlur => true;

        [SerializeField] DebugSettingsPanel debugSettingsPanel;

        const int minVisibilityLevel = 0;

        void Start()
        {
            var playerDataManager = PlayerDataManager.Instance;
            var debugSettings = playerDataManager.DebugSettings;

            debugSettingsPanel.Initialize(
                () =>
                {
                    playerDataManager.SaveDebugSettings();
                    playerDataManager.SavePlayerPreference();
                },
                () => PopSelf()
            );
            debugSettingsPanel.PopulateRows(debugSettings,
                // ReSharper disable once RedundantArgumentDefaultValue
                minVisibilityLevel);
        }

        void Update()
        {
            if (!IsActive) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnBackButton();
            }
        }
    }
}
