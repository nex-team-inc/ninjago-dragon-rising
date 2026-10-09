#nullable enable

using UnityEditor;
using UnityEngine;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Rebuilds every Ninjago asset in dependency order. Idempotent: asset GUIDs are kept, contents are regenerated.
    /// CLI: unity command eval 'return Nex.Ninjago.Editor.NinjagoBuildAll.Run();' 300000 --timeout 320
    /// </summary>
    public static class NinjagoBuildAll
    {
        #region Entry Point

        [MenuItem("Nex/Ninjago/Build All")]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            NinjagoAssetsBuilder.BuildSprites();
            NinjagoAssetsBuilder.BuildConfigs();
            var localization = NinjagoLocalizationSeeder.Run();
            NinjagoAssetsBuilder.BuildAudioRegistry();
            NinjagoVfxBuilder.Build();
            NinjagoWorldBuilder.Build();
            NinjagoViewsBuilder.Build();
            NinjagoGamesBuilder.Build();
            AssetDatabase.SaveAssets();
            return $"[NinjagoBuildAll] done ({localization})";
        }

        #endregion
    }
}
