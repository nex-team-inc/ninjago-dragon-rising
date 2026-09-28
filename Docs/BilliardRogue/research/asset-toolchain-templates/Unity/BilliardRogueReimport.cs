#nullable enable

using UnityEditor;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Re-runs BilliardRogueAssetPostprocessor on generated content only. Use this after editing the import
    /// rules instead of bumping GetVersion(), which reimports every texture/model/audio clip in the project.
    /// </summary>
    public static class BilliardRogueReimport
    {
        static readonly string[] Roots =
        {
            "Assets/Models/BilliardRogue", "Assets/Textures/BilliardRogue", "Assets/Sprites/BilliardRogue",
            "Assets/Audio/Sfx/BilliardRogue", "Assets/Audio/Bgm/BilliardRogue",
        };

        [MenuItem("Nex/Billiard Rogue/Reimport Generated Assets")]
        public static void ReimportAll()
        {
            AssetDatabase.Refresh();
            foreach (var root in Roots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                AssetDatabase.ImportAsset(root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
