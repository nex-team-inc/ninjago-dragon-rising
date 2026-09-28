#nullable enable

using System.Collections.Generic;
using System.Text;
using Nex.BilliardRogue.Simulation;
using Nex.Util;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds the world view prefabs of TDD §17 from the FBX models (placeholders when missing): one EnemyView
    /// prefab per EnemyType, Ball, FieldObjectView / PickupView per type, the cat and the BoardPresenter, then fills
    /// the EnemyDefinition / FieldObjectCatalog / BallDefinition references that are still null.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.WorldPrefabsBuilder.Run();'
    /// </summary>
    public static class WorldPrefabsBuilder
    {
        const string Root = WorldPrefabModels.PrefabRoot;

        [MenuItem("Nex/Billiard Rogue/World Prefabs", priority = 40)]
        public static void Menu() => Debug.Log(Run());

        #region Entry Point

        public static string Run()
        {
            WorldPrefabModels.ResetWarnings();
            var layer = WorldPrefabModels.WorldLayer();
            var palette = WorldPrefabModels.Palette(false);
            var glow = WorldPrefabModels.Glow();
            var enemies = new EnemyView[SimConstants.EnemyTypeCount];
            for (var i = 0; i < enemies.Length; i++)
            {
                enemies[i] = BuildEnemy((EnemyType)i, palette, glow, layer);
            }

            var ball = BuildBall(palette, glow, layer);
            var objects = new FieldObjectView[SimConstants.FieldObjectTypeCount];
            for (var i = 0; i < objects.Length; i++)
            {
                objects[i] = BuildFieldObject((FieldObjectType)i, palette, glow, layer);
            }

            var pickups = new PickupView[SimConstants.PickupTypeCount];
            for (var i = 0; i < pickups.Length; i++)
            {
                pickups[i] = BuildPickup((PickupType)i, palette, layer);
            }

            var cat = BoardPrefabBuilder.BuildCat(palette, WorldPrefabModels.Palette(true), glow, layer);
            BoardPrefabBuilder.BuildLabelPrefabs();
            BoardPrefabBuilder.BuildBoardPresenter(ball, cat, glow, layer);
            var filled = FillDefinitions(enemies, objects, pickups);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var report = new StringBuilder("[WorldPrefabsBuilder] prefabs: ");
            report.Append($"{enemies.Length} enemies, ball, {objects.Length} field objects, {pickups.Length} pickups, cat, labels, BoardPresenter; ");
            report.Append($"definition slots filled {filled}; warnings {WorldPrefabModels.Warnings.Count}");
            Debug.Log(report.ToString());
            return report.ToString();
        }

        #endregion

        #region Enemies

        static EnemyView BuildEnemy(EnemyType type, Material palette, Material glow, int layer)
        {
            var isBoss = type is EnemyType.KingSlime or EnemyType.BoneLich or EnemyType.CrystalGolem;
            var modelPath = isBoss ? $"Bosses/Boss_{type}.fbx" : $"Enemies/Enemy_{type}.fbx";
            var root = new GameObject($"Enemy_{type}");
            root.layer = Mathf.Max(0, layer);
            var footprint = isBoss ? 2f : 1f;
            var placeholderScale = new Vector3(0.7f * footprint, type == EnemyType.Totem ? 1.3f : 0.75f * footprint, 0.7f * footprint);
            var model = WorldPrefabModels.InstantiateModel(modelPath, root.transform, "Model", PrimitiveType.Capsule, placeholderScale, layer, out _);
            var renderers = new List<Renderer>();
            WorldPrefabModels.ApplyMaterial(model, palette, renderers);
            var emissive = WorldPrefabModels.CollectEmissive(renderers, type == EnemyType.Healer ? "Spores" : "");
            var bounds = WorldPrefabModels.RendererBounds(model);
            var marker = WorldPrefabModels.CreatePrimitive(root.transform, "ShieldMarker", PrimitiveType.Cube, new Vector3(0f, 0.35f, 0.5f), new Vector3(1f, 0.7f, 0.08f), glow, layer);
            marker.SetActive(false);

            var view = root.AddComponent<EnemyView>();
            var idle = root.AddComponent<EnemyIdleMotion>();
            var status = root.AddComponent<EnemyStatusVisuals>();
            var modelTransform = model.transform;

            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("model").objectReferenceValue = modelTransform;
            viewSo.FindProperty("idle").objectReferenceValue = idle;
            viewSo.FindProperty("statusVisuals").objectReferenceValue = status;
            viewSo.FindProperty("labelHeight").floatValue = Mathf.Clamp(bounds.max.y + 0.25f, 0.6f, 4f);
            viewSo.FindProperty("centerHeight").floatValue = Mathf.Clamp(bounds.center.y, 0.1f, 3f);
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            var idleSo = new SerializedObject(idle);
            idleSo.FindProperty("bobPart").objectReferenceValue = WorldPrefabModels.FindFirstPart(modelTransform, "Body", "Stem", "Base", "Robe", "Wall");
            SerializedPropertyWriter.Write(idleSo.FindProperty("blinkParts"), WorldPrefabModels.FindParts(modelTransform, "Eyes"));
            SerializedPropertyWriter.Write(idleSo.FindProperty("flapParts"), WorldPrefabModels.FindParts(modelTransform, "WingL", "WingR"));
            idleSo.ApplyModifiedPropertiesWithoutUndo();

            var statusSo = new SerializedObject(status);
            SerializedPropertyWriter.Write(statusSo.FindProperty("renderers"), renderers);
            SerializedPropertyWriter.Write(statusSo.FindProperty("emissiveRenderers"), emissive);
            statusSo.FindProperty("shieldMarker").objectReferenceValue = marker.transform;
            statusSo.FindProperty("shieldMarkerRenderer").objectReferenceValue = marker.GetComponent<Renderer>();
            statusSo.FindProperty("rotatingShieldPart").objectReferenceValue = WorldPrefabModels.FindPart(modelTransform, "ShieldCrystal");
            statusSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = WorldPrefabModels.SavePrefab(root, $"{Root}/Enemies/Enemy_{type}.prefab");
            return prefab.GetComponent<EnemyView>();
        }

        #endregion

        #region Balls, field objects, pickups

        static BallView BuildBall(Material palette, Material glow, int layer)
        {
            var root = new GameObject("Ball");
            root.layer = Mathf.Max(0, layer);
            var mesh = WorldPrefabModels.InstantiateModel("Balls/Ball.fbx", root.transform, "Mesh", PrimitiveType.Sphere, Vector3.one, layer, out var placeholder);
            if (placeholder) mesh.transform.localPosition = Vector3.zero;
            var renderers = new List<Renderer>();
            var basic = WorldPrefabModels.Ball(BallType.Basic);
            WorldPrefabModels.ApplyMaterial(mesh, basic != null ? basic : palette, renderers);
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = glow;
            trail.time = 0.16f;
            trail.minVertexDistance = 0.05f;
            trail.startWidth = 0.35f;
            trail.endWidth = 0f;
            trail.numCornerVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.autodestruct = false;
            trail.emitting = false;
            var view = root.AddComponent<BallView>();
            var so = new SerializedObject(view);
            so.FindProperty("mesh").objectReferenceValue = mesh.transform;
            so.FindProperty("meshRenderer").objectReferenceValue = renderers.Count > 0 ? renderers[0] : null;
            so.FindProperty("trail").objectReferenceValue = trail;
            so.ApplyModifiedPropertiesWithoutUndo();
            return WorldPrefabModels.SavePrefab(root, $"{Root}/Balls/Ball.prefab").GetComponent<BallView>();
        }

        static FieldObjectView BuildFieldObject(FieldObjectType type, Material palette, Material glow, int layer)
        {
            var root = new GameObject($"FieldObject_{type}");
            root.layer = Mathf.Max(0, layer);
            var (primitive, scale) = type switch
            {
                FieldObjectType.Pillar => (PrimitiveType.Cylinder, new Vector3(0.6f, 1.1f, 0.6f)),
                FieldObjectType.Crate => (PrimitiveType.Cube, new Vector3(0.8f, 0.8f, 0.8f)),
                FieldObjectType.Portal => (PrimitiveType.Cylinder, new Vector3(0.9f, 0.05f, 0.9f)),
                _ => (PrimitiveType.Cylinder, new Vector3(0.95f, 0.02f, 0.95f)),
            };
            var model = WorldPrefabModels.InstantiateModel($"Props/Prop_{type}.fbx", root.transform, "Model", primitive, scale, layer, out _);
            var renderers = new List<Renderer>();
            WorldPrefabModels.ApplyMaterial(model, palette, renderers);
            var emissive = WorldPrefabModels.CollectEmissive(renderers);
            var bounds = WorldPrefabModels.RendererBounds(model);
            var pips = new List<Renderer>();
            if (type == FieldObjectType.Crate)
            {
                const int pipCount = 8;
                for (var i = 0; i < pipCount; i++)
                {
                    var x = (i - (pipCount - 1) * 0.5f) * 0.1f;
                    var pip = WorldPrefabModels.CreatePrimitive(root.transform, $"Pip{i}", PrimitiveType.Cube, new Vector3(x, bounds.max.y + 0.06f, 0f), new Vector3(0.07f, 0.07f, 0.07f), glow, layer);
                    pips.Add(pip.GetComponent<Renderer>());
                }
            }

            var view = root.AddComponent<FieldObjectView>();
            var so = new SerializedObject(view);
            so.FindProperty("type").intValue = (int)type;
            so.FindProperty("model").objectReferenceValue = model.transform;
            so.FindProperty("spinPart").objectReferenceValue = WorldPrefabModels.FindPart(model.transform, "Swirl");
            SerializedPropertyWriter.Write(so.FindProperty("emissiveRenderers"), emissive);
            SerializedPropertyWriter.Write(so.FindProperty("pips"), pips);
            so.FindProperty("labelHeight").floatValue = Mathf.Clamp(bounds.max.y + 0.3f, 0.4f, 3f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return WorldPrefabModels.SavePrefab(root, $"{Root}/Board/FieldObject_{type}.prefab").GetComponent<FieldObjectView>();
        }

        static PickupView BuildPickup(PickupType type, Material palette, int layer)
        {
            var root = new GameObject($"Pickup_{type}");
            root.layer = Mathf.Max(0, layer);
            var model = WorldPrefabModels.InstantiateModel($"Props/Pickup_{type}.fbx", root.transform, "Model", PrimitiveType.Sphere, new Vector3(0.4f, 0.4f, 0.4f), layer, out _);
            var renderers = new List<Renderer>();
            WorldPrefabModels.ApplyMaterial(model, palette, renderers);
            var emissive = WorldPrefabModels.CollectEmissive(renderers);
            var view = root.AddComponent<PickupView>();
            var so = new SerializedObject(view);
            so.FindProperty("type").intValue = (int)type;
            so.FindProperty("model").objectReferenceValue = model.transform;
            SerializedPropertyWriter.Write(so.FindProperty("emissiveRenderers"), emissive);
            so.ApplyModifiedPropertiesWithoutUndo();
            return WorldPrefabModels.SavePrefab(root, $"{Root}/Board/Pickup_{type}.prefab").GetComponent<PickupView>();
        }

        #endregion

        #region Definitions

        /// <summary>Fills null prefab / material / icon slots on the config assets (never overwrites designer values).</summary>
        static int FillDefinitions(EnemyView[] enemies, FieldObjectView[] objects, PickupView[] pickups)
        {
            var filled = 0;
            for (var i = 0; i < enemies.Length; i++)
            {
                var type = (EnemyType)i;
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{BuilderAssets.ConfigRoot}/Enemies/Enemy_{type}.asset");
                if (definition == null) continue;
                var so = new SerializedObject(definition);
                if (BuilderAssets.FillIfNull(so, "prefab", enemies[i])) filled++;
                if (so.FindProperty("icon").objectReferenceValue == null)
                {
                    var icon = WorldPrefabModels.LoadSprite($"{WorldPrefabModels.IconRoot}/Enemy_{type}.png");
                    if (icon != null && BuilderAssets.FillIfNull(so, "icon", icon)) filled++;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }

            for (var i = 0; i < SimConstants.BallTypeCount; i++)
            {
                var type = (BallType)i;
                var definition = AssetDatabase.LoadAssetAtPath<BallDefinition>($"{BuilderAssets.ConfigRoot}/Balls/Ball_{type}.asset");
                if (definition == null) continue;
                var so = new SerializedObject(definition);
                var material = WorldPrefabModels.Ball(type);
                if (material != null && BuilderAssets.FillIfNull(so, "material", material)) filled++;
                if (so.FindProperty("icon").objectReferenceValue == null)
                {
                    var icon = WorldPrefabModels.LoadSprite($"{WorldPrefabModels.IconRoot}/Ball_{type}.png");
                    if (icon != null && BuilderAssets.FillIfNull(so, "icon", icon)) filled++;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<FieldObjectCatalog>($"{BuilderAssets.ConfigRoot}/FieldObjectCatalog.asset");
            if (catalog == null) return filled;
            var catalogSo = new SerializedObject(catalog);
            var objectDict = catalogSo.FindProperty("objectPrefabs");
            for (var i = 0; i < objects.Length; i++)
            {
                var slot = EnumDictionaryEditorUtils.GetValueProperty(objectDict, i);
                if (slot.objectReferenceValue != null) continue;
                slot.objectReferenceValue = objects[i];
                filled++;
            }

            var pickupDict = catalogSo.FindProperty("pickupPrefabs");
            for (var i = 0; i < pickups.Length; i++)
            {
                var slot = EnumDictionaryEditorUtils.GetValueProperty(pickupDict, i);
                if (slot.objectReferenceValue != null) continue;
                slot.objectReferenceValue = pickups[i];
                filled++;
            }

            catalogSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return filled;
        }

        #endregion
    }
}
