#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// One player's cat knight on the launch line: walks to the launch X, idles in a 3/4 view toward the camera,
    /// turns up the arena to strike with the cue, flinches when hurt, dances on victory and slumps on defeat.
    /// Rigid parts (Cat_Hero.fbx: Body, Head, EarL/R, Tail, PawL/R, Cape) are animated by code; P1/P2 palettes
    /// swap the model material (M_Palette / M_Palette_CatP2).
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
        MaterialPropertyBlock block = null!;
        int playerIndex;
        float currentX01 = 0.5f;
        float targetX01 = 0.5f;
        float phase;
        float earTimer;
        float strikeT = 1f;
        float hurtT = 1f;
        float victoryT = 1f;
        bool defeated;
        bool active;
        Vector3 cueBaseLocal;
        Quaternion cueBaseRotation;

        public int PlayerIndex => playerIndex;
        public Vector3 Position { get; private set; }
        public Vector3 LabelAnchor => Position + Vector3.up * (labelHeight * layout.CellSize * settings.modelScale);
        public Vector3 Center => Position + Vector3.up * (0.5f * layout.CellSize * settings.modelScale);

        #region Life Cycle

        public void Initialize(int aPlayerIndex, ArenaLayout aLayout, JuiceConfig juice)
        {
            playerIndex = aPlayerIndex;
            layout = aLayout;
            settings = juice.Cat;
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
            strikeT = hurtT = victoryT = 1f;
            SetActive(false);
            Place();
        }

        void Update()
        {
            // The scene cats exist before any run: BoardPresenter.Initialize wires them at the first GameplayView.
            if (settings == null) return;
            var dt = Time.deltaTime;
            phase += dt;
            if (!defeated)
            {
                var step = settings.walkSpeed / layout.Rules.columns * dt;
                currentX01 = Mathf.MoveTowards(currentX01, targetX01, step);
            }

            Place();
            var walking = !Mathf.Approximately(currentX01, targetX01);
            var yaw = settings.idleYaw;
            var cueOffset = Vector3.zero;
            var bounce = 0f;
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
            else if (walking)
            {
                yaw = currentX01 < targetX01 ? 90f : 270f;
                bounce = Mathf.Abs(Mathf.Sin(phase * 14f)) * 0.06f * layout.CellSize;
            }

            var lean = defeated ? Quaternion.Euler(35f, 0f, 0f) : Quaternion.identity;
            model.localRotation = Quaternion.Euler(0f, yaw, 0f) * lean;
            model.localPosition = new Vector3(0f, bounce, walking ? 0f : Mathf.Sin(phase * 1.3f) * 0.01f);
            model.localScale = (defeated ? new Vector3(1.08f, 0.85f, 1.08f) : Vector3.one) * settings.modelScale;
            cue.localPosition = cueBaseLocal + cueOffset;
            cue.localRotation = cueBaseRotation;
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase * settings.tailWagSpeed * Mathf.PI) * settings.tailWagAngle, 0f);
            if (head != null) head.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase * 0.7f) * 6f, 0f);
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
            victoryT = strikeT = hurtT = 1f;
        }

        #endregion

        #region Helpers

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
