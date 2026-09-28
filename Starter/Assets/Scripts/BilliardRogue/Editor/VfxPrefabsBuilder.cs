#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds every VFX prefab from code (TDD D13, §16, §17): one pooled burst per VfxManager.VisualEffect plus the three
    /// looping act ambients, their materials, the VfxManager registry, the ActDefinition ambient links and the review
    /// gallery scene. Idempotent; re-run after RenderPipelineBuilder (World layer) and the Rendering shaders land.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.VfxPrefabsBuilder.Run();' --project-path …/Starter
    /// </summary>
    public static class VfxPrefabsBuilder
    {
        const int BurstParticleBudget = 60;
        const int AmbientParticleBudget = 60;

        [MenuItem("Nex/Billiard Rogue/VFX Prefabs", priority = 60)]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var warnings = new List<string>();
            var recipes = Recipes(warnings);
            var library = new VfxMaterialLibrary(warnings);
            var worldLayer = WorldLayers.Resolve();
            if (worldLayer < 0) warnings.Add("Layer 'World' missing (RenderPipelineBuilder): prefabs keep the Default layer the WorldCamera does not render; re-run after it.");

            var staging = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var recipe in recipes)
                {
                    VfxPrefabWriter.Write(recipe, library, staging, worldLayer);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(staging);
            }

            AssetDatabase.SaveAssets();
            var registered = VfxRegistryWriter.RegisterBursts(recipes, warnings);
            var linked = VfxRegistryWriter.LinkAmbient(recipes, warnings);
            var gallery = VfxGalleryBuilder.Build(recipes, warnings);
            AssetDatabase.SaveAssets();
            return Report(recipes.Count, registered, linked, gallery, warnings);
        }

        #region Helpers

        static List<VfxRecipe> Recipes(List<string> warnings)
        {
            var recipes = new List<VfxRecipe>();
            VfxCombatRecipes.AddTo(recipes);
            VfxFeedbackRecipes.AddTo(recipes);
            AddPlaceholders(recipes, warnings);
            VfxAmbientRecipes.AddTo(recipes);
            foreach (var recipe in recipes)
            {
                var budget = recipe.IsAmbient ? AmbientParticleBudget : BurstParticleBudget;
                if (recipe.ParticleBudget() > budget) warnings.Add($"{recipe.prefabName}: {recipe.ParticleBudget()} particles exceed the budget of {budget}.");
            }

            return recipes;
        }

        /// <summary>An enum entry without a recipe still gets a small spark so the registry stays complete (TDD D11).</summary>
        static void AddPlaceholders(List<VfxRecipe> recipes, List<string> warnings)
        {
            foreach (VfxManager.VisualEffect effect in Enum.GetValues(typeof(VfxManager.VisualEffect)))
            {
                if (HasRecipe(recipes, effect)) continue;
                warnings.Add($"VisualEffect {effect} has no designed recipe; a placeholder spark is built.");
                recipes.Add(VfxRecipe.Burst(effect, 2, 8)
                    .Add(VfxLayerSpec.Glow("Sparks", "Spark").Burst(6).Life(0.4f).Speed(2f, 4f).Sphere(0.1f).Drag(4f).Size(0.5f).Fade()));
            }
        }

        static bool HasRecipe(List<VfxRecipe> recipes, VfxManager.VisualEffect effect)
        {
            foreach (var recipe in recipes)
            {
                if (recipe.effect == effect) return true;
            }

            return false;
        }

        static string Report(int prefabs, int registered, int linked, string gallery, List<string> warnings)
        {
            var report = new StringBuilder("[VfxPrefabsBuilder] ");
            report.Append(prefabs).Append(" prefabs, ").Append(registered).Append(" registered in VfxManager, ")
                .Append(linked).Append(" ambient slots linked, gallery ").Append(gallery);
            if (warnings.Count > 0) report.Append("; warnings (").Append(warnings.Count).Append("): ").Append(string.Join(" | ", warnings));
            return report.ToString();
        }

        #endregion
    }
}
