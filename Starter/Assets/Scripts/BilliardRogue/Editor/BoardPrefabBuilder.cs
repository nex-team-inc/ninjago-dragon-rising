#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;
using Nex.Util;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds the board-level prefabs for WorldPrefabsBuilder: the cat knight (model, cue, pennant, palettes), the
    /// UI label prefabs (WorldLabel, DamageNumber, WorldLabelLayer), the cat arms layer and World/BoardPresenter.prefab
    /// with its pools, aim guide, two cats and camera shaker, wiring every serialized reference.
    /// </summary>
    public static class BoardPrefabBuilder
    {
        const string Root = WorldPrefabModels.PrefabRoot;
        const string CatPath = Root + "/Player/Cat_Hero.prefab";
        const string LabelPath = Root + "/Board/WorldLabel.prefab";
        const string NumberPath = Root + "/Board/DamageNumber.prefab";
        const string LabelLayerPath = Root + "/Board/WorldLabelLayer.prefab";
        public const string CatArmsPath = Root + "/Board/CatArmsLayer.prefab";
        const string UiSprites = "Assets/Sprites/BilliardRogue/UI/";
        // Cat arm sprites (make_arms.py: sleeve 24 px wide, paw 36x40 px) at 2x canvas units per sprite pixel.
        const float ArmScale = 2f;
        const string PresenterPath = Root + "/World/BoardPresenter.prefab";
        const int BounceMarkers = 8;

        #region Cat

        public static CatView BuildCat(Material paletteP1, Material paletteP2, Material glow, int layer)
        {
            var root = WorldPrefabModels.NewRoot("Cat_Hero");
            root.layer = Mathf.Max(0, layer);
            var model = WorldPrefabModels.InstantiateModel("Player/Cat_Hero.fbx", root.transform, "Model", PrimitiveType.Capsule, new Vector3(0.5f, 0.9f, 0.5f), layer, out _);
            var renderers = new List<Renderer>();
            WorldPrefabModels.ApplyMaterial(model, paletteP1, renderers);
            WorldPrefabModels.CastShadowsFrom(renderers, WorldPrefabModels.FindFirstPart(model.transform, "Body"));
            var modelTransform = model.transform;
            var pawR = WorldPrefabModels.FindPart(modelTransform, "PawR");
            // The cue hangs from the FBX root (merged Body) so it follows the body pose but not the container tweens.
            var cue = WorldPrefabModels.InstantiateModel("Player/Cue_Stick.fbx", WorldPrefabModels.FbxRoot(model), "Cue", PrimitiveType.Cylinder, new Vector3(0.06f, 0.6f, 0.06f), layer, out var cuePlaceholder);
            var cueRenderers = new List<Renderer>();
            WorldPrefabModels.ApplyMaterial(cue, paletteP1, cueRenderers);
            foreach (var renderer in cueRenderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            cue.transform.localPosition = pawR != null ? pawR.localPosition + new Vector3(0f, 0f, -0.15f) : new Vector3(-0.14f, 0.385f, -0.15f);
            if (cuePlaceholder)
            {
                // A cylinder primitive stands along Y; the cue must point along +Z (TDD §14.1).
                var placeholder = WorldPrefabModels.FbxRoot(cue);
                placeholder.localRotation = Quaternion.Euler(90f, 0f, 0f);
                placeholder.localPosition = new Vector3(0f, 0f, 0.6f);
            }

            var bounds = WorldPrefabModels.RendererBounds(model);
            var pennant = WorldPrefabModels.CreatePrimitive(root.transform, "Pennant", PrimitiveType.Cube, new Vector3(0f, bounds.max.y + 0.22f, 0f), new Vector3(0.16f, 0.1f, 0.02f), glow, layer);
            var view = root.AddComponent<CatView>();
            var so = new SerializedObject(view);
            so.FindProperty("model").objectReferenceValue = modelTransform;
            so.FindProperty("cue").objectReferenceValue = cue.transform;
            so.FindProperty("pennant").objectReferenceValue = pennant.transform;
            so.FindProperty("pennantRenderer").objectReferenceValue = pennant.GetComponent<Renderer>();
            renderers.AddRange(cueRenderers);
            SerializedPropertyWriter.Write(so.FindProperty("paletteRenderers"), renderers);
            so.FindProperty("paletteP1").objectReferenceValue = paletteP1;
            so.FindProperty("paletteP2").objectReferenceValue = paletteP2;
            so.FindProperty("head").objectReferenceValue = WorldPrefabModels.FindPart(modelTransform, "Head");
            so.FindProperty("tail").objectReferenceValue = WorldPrefabModels.FindPart(modelTransform, "Tail");
            so.FindProperty("earL").objectReferenceValue = WorldPrefabModels.FindPart(modelTransform, "EarL");
            so.FindProperty("earR").objectReferenceValue = WorldPrefabModels.FindPart(modelTransform, "EarR");
            so.FindProperty("pawR").objectReferenceValue = pawR;
            so.FindProperty("labelHeight").floatValue = Mathf.Clamp(bounds.max.y + 0.3f, 0.5f, 3f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return WorldPrefabModels.SavePrefab(root, CatPath).GetComponent<CatView>();
        }

        #endregion

        #region Labels

        public static void BuildLabelPrefabs()
        {
            var font = WorldPrefabModels.LoadFont("BilliardPixelBold_TMP");
            if (font == null) font = WorldPrefabModels.LoadFont("BilliardPixel_TMP");
            if (font == null) WorldPrefabModels.Warn("BilliardPixel TMP font assets missing (FontAssetsBuilder); labels use the TMP default font.");

            var label = NewRect("WorldLabel", null, new Vector2(160f, 48f));
            var labelView = label.AddComponent<WorldLabel>();
            // Dark rounded pill under the number (built-in 9-sliced rounded rect; WorldLabel sizes and tints it).
            var pill = NewImage("HpPill", label.transform, Vector2.zero, new Vector2(56f, 40f));
            pill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            pill.type = Image.Type.Sliced;
            pill.preserveAspect = false;
            pill.color = new Color(0.04f, 0.04f, 0.07f, 0.86f);
            var hp = NewText("Hp", label.transform, font, 32f, new Vector2(0f, 0f), new Vector2(160f, 40f));
            var icons = new List<Image>();
            var statuses = EnumDictionary<StatusType, Image>.allKeys;
            for (var i = 0; i < statuses.Length; i++)
            {
                var image = NewImage($"Status_{statuses[i]}", label.transform, new Vector2((i - 1) * 30f, -30f), new Vector2(28f, 28f));
                image.sprite = WorldPrefabModels.LoadSprite($"{WorldPrefabModels.IconRoot}/Status_{statuses[i]}.png");
                image.enabled = false;
                icons.Add(image);
            }

            var telegraph = NewImage("Telegraph", label.transform, new Vector2(0f, 42f), new Vector2(40f, 40f));
            telegraph.enabled = false;
            var labelSo = new SerializedObject(labelView);
            labelSo.FindProperty("rect").objectReferenceValue = label.GetComponent<RectTransform>();
            labelSo.FindProperty("hpText").objectReferenceValue = hp;
            labelSo.FindProperty("hpPill").objectReferenceValue = pill;
            WriteEnumDictionary(labelSo.FindProperty("statusIcons"), icons);
            labelSo.FindProperty("telegraphIcon").objectReferenceValue = telegraph;
            labelSo.ApplyModifiedPropertiesWithoutUndo();
            WorldPrefabModels.SavePrefab(label, LabelPath);

            var number = NewRect("DamageNumber", null, new Vector2(200f, 60f));
            var numberView = number.AddComponent<DamageNumber>();
            // The outline font's glyphs are the text's dilated by 1 px on the same metrics: created first, it draws under
            // the text as its dark edge (DamageNumber drops it one font pixel for depth).
            var outlineFont = WorldPrefabModels.LoadFont("BilliardPixelBoldOutline_TMP");
            if (outlineFont == null) WorldPrefabModels.Warn("BilliardPixelBoldOutline_TMP missing (FontAssetsBuilder); damage numbers draw their outline with the text font.");
            var outline = NewText("Outline", number.transform, outlineFont != null ? outlineFont : font, 32f, Vector2.zero, new Vector2(200f, 60f));
            outline.color = new Color(0.05f, 0.04f, 0.1f, 1f);
            var text = NewText("Text", number.transform, font, 32f, Vector2.zero, new Vector2(200f, 60f));
            var numberSo = new SerializedObject(numberView);
            numberSo.FindProperty("rect").objectReferenceValue = number.GetComponent<RectTransform>();
            numberSo.FindProperty("text").objectReferenceValue = text;
            numberSo.FindProperty("outline").objectReferenceValue = outline;
            numberSo.ApplyModifiedPropertiesWithoutUndo();
            WorldPrefabModels.SavePrefab(number, NumberPath);

            var layer = NewRect("WorldLabelLayer", null, Vector2.zero);
            var layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;
            // Every label moves each frame: an own canvas keeps those rebuilds off the GameplayView canvas.
            layer.AddComponent<Canvas>().additionalShaderChannels = (AdditionalCanvasShaderChannels)27;
            var labelPool = layer.AddComponent<WorldLabelPool>();
            var numberPool = layer.AddComponent<DamageNumberPool>();
            var layerView = layer.AddComponent<WorldLabelLayer>();
            var telegraphNames = new[] { "Telegraph_Spawn", "Telegraph_Cast", "Telegraph_Heal", "Telegraph_Quake" };
            var telegraphIcons = new List<Sprite?>();
            foreach (var name in telegraphNames)
            {
                telegraphIcons.Add(WorldPrefabModels.LoadSprite($"{WorldPrefabModels.IconRoot}/{name}.png"));
            }

            var layerSo = new SerializedObject(layerView);
            layerSo.FindProperty("rect").objectReferenceValue = layerRect;
            layerSo.FindProperty("labelPool").objectReferenceValue = labelPool;
            layerSo.FindProperty("numberPool").objectReferenceValue = numberPool;
            SerializedPropertyWriter.Write(layerSo.FindProperty("telegraphIcons"), telegraphIcons);
            layerSo.ApplyModifiedPropertiesWithoutUndo();
            WorldPrefabModels.SavePrefab(layer, LabelLayerPath);
            BuildCatArms();
        }

        /// <summary>
        /// Cat arms layer (GDD v2 §19): a full-stretch canvas with, per player, a ball arm (open paw) and a cue arm (fist)
        /// in P1 orange / P2 charcoal; the ball arms draw first, so a cue fist sits over a crossing ball arm.
        /// </summary>
        public static void BuildCatArms()
        {
            var layer = NewRect("CatArmsLayer", null, Vector2.zero);
            var layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;
            // The arms move every frame: an own canvas keeps those rebuilds off the GameplayView canvas.
            layer.AddComponent<Canvas>();
            var ballArms = new List<PawArm>();
            var cueArms = new List<PawArm>();
            for (var p = 1; p <= 2; p++)
            {
                ballArms.Add(BuildArm(layer.transform, $"BallArm_P{p}", $"Arm_P{p}.png", $"Paw_P{p}_Open.png"));
            }

            for (var p = 1; p <= 2; p++)
            {
                cueArms.Add(BuildArm(layer.transform, $"CueArm_P{p}", $"Arm_P{p}.png", $"Paw_P{p}_Grab.png"));
            }

            var view = layer.AddComponent<CatArmsLayer>();
            var so = new SerializedObject(view);
            so.FindProperty("rect").objectReferenceValue = layerRect;
            SerializedPropertyWriter.Write(so.FindProperty("ballArms"), ballArms);
            SerializedPropertyWriter.Write(so.FindProperty("cueArms"), cueArms);
            so.ApplyModifiedPropertiesWithoutUndo();
            WorldPrefabModels.SavePrefab(layer, CatArmsPath);
        }

        /// <summary>Shoulder (pivot, rotated) → tiled fur sleeve (bottom pivot) + paw (palm-centre pivot) at the sleeve end.</summary>
        static PawArm BuildArm(Transform parent, string name, string sleeveFile, string pawFile)
        {
            var go = NewRect(name, parent, new Vector2(24f * ArmScale, 24f * ArmScale));
            var sleeveGo = NewRect("Sleeve", go.transform, new Vector2(24f * ArmScale, 200f));
            var sleeveRect = sleeveGo.GetComponent<RectTransform>();
            sleeveRect.pivot = new Vector2(0.5f, 0f);
            var sleeve = sleeveGo.AddComponent<Image>();
            sleeve.sprite = WorldPrefabModels.LoadSprite(UiSprites + sleeveFile);
            sleeve.type = Image.Type.Tiled;
            // UI sprites import at 3x (PPU 100/3); 1.5 tiles them at 2x.
            sleeve.pixelsPerUnitMultiplier = 3f / ArmScale;
            sleeve.raycastTarget = false;
            var pawGo = NewRect("Paw", go.transform, new Vector2(36f * ArmScale, 40f * ArmScale));
            pawGo.transform.localPosition = new Vector3(0f, 200f, 0f);
            var paw = pawGo.AddComponent<Image>();
            paw.sprite = WorldPrefabModels.LoadSprite(UiSprites + pawFile);
            paw.raycastTarget = false;
            var arm = go.AddComponent<PawArm>();
            var so = new SerializedObject(arm);
            so.FindProperty("shoulder").objectReferenceValue = go.transform;
            so.FindProperty("sleeve").objectReferenceValue = sleeveRect;
            so.FindProperty("sleeveImage").objectReferenceValue = sleeve;
            so.FindProperty("paw").objectReferenceValue = pawGo.transform;
            so.FindProperty("pawImage").objectReferenceValue = paw;
            so.FindProperty("minLength").floatValue = 40f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return arm;
        }

        static GameObject NewRect(string name, Transform? parent, Vector2 size)
        {
            var go = WorldPrefabModels.NewRoot(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.GetComponent<RectTransform>();
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return go;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset? font, float size, Vector2 position, Vector2 rectSize)
        {
            var go = NewRect(name, parent, rectSize);
            go.GetComponent<RectTransform>().anchoredPosition = position;
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = "0";
            return text;
        }

        static Image NewImage(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = NewRect(name, parent, size);
            go.GetComponent<RectTransform>().anchoredPosition = position;
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        #endregion

        #region Board presenter

        public static void BuildBoardPresenter(BallView ballPrefab, CatView catPrefab, Material glow, int layer)
        {
            var root = WorldPrefabModels.NewRoot("BoardPresenter");
            root.layer = Mathf.Max(0, layer);
            var pools = new GameObject("Pools");
            pools.transform.SetParent(root.transform, false);
            var enemyPools = new List<EnemyViewPool>();
            for (var i = 0; i < SimConstants.EnemyTypeCount; i++)
            {
                enemyPools.Add(pools.AddComponent<EnemyViewPool>());
            }

            var ballPool = pools.AddComponent<BallViewPool>();
            var objectPools = new List<FieldObjectViewPool>();
            for (var i = 0; i < SimConstants.FieldObjectTypeCount; i++)
            {
                objectPools.Add(pools.AddComponent<FieldObjectViewPool>());
            }

            var pickupPools = new List<PickupViewPool>();
            for (var i = 0; i < SimConstants.PickupTypeCount; i++)
            {
                pickupPools.Add(pools.AddComponent<PickupViewPool>());
            }

            var aimGuide = BuildAimGuide(root.transform, glow, layer);
            var aura = BuildHypeAura(root.transform, glow, layer);
            var cats = new List<CatView>();
            for (var i = 0; i < 2; i++)
            {
                var cat = (GameObject)PrefabUtility.InstantiatePrefab(catPrefab.gameObject, root.transform);
                cat.name = $"Cat_P{i + 1}";
                cats.Add(cat.GetComponent<CatView>());
            }

            var shaker = root.AddComponent<CameraShaker>();
            var presenter = root.AddComponent<BoardPresenter>();
            var so = new SerializedObject(presenter);
            WriteEnumDictionary(so.FindProperty("enemyPools"), enemyPools);
            so.FindProperty("ballPool").objectReferenceValue = ballPool;
            so.FindProperty("ballPrefab").objectReferenceValue = ballPrefab;
            WriteEnumDictionary(so.FindProperty("objectPools"), objectPools);
            WriteEnumDictionary(so.FindProperty("pickupPools"), pickupPools);
            so.FindProperty("aimGuide").objectReferenceValue = aimGuide;
            SerializedPropertyWriter.Write(so.FindProperty("cats"), cats);
            so.FindProperty("cameraShaker").objectReferenceValue = shaker;
            so.FindProperty("hypeAura").objectReferenceValue = aura;
            so.FindProperty("labelLayerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WorldLabelLayer>(LabelLayerPath);
            so.FindProperty("labelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WorldLabel>(LabelPath);
            so.FindProperty("damageNumberPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DamageNumber>(NumberPath);
            so.FindProperty("catArmsPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CatArmsLayer>(CatArmsPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            WorldPrefabModels.SavePrefab(root, PresenterPath);
        }

        /// <summary>Tier-3 Hype rim aura: one looped additive line (M_BallTrail); HypeAuraView places it on the arena rim.</summary>
        static HypeAuraView BuildHypeAura(Transform parent, Material glow, int layer)
        {
            var go = new GameObject("HypeAura");
            go.transform.SetParent(parent, false);
            go.layer = Mathf.Max(0, layer);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = WorldPrefabModels.BallTrail(glow);
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 0;
            line.widthMultiplier = 0.2f;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            var view = go.AddComponent<HypeAuraView>();
            var so = new SerializedObject(view);
            so.FindProperty("line").objectReferenceValue = line;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        static AimGuideView BuildAimGuide(Transform parent, Material glow, int layer)
        {
            var go = new GameObject("AimGuide");
            go.transform.SetParent(parent, false);
            go.layer = Mathf.Max(0, layer);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = WorldPrefabModels.AimGuide();
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.widthMultiplier = 0.09f;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            // The waiting ball at the cue: a real lit ball (M_Ball_Basic + emission), not an additive glow disc.
            var ghostMaterial = WorldPrefabModels.Ball(BallType.Basic);
            var ghost = WorldPrefabModels.CreatePrimitive(go.transform, "GhostBall", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.4f, ghostMaterial != null ? ghostMaterial : glow, layer);
            // Guides are overlays: no shadow from the ghost ball or the bounce markers.
            ghost.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var markers = new List<Transform>();
            var markerRenderers = new List<Renderer>();
            for (var i = 0; i < BounceMarkers; i++)
            {
                var marker = WorldPrefabModels.CreatePrimitive(go.transform, $"Marker{i}", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.16f, glow, layer);
                marker.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                marker.SetActive(false);
                markers.Add(marker.transform);
                markerRenderers.Add(marker.GetComponent<Renderer>());
            }

            var view = go.AddComponent<AimGuideView>();
            var so = new SerializedObject(view);
            so.FindProperty("line").objectReferenceValue = line;
            so.FindProperty("ghostBall").objectReferenceValue = ghost.transform;
            so.FindProperty("ghostRenderer").objectReferenceValue = ghost.GetComponent<Renderer>();
            SerializedPropertyWriter.Write(so.FindProperty("bounceMarkers"), markers);
            SerializedPropertyWriter.Write(so.FindProperty("bounceMarkerRenderers"), markerRenderers);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        #endregion

        /// <summary>Fills an EnumDictionary property whose keys are 0..n-1 (the builders create one value per enum key in order).</summary>
        static void WriteEnumDictionary<T>(SerializedProperty dictionary, List<T> values) where T : UnityEngine.Object
        {
            for (var i = 0; i < values.Count; i++)
            {
                EnumDictionaryEditorUtils.GetValueProperty(dictionary, i).objectReferenceValue = values[i];
            }
        }
    }
}
