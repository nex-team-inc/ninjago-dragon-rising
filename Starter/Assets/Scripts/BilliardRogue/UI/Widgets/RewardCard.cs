#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>One reward card: kind chip, icon, name, level stars, per-level description and rarity.</summary>
    public sealed class RewardCard : MonoBehaviour
    {
        [SerializeField] Button button = null!;
        [SerializeField] UiButtonKeyResponder responder = null!;
        [Tooltip("Card body animated by reveal/pick (not the focus-pulsed root).")]
        [SerializeField] RectTransform body = null!;
        [SerializeField] CanvasGroup bodyGroup = null!;

        [Header("Content")]
        [SerializeField] TextLabel kindLabel = null!;
        [SerializeField] Image icon = null!;
        [SerializeField] TextLabel nameLabel = null!;
        [SerializeField] TextLabel levelLabel = null!;
        [Tooltip("Three star faces (★), gold up to the level.")]
        [SerializeField] TextLabel[] stars = null!;
        [SerializeField] GameObject starsRow = null!;
        [SerializeField] TextLabel descriptionLabel = null!;
        [SerializeField] GameObject rarityChip = null!;
        [SerializeField] TextLabel rarityLabel = null!;

        static readonly string[] rarityKeys =
        {
            LocKeys.Reward.RarityCommon, LocKeys.Reward.RarityUncommon, LocKeys.Reward.RarityRare,
        };

        public Button Button => button;
        public UiButtonKeyResponder Responder => responder;
        public RectTransform Body => body;
        public CanvasGroup BodyGroup => bodyGroup;

        public void Show(RewardOption option, BallCatalog balls, RunState run, UiTheme theme)
        {
            var isBall = option.kind is RewardKind.NewBall or RewardKind.UpgradeBall;
            starsRow.SetActive(isBall);
            rarityChip.SetActive(isBall);
            levelLabel.gameObject.SetActive(isBall);
            switch (option.kind)
            {
                case RewardKind.NewBall:
                    ShowBall(option.ballType, 1, balls, theme);
                    kindLabel.SetKey(LocKeys.Reward.KindNewBall);
                    levelLabel.SetKey(LocKeys.Reward.Level, 1);
                    break;
                case RewardKind.UpgradeBall:
                    var level = Mathf.Clamp(option.amount, 1, SimConstants.MaxBallLevel);
                    ShowBall(option.ballType, level, balls, theme);
                    kindLabel.SetKey(LocKeys.Reward.KindUpgrade);
                    levelLabel.SetKey(LocKeys.Reward.LevelUp, CurrentLevel(option, run), level);
                    break;
                case RewardKind.Heal:
                    icon.sprite = theme.RewardHeal;
                    kindLabel.SetKey(LocKeys.Reward.KindHeal);
                    nameLabel.SetKey(LocKeys.Reward.HealName);
                    descriptionLabel.SetKey(LocKeys.Reward.HealDesc, option.amount);
                    break;
                case RewardKind.MaxHp:
                    icon.sprite = theme.RewardMaxHp;
                    kindLabel.SetKey(LocKeys.Reward.KindMaxHp);
                    nameLabel.SetKey(LocKeys.Reward.MaxHpName);
                    descriptionLabel.SetKey(LocKeys.Reward.MaxHpDesc, option.amount);
                    break;
            }
        }

        void ShowBall(BallType type, int level, BallCatalog balls, UiTheme theme)
        {
            var definition = balls.Get(type);
            icon.sprite = definition.Icon;
            nameLabel.SetKey(LocKeys.Ball.Name(type));
            descriptionLabel.SetKey(LocKeys.Ball.Description(type, level));
            var rarity = definition.Rules.rarity;
            rarityLabel.SetKey(rarityKeys[(int)rarity]);
            rarityLabel.Color = theme.RarityColor(rarity);
            for (var i = 0; i < stars.Length; i++)
            {
                stars[i].Color = i < level ? theme.Accent : theme.Disabled;
            }
        }

        static int CurrentLevel(RewardOption option, RunState run)
        {
            var index = option.bagIndex;
            return index >= 0 && index < run.bag.Count ? run.bag[index].level : option.amount - 1;
        }
    }
}
