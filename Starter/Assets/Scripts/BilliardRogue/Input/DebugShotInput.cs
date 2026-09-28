#nullable enable

using Nex.BilliardRogue.Simulation;
using Nex.Dev;
using Nex.Utils;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Mouse / keyboard shot input for the Editor and debug builds (GDD §3.3), read through Nex.Dev.DebugInput
    /// (always false under PRODUCTION). The mouse aims at the point it hovers on the arena (a ray from the world
    /// camera onto the arena floor; without one the screen stands for the arena); holding the follow button walks
    /// the cat to the mouse. Arrows nudge the launch position, A/D rotate the aim, Space strikes (Shift + Space =
    /// power strike), and holding the drop key simulates lost tracking. ShotInputRouter enables it only while it is
    /// the active source.
    /// </summary>
    public sealed class DebugShotInput : MonoBehaviour, IShotInput
    {
        const float MouseMoveThresholdSq = 0.25f;
        const float MinMouseAimDistance = 0.3f;

        [Header("Keys (Nex.Dev.DebugInput)")]
        [SerializeField] KeyCode strikeKey = KeyCode.Space;
        [Tooltip("Held with the strike key: power strike at power 1.")]
        [SerializeField] KeyCode powerModifierKey = KeyCode.LeftShift;
        [SerializeField] KeyCode launchLeftKey = KeyCode.LeftArrow;
        [SerializeField] KeyCode launchRightKey = KeyCode.RightArrow;
        [Tooltip("Rotates the aim toward the left wall.")]
        [SerializeField] KeyCode aimLeftKey = KeyCode.A;
        [Tooltip("Rotates the aim toward the right wall.")]
        [SerializeField] KeyCode aimRightKey = KeyCode.D;
        [Tooltip("Held: the cat follows the mouse instead of aiming at it.")]
        [SerializeField] KeyCode followMouseKey = KeyCode.Mouse1;
        [Tooltip("Held: reports lost tracking (tests the tracking-lost overlay without a camera).")]
        [SerializeField] KeyCode dropTrackingKey = KeyCode.L;

        ControlConfig config = null!;
        ArenaRules arena = null!;
        Camera? worldCamera;
        ArenaLayout? layout;
        float angleDeg = 90f;
        Vector3 lastMousePosition;
        bool hasPendingStrike;
        StrikeInfo pendingStrike;
        float pendingStrikeTime;

        public int PlayerIndex { get; private set; }
        public bool IsTracking { get; private set; } = true;
        public float LaunchX01 { get; private set; } = 0.5f;
        public Vector2 AimDirection { get; private set; } = Vector2.up;

        #region Life Cycle

        /// <summary>worldCamera / layout are optional; without them the mouse maps the screen onto the arena.</summary>
        public void Initialize(int playerIndex, ControlConfig aConfig, ArenaRules aArena, Camera? aWorldCamera, ArenaLayout? aLayout)
        {
            PlayerIndex = playerIndex;
            config = aConfig;
            arena = aArena;
            worldCamera = aWorldCamera;
            layout = aLayout;
            AimDirection = ArenaGeometry.ClampAim(arena, Vector2Utils.PolarDeg(angleDeg));
        }

        void OnEnable()
        {
            lastMousePosition = MousePosition;
        }

        void Update()
        {
            IsTracking = !DebugInput.GetKey(dropTrackingKey);
            var dt = Time.unscaledDeltaTime;
            UpdateKeys(dt);
            UpdateMouse();
            angleDeg = Mathf.Clamp(angleDeg, arena.minAimAngleDeg, 180f - arena.minAimAngleDeg);
            AimDirection = Vector2Utils.PolarDeg(angleDeg);
            if (DebugInput.GetKeyDown(strikeKey)) QueueStrike(DebugInput.GetKey(powerModifierKey));
        }

        #endregion

        #region IShotInput

        public bool TryConsumeStrike(out StrikeInfo strike)
        {
            strike = pendingStrike;
            if (!hasPendingStrike) return false;

            hasPendingStrike = false;
            return Time.unscaledTime - pendingStrikeTime <= config.StrikeExpirySeconds;
        }

        public void ResetStrike()
        {
            hasPendingStrike = false;
        }

        #endregion

        #region Helpers

        void UpdateKeys(float dt)
        {
            var launchStep = config.DebugLaunchPerSec * dt;
            if (DebugInput.GetKey(launchLeftKey)) LaunchX01 = Mathf.Clamp01(LaunchX01 - launchStep);
            if (DebugInput.GetKey(launchRightKey)) LaunchX01 = Mathf.Clamp01(LaunchX01 + launchStep);
            var aimStep = config.DebugAimDegPerSec * dt;
            if (DebugInput.GetKey(aimLeftKey)) angleDeg += aimStep;
            if (DebugInput.GetKey(aimRightKey)) angleDeg -= aimStep;
        }

        void UpdateMouse()
        {
            var mouse = MousePosition;
            var moved = (mouse - lastMousePosition).sqrMagnitude > MouseMoveThresholdSq;
            lastMousePosition = mouse;
            var follow = DebugInput.GetKey(followMouseKey);
            if (!moved && !follow) return;
            if (!TryMouseToSim(mouse, out var sim)) return;

            if (follow)
            {
                LaunchX01 = Mathf.InverseLerp(arena.ballRadius, arena.columns - arena.ballRadius, sim.x);
                return;
            }

            var toMouse = sim - ArenaGeometry.LaunchOrigin(arena, LaunchX01);
            if (toMouse.sqrMagnitude < MinMouseAimDistance * MinMouseAimDistance) return;
            var clamped = ArenaGeometry.ClampAim(arena, toMouse);
            angleDeg = Mathf.Atan2(clamped.y, clamped.x) * Mathf.Rad2Deg;
        }

        // The world camera renders the full-screen world image, so screen-normalized = its viewport.
        bool TryMouseToSim(Vector3 mouse, out Vector2 sim)
        {
            var viewport = new Vector2(mouse.x / Screen.width, mouse.y / Screen.height);
            if (worldCamera == null || layout == null)
            {
                sim = new Vector2(viewport.x * arena.columns, viewport.y * ArenaGeometry.TopWallY(arena));
                return true;
            }

            var ray = worldCamera.ViewportPointToRay(viewport);
            var floor = new Plane(layout.transform.up, layout.transform.position);
            if (!floor.Raycast(ray, out var enter))
            {
                sim = default;
                return false;
            }

            sim = layout.ToSim(ray.GetPoint(enter));
            return true;
        }

        void QueueStrike(bool power)
        {
            pendingStrike = new StrikeInfo
            {
                direction = AimDirection,
                power01 = power ? 1f : config.DebugStrikePower,
                isPowerShot = power,
            };
            pendingStrikeTime = Time.unscaledTime;
            hasPendingStrike = true;
        }

        static Vector3 MousePosition
        {
            get
            {
#if PRODUCTION
                return Vector3.zero;
#else
                return UnityEngine.Input.mousePosition;
#endif
            }
        }

        #endregion
    }
}
