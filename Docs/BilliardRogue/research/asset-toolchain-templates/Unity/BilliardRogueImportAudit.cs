#nullable enable

using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Logs what Unity actually imported for generated Billiard Rogue content (transforms, vertex
    /// attributes, texture formats, audio settings). Editor-only verification; LINQ is fine here.
    /// </summary>
    public static class BilliardRogueImportAudit
    {
        [MenuItem("Nex/Billiard Rogue/Log Import Audit")]
        public static void Run()
        {
            var log = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models/BilliardRogue" }))
                LogModel(AssetDatabase.GUIDToAssetPath(guid), log);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Textures/BilliardRogue", "Assets/Sprites/BilliardRogue" }))
                LogTexture(AssetDatabase.GUIDToAssetPath(guid), log);
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/Sfx/BilliardRogue", "Assets/Audio/Bgm/BilliardRogue" }))
                LogAudio(AssetDatabase.GUIDToAssetPath(guid), log);
            Debug.Log($"[ImportAudit] target={EditorUserBuildSettings.activeBuildTarget}\n{log}");
        }

        static void LogModel(string path, StringBuilder log)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            log.AppendLine($"MODEL {path}");
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var line = $"  {t.name} pos={t.localPosition:F3} rot={t.localEulerAngles:F1} scale={t.localScale:F3}";
                var filter = t.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    var mesh = filter.sharedMesh;
                    line += $" verts={mesh.vertexCount} tris={mesh.triangles.Length / 3} size={mesh.bounds.size:F3}"
                        + $" color={mesh.HasVertexAttribute(VertexAttribute.Color)} uv1={mesh.HasVertexAttribute(VertexAttribute.TexCoord1)}"
                        + $" tangent={mesh.HasVertexAttribute(VertexAttribute.Tangent)}";
                }
                log.AppendLine(line);
            }
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                log.AppendLine($"  CLIP {clip.name} {clip.length:F2}s curves={AnimationUtility.GetCurveBindings(clip).Length}");
        }

        static void LogTexture(string path, StringBuilder log)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var android = importer.GetPlatformTextureSettings("Android");
            log.AppendLine($"TEX {path} {importer.textureType} {texture.width}x{texture.height} fmt={texture.format} mips={texture.mipmapCount}"
                + $" filter={texture.filterMode} wrap={texture.wrapMode} android={android.format} ppu={importer.spritePixelsPerUnit}");
        }

        static void LogAudio(string path, StringBuilder log)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var settings = importer.defaultSampleSettings;
            log.AppendLine($"AUDIO {path} {clip.length:F2}s ch={clip.channels} hz={clip.frequency} load={settings.loadType}"
                + $" fmt={settings.compressionFormat} q={settings.quality} mono={importer.forceToMono} bg={importer.loadInBackground}");
        }
    }
}
