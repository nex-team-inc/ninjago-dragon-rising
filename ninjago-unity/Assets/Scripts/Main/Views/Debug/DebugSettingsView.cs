using Nex.Dev;
using UnityEngine;

namespace Nex
{
    public class DebugSettingsView : SimpleCanvasView
    {
        public override ViewIdentifier Identifier => ViewIdentifier.DebugSettings;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "debug-settings";

        [SerializeField] DebugSettingsPanel debugSettingsPanel;

        const int minVisibilityLevel = 0;

        protected override void Awake()
        {
            base.Awake();
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
    }
}
