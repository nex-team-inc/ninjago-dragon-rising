#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Cat arms at the bottom of the screen (GDD v2 §19, JuiceConfig.arms): per player an open paw holds the waiting ball
    /// from the side away from the cue and a fist grips the cue under it, both reaching up from shoulders below the screen edge while
    /// that player's cue shows (a shot waits, CatView.CueShown). They follow the ball along the launch line, the cue around
    /// the aim and its thrust on the strike, then sink out of view once the shot is fired. The ball arm comes from the far
    /// side of the cue arm, so the two never cross. Re-projected every LateUpdate (after the cats' Update) through
    /// PixelWorldDisplay.TryWorldToCanvas. Allocation-free.
    /// </summary>
    public sealed class CatArmsLayer : MonoBehaviour
    {
        const float SideDeadZone = 8f;

        [Header("Wiring (BoardPrefabBuilder)")]
        [SerializeField] RectTransform rect = null!;
        [Tooltip("Index = player: the open paw holding the ball.")]
        [SerializeField] PawArm[] ballArms = System.Array.Empty<PawArm>();
        [Tooltip("Index = player: the fist gripping the cue.")]
        [SerializeField] PawArm[] cueArms = System.Array.Empty<PawArm>();

        PixelWorldDisplay display = null!;
        JuiceConfig.ArmsSettings settings = null!;
        ArenaLayout layout = null!;
        CatView[] cats = System.Array.Empty<CatView>();
        float[] rise = System.Array.Empty<float>();
        float[] side = System.Array.Empty<float>();
        bool[] shown = System.Array.Empty<bool>();

        public void Initialize(PixelWorldDisplay aDisplay, JuiceConfig juice, ArenaLayout aLayout, CatView[] aCats)
        {
            display = aDisplay;
            settings = juice.Arms;
            layout = aLayout;
            cats = aCats;
            var players = Mathf.Min(ballArms.Length, cueArms.Length);
            rise = new float[players];
            side = new float[players];
            shown = new bool[players];
            for (var p = 0; p < players; p++)
            {
                // Until the aim picks a side: P1's cue arm from the right, P2's from the left.
                side[p] = p == 0 ? 1f : -1f;
                shown[p] = true;
                SetShown(p, false);
            }
        }

        void LateUpdate()
        {
            if (settings == null) return;
            var dt = Time.unscaledDeltaTime;
            for (var p = 0; p < rise.Length; p++)
            {
                var cat = p < cats.Length ? cats[p] : null;
                var want = settings.visible && cat != null && cat.gameObject.activeInHierarchy && cat.CueShown;
                rise[p] = Mathf.MoveTowards(rise[p], want ? 1f : 0f, dt / settings.riseSeconds);
                SetShown(p, rise[p] > 0f);
                if (!shown[p] || cat == null) continue;
                if (!display.TryWorldToCanvas(cat.BallWorld, rect, out var ball)
                    || !display.TryWorldToCanvas(cat.CueTipWorld, rect, out var tip)
                    || !display.TryWorldToCanvas(cat.CueButtWorld, rect, out var butt))
                {
                    continue;
                }

                var grip = Grip(cat, tip, butt);
                var dx = grip.x - ball.x;
                if (dx > SideDeadZone) side[p] = 1f;
                else if (dx < -SideDeadZone) side[p] = -1f;

                var sink = new Vector2(0f, -(1f - Easing.OutQuad(rise[p])) * settings.sinkDistance);
                var away = -side[p];
                var ballPaw = ball + new Vector2(away * settings.ballPawSide, -settings.ballPawDrop) + sink;
                var cuePaw = grip + sink;
                var down = new Vector2(0f, -settings.shoulderDrop);
                ballArms[p].Reach(ballPaw + new Vector2(away * settings.ballShoulderSpread, 0f) + down, ballPaw);
                cueArms[p].Reach(cuePaw + new Vector2(side[p] * settings.cueShoulderSpread, 0f) + down, cuePaw);
            }
        }

        /// <summary>gripFromTip cells behind the tip, slid toward the tip while that point is below minPawHeight.</summary>
        Vector2 Grip(CatView cat, Vector2 tip, Vector2 butt)
        {
            var length = Vector3.Distance(cat.CueTipWorld, cat.CueButtWorld);
            var t = length > 1e-4f ? Mathf.Clamp(settings.gripFromTip * layout.CellSize / length, 0.05f, 0.95f) : 0f;
            var minY = -rect.rect.height * 0.5f + settings.minPawHeight;
            var grip = Vector2.Lerp(tip, butt, t);
            if (grip.y >= minY || tip.y <= butt.y) return grip;
            var highest = tip.y <= minY ? 0f : (tip.y - minY) / (tip.y - butt.y);
            return Vector2.Lerp(tip, butt, Mathf.Min(t, highest));
        }

        void SetShown(int player, bool value)
        {
            if (shown[player] == value) return;
            shown[player] = value;
            ballArms[player].gameObject.SetActive(value);
            cueArms[player].gameObject.SetActive(value);
        }
    }
}
