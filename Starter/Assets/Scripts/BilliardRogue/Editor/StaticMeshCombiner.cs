#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Build-time mesh combining for the static parts of a prefab (arena kit, act dioramas): every BatchingStatic
    /// MeshRenderer with a single material is merged with the others that share its group key, material and shadow
    /// settings into one renderer under the root, and the merged mesh is saved as an asset (stable path = stable GUID).
    /// Unity's own static batching still leaves one draw per object in the sorted queue when materials interleave
    /// (the arena alone rendered ~250 draws + shadows); one renderer per material is one draw, whatever the sorting.
    /// The source renderers are disabled and lose their static flag (they stay in the prefab so part lookups and the
    /// nested model links keep working); animated parts are never static, so they are untouched.
    /// </summary>
    public static class StaticMeshCombiner
    {
        public sealed class Result
        {
            public int sourceRenderers;
            public int combinedRenderers;
            public int skipped;
            public readonly Dictionary<string, MeshRenderer> byKey = new();
            public override string ToString() => $"{sourceRenderers} renderers → {combinedRenderers} combined ({skipped} kept)";
        }

        const int MaxUInt16Vertices = 65000;

        #region Public Methods

        /// <summary>
        /// Combines under root. groupKey returns the logical group of a renderer (null = leave it alone); the final
        /// group also separates materials and shadow modes. Meshes go to meshFolder as "{prefix}_{group}.asset".
        /// </summary>
        public static Result Combine(GameObject root, string meshFolder, string prefix, Func<MeshRenderer, string?> groupKey)
        {
            BuilderAssets.EnsureFolder(meshFolder);
            var result = new Result();
            var groups = new Dictionary<string, List<MeshRenderer>>();
            var order = new List<string>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var material = renderer.sharedMaterial;
                var key = groupKey(renderer);
                if (key == null || filter == null || filter.sharedMesh == null || material == null || renderer.sharedMaterials.Length != 1
                    || filter.sharedMesh.subMeshCount != 1 || !GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.BatchingStatic))
                {
                    result.skipped++;
                    continue;
                }

                var full = $"{key}|{material.name}|{renderer.shadowCastingMode}|{(renderer.receiveShadows ? "r" : "n")}";
                if (!groups.TryGetValue(full, out var list))
                {
                    groups[full] = list = new List<MeshRenderer>();
                    order.Add(full);
                }

                list.Add(renderer);
                result.sourceRenderers++;
            }

            var worldToRoot = root.transform.worldToLocalMatrix;
            foreach (var full in order)
            {
                var sources = groups[full];
                var first = sources[0];
                var instances = new List<CombineInstance>(sources.Count);
                var vertices = 0;
                foreach (var source in sources)
                {
                    var mesh = source.GetComponent<MeshFilter>().sharedMesh;
                    vertices += mesh.vertexCount;
                    instances.Add(new CombineInstance { mesh = mesh, subMeshIndex = 0, transform = worldToRoot * source.transform.localToWorldMatrix });
                }

                var groupName = SafeName(full);
                var mesh32 = LoadOrCreateMesh($"{meshFolder}/{prefix}_{groupName}.asset", $"{prefix}_{groupName}");
                mesh32.Clear(false);
                mesh32.indexFormat = vertices > MaxUInt16Vertices ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh32.CombineMeshes(instances.ToArray(), true, true, false);
                mesh32.RecalculateBounds();
                mesh32.UploadMeshData(false);
                EditorUtility.SetDirty(mesh32);

                var go = new GameObject($"Combined_{groupName}");
                go.transform.SetParent(root.transform, false);
                go.layer = first.gameObject.layer;
                go.AddComponent<MeshFilter>().sharedMesh = mesh32;
                var combined = go.AddComponent<MeshRenderer>();
                combined.sharedMaterial = first.sharedMaterial;
                combined.shadowCastingMode = first.shadowCastingMode;
                combined.receiveShadows = first.receiveShadows;
                combined.lightProbeUsage = LightProbeUsage.BlendProbes;
                combined.reflectionProbeUsage = ReflectionProbeUsage.Off;
                combined.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                result.byKey[full] = combined;
                result.combinedRenderers++;

                foreach (var source in sources)
                {
                    source.enabled = false;
                    GameObjectUtility.SetStaticEditorFlags(source.gameObject, 0);
                }
            }

            return result;
        }

        /// <summary>Combined renderer of the logical group + material, if one was produced (first match wins).</summary>
        public static MeshRenderer? Find(Result result, string key, Material material)
        {
            var start = $"{key}|{material.name}|";
            foreach (var pair in result.byKey)
            {
                if (pair.Key.StartsWith(start, StringComparison.Ordinal)) return pair.Value;
            }

            return null;
        }

        /// <summary>Every combined renderer whose logical group is key (across materials / shadow modes).</summary>
        public static List<MeshRenderer> FindAll(Result result, string key)
        {
            var found = new List<MeshRenderer>();
            var start = key + "|";
            foreach (var pair in result.byKey)
            {
                if (pair.Key.StartsWith(start, StringComparison.Ordinal)) found.Add(pair.Value);
            }

            return found;
        }

        #endregion

        #region Helpers

        static Mesh LoadOrCreateMesh(string path, string name)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static string SafeName(string key)
        {
            var sb = new StringBuilder(key.Length);
            foreach (var c in key)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            return sb.ToString();
        }

        #endregion
    }
}
