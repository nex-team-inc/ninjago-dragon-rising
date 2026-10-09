#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Per-renderer material state of an enemy through MaterialPropertyBlocks (TDD §16): hit flash, status tint
    /// (burn / poison / freeze), emissive pulse on the builder-tagged emissive parts, and the shield face marker
    /// (plus the Crystal Golem's rotating ShieldCrystal part). Local model space: +Z is the sim "Bottom" face.
    /// </summary>
    public sealed class EnemyStatusVisuals : MonoBehaviour
    {
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int StatusTintId = Shader.PropertyToID("_StatusTint");
        static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Renderers (wired by WorldPrefabsBuilder)")]
        [SerializeField] Renderer[] renderers = null!;
        [Tooltip("Subset of renderers whose parts glow (names containing Emissive, Gem, Orb, Flame, Crystal, Fuse, Eyes).")]
        [SerializeField] Renderer[] emissiveRenderers = null!;

        [Header("Shield")]
        [Tooltip("Thin glowing slab shown on the shielded face.")]
        [SerializeField] Transform shieldMarker = null!;
        [SerializeField] Renderer shieldMarkerRenderer = null!;
        [Tooltip("Optional model part that turns toward the shielded face (ShieldCrystal).")]
        [SerializeField] Transform? rotatingShieldPart;

        JuiceConfig juice = null!;
        MaterialPropertyBlock block = null!;
        MaterialPropertyBlock emissiveBlock = null!;
        float flashLevel;
        float flashSpeed;
        Color flashColor = Color.white;
        Color tint = Color.clear;
        float pulsePhase;
        float footprintWidth = 1f;
        float footprintHeight = 1f;
        Face shieldFace = Face.None;
        float shieldTurnT = 1f;
        Quaternion shieldFrom = Quaternion.identity;
        Quaternion shieldTo = Quaternion.identity;
        bool dirty = true;

        #region Life Cycle

        public void Initialize(JuiceConfig aJuice, float width, float height, float cellSize)
        {
            juice = aJuice;
            footprintWidth = width * cellSize;
            footprintHeight = height * cellSize;
            block ??= new MaterialPropertyBlock();
            emissiveBlock ??= new MaterialPropertyBlock();
            flashLevel = 0f;
            tint = Color.clear;
            pulsePhase = Random.value * juice.Emissive.pulsePeriod;
            shieldFace = Face.None;
            shieldTurnT = 1f;
            shieldMarker.gameObject.SetActive(false);
            var markerBlock = block;
            markerBlock.Clear();
            // Below the bloom threshold: the shield plane is a status marker, not a light source.
            markerBlock.SetColor(BaseColorId, juice.EnemyFreezeTint * 0.9f);
            shieldMarkerRenderer.SetPropertyBlock(markerBlock);
            dirty = true;
            Apply();
        }

        void Update()
        {
            var dt = Time.deltaTime;
            if (flashLevel > 0f)
            {
                flashLevel = Mathf.Max(0f, flashLevel - dt * flashSpeed);
                dirty = true;
            }

            if (emissiveRenderers.Length > 0)
            {
                pulsePhase += dt;
                dirty = true;
            }

            if (shieldTurnT < 1f && rotatingShieldPart != null)
            {
                shieldTurnT = Mathf.Min(1f, shieldTurnT + dt * 3f);
                rotatingShieldPart.localRotation = Quaternion.Slerp(shieldFrom, shieldTo, Easing.OutBack(shieldTurnT));
            }

            if (dirty) Apply();
        }

        #endregion

        #region Public Methods

        public void Flash(float amount, Color color)
        {
            flashColor = color;
            flashLevel = Mathf.Clamp01(amount);
            flashSpeed = 1f / Mathf.Max(0.02f, juice.FlashDuration);
            dirty = true;
        }

        /// <summary>Freeze wins over poison, poison over burn; the tint alpha is the blend amount.</summary>
        public void SetStatus(int burn, int poison, bool frozen)
        {
            var s = juice.StatusVisuals;
            if (frozen)
            {
                tint = juice.EnemyFreezeTint;
                tint.a = s.freezeTintAmount;
            }
            else if (poison > 0)
            {
                tint = s.poisonTint;
                tint.a = s.tintAmount;
            }
            else if (burn > 0)
            {
                tint = s.burnTint;
                tint.a = s.tintAmount;
            }
            else
            {
                tint = Color.clear;
            }

            dirty = true;
        }

        public void SetShield(Face face, bool animate)
        {
            shieldFace = face;
            var visible = face != Face.None;
            shieldMarker.gameObject.SetActive(visible);
            if (!visible) return;
            var s = juice.StatusVisuals;
            var thickness = s.shieldMarkerThickness;
            var y = s.shieldMarkerHeight;
            var halfW = footprintWidth * 0.5f;
            var halfH = footprintHeight * 0.5f;
            switch (face)
            {
                case Face.Bottom:
                    shieldMarker.localPosition = new Vector3(0f, y, halfH);
                    shieldMarker.localScale = new Vector3(footprintWidth, y * 2f, thickness);
                    break;
                case Face.Top:
                    shieldMarker.localPosition = new Vector3(0f, y, -halfH);
                    shieldMarker.localScale = new Vector3(footprintWidth, y * 2f, thickness);
                    break;
                case Face.Left:
                    shieldMarker.localPosition = new Vector3(halfW, y, 0f);
                    shieldMarker.localScale = new Vector3(thickness, y * 2f, footprintHeight);
                    break;
                default:
                    shieldMarker.localPosition = new Vector3(-halfW, y, 0f);
                    shieldMarker.localScale = new Vector3(thickness, y * 2f, footprintHeight);
                    break;
            }

            if (rotatingShieldPart == null) return;
            shieldFrom = rotatingShieldPart.localRotation;
            shieldTo = Quaternion.Euler(0f, FaceYaw(face), 0f);
            shieldTurnT = animate ? 0f : 1f;
            if (!animate) rotatingShieldPart.localRotation = shieldTo;
        }

        public Face ShieldFace => shieldFace;

        #endregion

        #region Helpers

        /// <summary>Local yaw whose +Z points at the face: Bottom 0, Left 90 (+X), Top 180, Right 270.</summary>
        static float FaceYaw(Face face)
        {
            return face switch
            {
                Face.Left => 90f,
                Face.Top => 180f,
                Face.Right => 270f,
                _ => 0f,
            };
        }

        void Apply()
        {
            dirty = false;
            block.Clear();
            block.SetColor(FlashColorId, flashColor);
            block.SetFloat(FlashAmountId, flashLevel);
            block.SetColor(StatusTintId, tint);
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].SetPropertyBlock(block);
            }

            if (emissiveRenderers.Length == 0) return;
            var e = juice.Emissive;
            var pulse = 1f + e.pulseAmount * Mathf.Sin(pulsePhase / e.pulsePeriod * Mathf.PI * 2f);
            emissiveBlock.Clear();
            emissiveBlock.SetColor(FlashColorId, flashColor);
            emissiveBlock.SetFloat(FlashAmountId, flashLevel);
            emissiveBlock.SetColor(StatusTintId, tint);
            emissiveBlock.SetFloat(EmissionStrengthId, e.strength * pulse);
            for (var i = 0; i < emissiveRenderers.Length; i++)
            {
                emissiveRenderers[i].SetPropertyBlock(emissiveBlock);
            }
        }

        #endregion
    }
}
