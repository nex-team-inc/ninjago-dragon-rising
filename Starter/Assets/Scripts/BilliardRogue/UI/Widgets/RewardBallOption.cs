#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// One reward ball of the motion pick (GDD v2 §1, §4): a big floating ball (icon, glow in the ball's colour, drop
    /// shadow), the hold ring, its name, a 1-3 word effect and a "New ball" / "Lv 1 → Lv 2" chip. RewardView drives the
    /// bob, hover scale and hold fill every frame; reveal/pick tweens run on Body.
    /// </summary>
    public sealed class RewardBallOption : MonoBehaviour
    {
        [SerializeField] Button button = null!;
        [SerializeField] UiButtonKeyResponder responder = null!;
        [Tooltip("Reveal/pick tweens (scale + fade).")]
        [SerializeField] RectTransform body = null!;
        [SerializeField] CanvasGroup bodyGroup = null!;
        [Tooltip("Bobs up and down (ball + labels).")]
        [SerializeField] RectTransform floater = null!;
        [Tooltip("Hover scale (ball graphics only, labels stay pixel-exact).")]
        [SerializeField] RectTransform visual = null!;

        [Header("Ball")]
        [SerializeField] Image ball = null!;
        [SerializeField] Image glow = null!;
        [SerializeField] Image holdTrack = null!;
        [SerializeField] Image holdFill = null!;
        [Tooltip("Visual radius of the ball in canvas units at scale 1 (paw hit test).")]
        [SerializeField] float radius = 128f;

        [Header("Labels")]
        [SerializeField] TextLabel nameLabel = null!;
        [SerializeField] TextLabel shortLabel = null!;
        [SerializeField] GameObject chip = null!;
        [SerializeField] TextLabel chipLabel = null!;

        Color glowColor = Color.white;
        float hover;
        float shownFill = -1f;
        float shownHover = -1f;

        public Button Button => button;
        public UiButtonKeyResponder Responder => responder;
        public RectTransform Body => body;
        public CanvasGroup BodyGroup => bodyGroup;
        public RectTransform Visual => visual;
        public Image Glow => glow;
        public float Radius => radius;

        public void Show(RewardOption option, BallCatalog balls, RunState run, UiTheme theme)
        {
            switch (option.kind)
            {
                case RewardKind.UpgradeBall:
                    var level = Mathf.Clamp(option.amount, 1, SimConstants.MaxBallLevel);
                    ShowBall(option.ballType, balls, theme);
                    chipLabel.SetKey(LocKeys.Reward.LevelUp, CurrentLevel(option, run), level);
                    break;
                case RewardKind.Heal:
                    // v1 saves may still hold Heal / Max HP options; v2 only offers balls.
                    ShowOther(theme.RewardHeal, LocKeys.Reward.HealName, LocKeys.Reward.HealDesc, option.amount, theme);
                    break;
                case RewardKind.MaxHp:
                    ShowOther(theme.RewardMaxHp, LocKeys.Reward.MaxHpName, LocKeys.Reward.MaxHpDesc, option.amount, theme);
                    break;
                default:
                    ShowBall(option.ballType, balls, theme);
                    chipLabel.SetKey(LocKeys.Reward.KindNewBall);
                    break;
            }

            hover = 0f;
            shownFill = shownHover = -1f;
            SetFill(0f, false);
            SetHover(0f, 1f, 0f);
        }

        /// <summary>Per-frame hover emphasis: amount 0..1 → scale 1..hoverScale, glow alpha; bob offset in units.</summary>
        public void SetHover(float amount, float hoverScale, float bob)
        {
            hover = amount;
            floater.anchoredPosition = new Vector2(0f, bob);
            if (Mathf.Abs(amount - shownHover) < 0.002f) return;
            shownHover = amount;
            var scale = Mathf.Lerp(1f, hoverScale, amount);
            visual.localScale = new Vector3(scale, scale, 1f);
            var c = glowColor;
            c.a = Mathf.Lerp(0.35f, 0.9f, amount);
            glow.color = c;
        }

        /// <summary>Hold fill 0..1; the track shows while a paw is on the ball.</summary>
        public void SetFill(float fill, bool trackVisible)
        {
            var track = trackVisible || fill > 0f;
            if (holdTrack.enabled != track) holdTrack.enabled = track;
            if (Mathf.Abs(fill - shownFill) < 0.002f) return;
            shownFill = fill;
            holdFill.fillAmount = fill;
            holdFill.enabled = fill > 0f;
        }

        /// <summary>Paw hit radius at the current hover scale.</summary>
        public float HitRadius(float hitScale) => radius * visual.localScale.x * hitScale;

        public float HoverAmount => hover;

        void ShowBall(BallType type, BallCatalog balls, UiTheme theme)
        {
            var definition = balls.Get(type);
            ball.sprite = definition.Icon;
            nameLabel.SetKey(LocKeys.Ball.Name(type));
            shortLabel.SetKey(LocKeys.Ball.Short(type));
            chipLabel.Color = theme.RarityColor(definition.Rules.rarity);
            chip.SetActive(true);
            glowColor = definition.Color;
        }

        void ShowOther(Sprite? icon, string nameKey, string descKey, int amount, UiTheme theme)
        {
            ball.sprite = icon;
            nameLabel.SetKey(nameKey);
            shortLabel.SetKey(descKey, amount);
            chip.SetActive(false);
            glowColor = theme.Positive;
        }

        static int CurrentLevel(RewardOption option, RunState run)
        {
            var index = option.bagIndex;
            return index >= 0 && index < run.bag.Count ? run.bag[index].level : option.amount - 1;
        }
    }
}
