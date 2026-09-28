#nullable enable

using System;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Pooled in-flight ball: Ball.fbx mesh with the ball type's material (or a base/emission tint through a
    /// MaterialPropertyBlock), a glow trail and spin from the velocity. Positions come from BallSimulator snapshots
    /// every frame; the frame stamp lets BoardViews release balls the simulator no longer reports.
    /// </summary>
    public sealed class BallView : MonoBehaviour, IPoolableObject
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public event Action<Component>? OnRelease;

        [Header("Wiring (WorldPrefabsBuilder)")]
        [SerializeField] Transform mesh = null!;
        [SerializeField] MeshRenderer meshRenderer = null!;
        [SerializeField] TrailRenderer trail = null!;

        MaterialPropertyBlock block = null!;
        Material? baseMaterial;
        float spinPerUnit;
        Vector3 lastPosition;

        public int Id { get; private set; }
        public int FrameStamp { get; private set; }
        public bool IsMini { get; private set; }
        public Vector3 Velocity { get; private set; }

        public void Spawn(int id, BallDefinition definition, bool isMini, float worldRadius, Vector3 world, JuiceConfig.BallSettings settings)
        {
            Id = id;
            IsMini = isMini;
            spinPerUnit = settings.spinPerUnit;
            block ??= new MaterialPropertyBlock();
            if (baseMaterial == null) baseMaterial = meshRenderer.sharedMaterial;
            var material = definition.Material;
            meshRenderer.sharedMaterial = material != null ? material : baseMaterial;
            block.Clear();
            block.SetColor(BaseColorId, definition.Color);
            block.SetColor(EmissionColorId, definition.GlowColor);
            meshRenderer.SetPropertyBlock(block);
            // Ball.fbx is a unit-diameter sphere (radius 0.5), TDD §14.1.
            var diameter = worldRadius * 2f;
            transform.localScale = Vector3.one;
            mesh.localScale = new Vector3(diameter, diameter, diameter);
            mesh.localRotation = Quaternion.identity;
            transform.position = world;
            lastPosition = world;
            var glow = definition.GlowColor;
            var trailColor = new Color(Mathf.Min(1f, glow.r), Mathf.Min(1f, glow.g), Mathf.Min(1f, glow.b), 0.9f);
            trail.startColor = trailColor;
            trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            trail.startWidth = diameter * settings.trailWidthScale;
            trail.endWidth = 0f;
            trail.time = settings.trailTime;
            trail.Clear();
            trail.emitting = true;
        }

        /// <summary>Applies the simulator snapshot for this frame (world position and velocity).</summary>
        public void Sync(Vector3 world, Vector3 worldVelocity, int frameStamp)
        {
            FrameStamp = frameStamp;
            Velocity = worldVelocity;
            transform.position = world;
            var delta = world - lastPosition;
            lastPosition = world;
            var distance = delta.magnitude;
            if (distance < 0.0001f) return;
            var axis = Vector3.Cross(Vector3.up, delta / distance);
            mesh.Rotate(axis, distance * spinPerUnit, Space.World);
        }

        public void Release()
        {
            trail.emitting = false;
            trail.Clear();
            OnRelease?.Invoke(this);
        }
    }
}
