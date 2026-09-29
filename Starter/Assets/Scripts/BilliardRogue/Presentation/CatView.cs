#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// One player's cat knight on the launch line: walks to the launch X, idles in a 3/4 view toward the camera,
    /// turns up the arena to strike with the cue, flinches when hurt, dances on victory and slumps on defeat.
    /// While balls fly it dances with Hype (GDD v2 §3) and strikes a big pose when a Hype tier is reached.
    /// Rigid parts (Cat_Hero.fbx: Body, Head, EarL/R, Tail, PawL/R, Cape) are animated by code; P1/P2 palettes
    /// swap the model material (M_Palette / M_Palette_CatP2). JuiceConfig.cat.visible off hides every renderer and
    /// skips the pose animation; the cat still walks, so its anchors keep marking the player's spot.
    /// </summary>
    public sealed class CatView : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Wiring (WorldPrefabsBuilder)")]
        [SerializeField] Transform model = null!;
        [SerializeField] Transform cue = null!;
        [SerializeField] Transform pennant = null!;
        [SerializeField] Renderer pennantRenderer = null!;
        [SerializeField] Renderer[] paletteRenderers = System.Array.Empty<Renderer>();
        [SerializeField] Material? paletteP1;
        [SerializeField] Material? paletteP2;
        [SerializeField] Transform? head;
        [SerializeField] Transform? tail;
        [SerializeField] Transform? earL;
        [SerializeField] Transform? earR;
        [SerializeField] Transform? pawR;
        [Tooltip("Height of the hurt number / heal sparkle anchor, in cells.")]
        [SerializeField, Range(0.2f, 3f)] float labelHeight = 1.15f;

        ArenaLayout layout = null!;
        JuiceConfig.CatSettings settings = null!;
        JuiceConfig.HypeSettings hypeSettings = null!;
        MaterialPropertyBlock block = null!;
        Renderer[] renderers = System.Array.Empty<Renderer>();
        bool visible = true;
        int playerIndex;
        float currentX01 = 0.5f;
        float targetX01 = 0.5f;
        float phase;
        float earTimer;
        float strikeT = 1f;
        float hurtT = 1f;
        float victoryT = 1f;
        float poseT = 1f;
        float poseStrength;
        float hype;
        float dancePhase;
        float tailPhase;
        bool defeated;
        bool active;
        Vector3 cueBaseLocal;
        Quaternion cueBaseRotation;

        public int PlayerIndex => playerIndex;
        /// <summary>JuiceConfig.cat.visible: effects that decorate the cat's poses skip a hidden cat.</summary>
        public bool IsVisible => visible;
        public Vector3 Position { get; private set; }
        public Vector3 LabelAnchor => Position + Vector3.up * (labelHeight * layout.CellSize * settings.modelScale);
        public Vector3 Center => Position + Vector3.up * (0.5f * layout.CellSize * settings.modelScale);

        #region Life Cycle

        public void Initialize(int aPlayerIndex, ArenaLayout aLayout, JuiceConfig juice)
        {
            playerIndex = aPlayerIndex;
            layout = aLayout;
            settings = juice.Cat;
            hypeSettings = juice.Hype;
            block ??= new MaterialPropertyBlock();
            cueBaseLocal = cue.localPosition;
            cueBaseRotation = cue.localRotation;
            phase = Random.value * 10f;
            var palette = playerIndex == 0 ? paletteP1 : paletteP2;
            if (palette != null)
            {
                for (var i = 0; i < paletteRenderers.Length; i++)
                {
                    paletteRenderers[i].sharedMaterial = palette;
                }
            }

            block.Clear();
            block.SetColor(BaseColorId, juice.PlayerColor(playerIndex) * 2f);
            pennantRenderer.SetPropertyBlock(block);
            currentX01 = targetX01 = playerIndex == 0 ? settings.waitingX01 : 1f - settings.waitingX01;
            defeated = false;
            strikeT = hurtT = victoryT = poseT = 1f;
            hype = 0f;
            if (renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            ApplyVisibility();
            SetActive(false);
            Place();
        }

        void Update()
        {
            // The scene cats exist before any run: BoardPresenter.Initialize wires them at the first GameplayView.
            if (settings == null) return;
            if (settings.visible != visible) ApplyVisibility();
            var dt = Time.deltaTime;
            phase += dt;
            if (!defeated)
            {
                var step = settings.walkSpeed / layout.Rules.columns * dt;
                currentX01 = Mathf.MoveTowards(currentX01, targetX01, step);
            }

            Place();
            if (!visible) return;
            var walking = !Mathf.Approximately(currentX01, targetX01);
            var yaw = settings.idleYaw;
            var cueOffset = Vector3.zero;
            var bounce = 0f;
            var roll = 0f;
            var squash = 1f;
            var dancing = !defeated && hype >= hypeSettings.danceMin;
            if (dancing) dancePhase += dt * Mathf.Lerp(hypeSettings.danceBeatsMin, hypeSettings.danceBeatsMax, hype);
            tailPhase += dt * (dancing ? Mathf.Lerp(1f, hypeSettings.danceTailSpeed, hype) : 1f);
            if (strikeT < 1f)
            {
                strikeT = Mathf.Min(1f, strikeT + dt / settings.strikeDuration);
                yaw = 0f;
                // Pull back, then thrust forward past the rest position.
                var pull = strikeT < 0.4f ? Easing.OutQuad(strikeT / 0.4f) : 1f - Easing.OutQuad((strikeT - 0.4f) / 0.6f) * 1.5f;
                cueOffset = new Vector3(0f, 0f, -settings.cuePullBack * pull * layout.CellSize);
            }
            else if (hurtT < 1f)
            {
                hurtT = Mathf.Min(1f, hurtT + dt / settings.hurtDuration);
                yaw = settings.idleYaw + 25f * Easing.Punch(hurtT);
                bounce = -settings.hurtRecoil * Easing.Punch(hurtT);
            }
            else if (victoryT < 1f)
            {
                victoryT = Mathf.Min(1f, victoryT + dt / 0.6f);
                yaw = 180f + 360f * victoryT;
                bounce = settings.victoryJump * Easing.Arc(victoryT) * layout.CellSize;
                if (victoryT >= 1f) victoryT = 0f;
            }
            else if (poseT < 1f)
            {
                // Tier reached: big jump, full spin and a scale punch (stronger for higher tiers).
                poseT = Mathf.Min(1f, poseT + dt / hypeSettings.tierPoseDuration);
                yaw = settings.idleYaw + 360f * Easing.OutQuad(poseT);
                bounce = hypeSettings.tierPoseJump * poseStrength * Easing.Arc(poseT) * layout.CellSize;
                squash = 1f + hypeSettings.tierPoseScale * poseStrength * Easing.Punch(poseT);
            }
            else if (walking)
            {
                yaw = currentX01 < targetX01 ? 90f : 270f;
                bounce = Mathf.Abs(Mathf.Sin(phase * 14f)) * 0.06f * layout.CellSize;
            }
            else if (dancing)
            {
                // Hop on every beat, sway side to side every two beats, squash on landing.
                var beat = Mathf.Abs(Mathf.Sin(dancePhase * Mathf.PI));
                var sway = Mathf.Sin(dancePhase * Mathf.PI * 0.5f);
                yaw = settings.idleYaw + sway * hypeSettings.danceYaw * hype;
                roll = sway * hypeSettings.danceRoll * hype;
                bounce = beat * hypeSettings.danceBounce * hype * layout.CellSize;
                squash = 1f + (beat - 0.5f) * hypeSettings.danceSquash * hype;
            }

            var lean = defeated ? Quaternion.Euler(35f, 0f, 0f) : Quaternion.identity;
            model.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, roll) * lean;
            model.localPosition = new Vector3(0f, bounce, walking ? 0f : Mathf.Sin(phase * 1.3f) * 0.01f);
            var stretch = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
            model.localScale = (defeated ? new Vector3(1.08f, 0.85f, 1.08f) : stretch) * settings.modelScale;
            cue.localPosition = cueBaseLocal + cueOffset;
            cue.localRotation = cueBaseRotation;
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(tailPhase * settings.tailWagSpeed * Mathf.PI) * settings.tailWagAngle, 0f);
            var nod = dancing ? Mathf.Abs(Mathf.Sin(dancePhase * Mathf.PI)) * 12f * hype : 0f;
            if (head != null) head.localRotation = Quaternion.Euler(nod, Mathf.Sin(phase * 0.7f) * 6f, 0f);
            UpdateEars(dt);
            pennant.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 5f) * 8f);
        }

        #endregion

        #region Public Methods

        /// <summary>Launch X the cat walks to (0 = left wall, 1 = right wall).</summary>
        public void SetLaunchX(float x01)
        {
            targetX01 = Mathf.Clamp01(x01);
        }

        /// <summary>Active shooter: pennant on, stands at the launch X; inactive: waits at the side.</summary>
        public void SetActive(bool isActive)
        {
            active = isActive;
            pennant.gameObject.SetActive(isActive);
            if (!isActive) targetX01 = playerIndex == 0 ? settings.waitingX01 : 1f - settings.waitingX01;
        }

        public bool IsActive => active;

        public void PlayStrike()
        {
            strikeT = 0f;
            hurtT = 1f;
        }

        public void PlayHurt()
        {
            if (defeated) return;
            hurtT = 0f;
        }

        public void PlayVictory()
        {
            defeated = false;
            victoryT = 0f;
        }

        public void PlayDefeat()
        {
            defeated = true;
            victoryT = 1f;
            strikeT = 1f;
        }

        public void ResetPose()
        {
            defeated = false;
            victoryT = strikeT = hurtT = poseT = 1f;
            hype = 0f;
        }

        /// <summary>Smoothed Hype 0..1 (HypeJuice): the cat dances harder the more the player moves.</summary>
        public void SetHype(float hype01)
        {
            hype = Mathf.Clamp01(hype01);
        }

        /// <summary>Big celebratory pose when a Hype tier (1..3) is reached.</summary>
        public void PlayTierPose(int tier)
        {
            if (defeated) return;
            poseT = 0f;
            poseStrength = 0.4f + 0.2f * Mathf.Clamp(tier, 1, 3);
        }

        #endregion

        #region Helpers

        void ApplyVisibility()
        {
            visible = settings.visible;
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }
        }

        void Place()
        {
            var sim = ArenaGeometryStand(currentX01);
            Position = layout.ToWorld(sim);
            transform.SetPositionAndRotation(Position, layout.transform.rotation);
        }

        /// <summary>Stand beside and slightly behind the ball (toward the camera) on the launch line, inside the walls.</summary>
        Vector2 ArenaGeometryStand(float x01)
        {
            var origin = Simulation.ArenaGeometry.LaunchOrigin(layout.Rules, x01);
            var side = playerIndex == 0 ? -settings.standSideOffset : settings.standSideOffset;
            var x = Mathf.Clamp(origin.x + side, 0.35f, layout.Rules.columns - 0.35f);
            return new Vector2(x, Mathf.Max(0.05f, origin.y - settings.standOffset));
        }

        void UpdateEars(float dt)
        {
            if (earL == null || earR == null) return;
            earTimer -= dt;
            if (earTimer <= 0f) earTimer = settings.earTwitchInterval + Random.value * settings.earTwitchInterval;
            var twitch = earTimer < 0.15f ? Mathf.Sin(earTimer / 0.15f * Mathf.PI) * 25f : 0f;
            earL.localRotation = Quaternion.Euler(0f, 0f, twitch);
            earR.localRotation = Quaternion.Euler(0f, 0f, -twitch * 0.5f);
        }

        #endregion
    }
}
