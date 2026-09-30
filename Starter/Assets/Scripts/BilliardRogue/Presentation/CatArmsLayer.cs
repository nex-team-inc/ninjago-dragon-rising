#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Cat arms at the bottom of the screen (GDD v2 §19, JuiceConfig.arms), per player: a fist holds the cue at its end,
    /// its arm running along the stick from below the screen edge, for as long as the cue shows (CatView.CueShown: the
    /// whole run), so the stick keeps its place at the bottom and follows the aim and the strike thrust in the cat's paw;
    /// an open paw holds the waiting ball from the side away from the cue while a shot waits (CatView.BallWaiting), then
    /// sinks out of view once the shot is fired. Re-projected every LateUpdate (after the cats' Update) through
    /// PixelWorldDisplay.TryWorldToCanvas. Allocation-free.
    /// </summary>
    public sealed class CatArmsLayer : MonoBehaviour
    {
        const float SideDeadZone = 8f;

        [Header("Wiring (BoardPrefabBuilder)")]
        [SerializeField] RectTransform rect = null!;
        [Tooltip("Index = player: the open paw holding the ball.")]
        [SerializeField] PawArm[] ballArms = System.Array.Empty<PawArm>();
        [Tooltip("Index = player: the fist holding the cue.")]
        [SerializeField] PawArm[] cueArms = System.Array.Empty<PawArm>();

        PixelWorldDisplay display = null!;
        JuiceConfig.ArmsSettings settings = null!;
        CatView[] cats = System.Array.Empty<CatView>();
        float[] ballRise = System.Array.Empty<float>();
        float[] cueRise = System.Array.Empty<float>();
        float[] side = System.Array.Empty<float>();

        public void Initialize(PixelWorldDisplay aDisplay, JuiceConfig juice, CatView[] aCats)
        {
            display = aDisplay;
            settings = juice.Arms;
            cats = aCats;
            var players = Mathf.Min(ballArms.Length, cueArms.Length);
            ballRise = new float[players];
            cueRise = new float[players];
            side = new float[players];
            for (var p = 0; p < players; p++)
            {
                // Until the cue leans one way: P1's cue on the right of its ball, P2's on the left.
                side[p] = p == 0 ? 1f : -1f;
                ballArms[p].gameObject.SetActive(false);
                cueArms[p].gameObject.SetActive(false);
            }
        }

        void LateUpdate()
        {
            if (settings == null) return;
            var step = Time.unscaledDeltaTime / settings.riseSeconds;
            for (var p = 0; p < cueRise.Length; p++)
            {
                var cat = p < cats.Length ? cats[p] : null;
                var cue = settings.visible && cat != null && cat.gameObject.activeInHierarchy && cat.CueShown;
                cueRise[p] = Mathf.MoveTowards(cueRise[p], cue ? 1f : 0f, step);
                ballRise[p] = Mathf.MoveTowards(ballRise[p], cue && cat!.BallWaiting ? 1f : 0f, step);
                var cueArm = cueArms[p];
                var ballArm = ballArms[p];
                SetShown(cueArm, cueRise[p] > 0f);
                SetShown(ballArm, ballRise[p] > 0f);
                if (cueRise[p] <= 0f || cat == null) continue;
                if (!display.TryWorldToCanvas(cat.BallWorld, rect, out var ball)
                    || !display.TryWorldToCanvas(cat.CueTipWorld, rect, out var tip)
                    || !display.TryWorldToCanvas(cat.CueButtWorld, rect, out var butt))
                {
                    continue;
                }

                var grip = Grip(tip, butt);
                var dx = grip.x - ball.x;
                if (dx > SideDeadZone) side[p] = 1f;
                else if (dx < -SideDeadZone) side[p] = -1f;

                // The fist grips the stick; its arm hangs straight down off the screen edge, so the wooden cue shows above it.
                var cuePaw = grip + Sink(cueRise[p]);
                cueArm.Reach(cuePaw + new Vector2(0f, -settings.shoulderDrop), cuePaw);
                if (ballRise[p] <= 0f) continue;

                var away = -side[p];
                var ballPaw = ball + new Vector2(away * settings.ballPawSide, -settings.ballPawDrop) + Sink(ballRise[p]);
                ballArm.Reach(ballPaw + new Vector2(away * settings.ballShoulderSpread, -settings.shoulderDrop), ballPaw);
            }
        }

        /// <summary>Where the cue crosses gripHeight above the screen bottom, kept within gripMinAlongCue..gripAlongCue of the cue.</summary>
        Vector2 Grip(Vector2 tip, Vector2 butt)
        {
            var edgeY = -rect.rect.height * 0.5f + settings.gripHeight;
            var t = tip.y - butt.y > 1f ? (tip.y - edgeY) / (tip.y - butt.y) : settings.gripAlongCue;
            t = Mathf.Clamp(t, settings.gripMinAlongCue, Mathf.Max(settings.gripMinAlongCue, settings.gripAlongCue));
            return Vector2.Lerp(tip, butt, t);
        }

        Vector2 Sink(float rise) => new(0f, -(1f - Easing.OutQuad(rise)) * settings.sinkDistance);

        static void SetShown(PawArm arm, bool value)
        {
            if (arm.gameObject.activeSelf != value) arm.gameObject.SetActive(value);
        }
    }
}
