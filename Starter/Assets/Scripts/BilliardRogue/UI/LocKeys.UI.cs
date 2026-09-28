#nullable enable

namespace Nex.BilliardRogue
{
    // UI-Views additions to the base key set (LocKeys.cs). English copy in the trailing "// en:" comment;
    // "(smart)" marks smart-string entries with {n} arguments.
    public static partial class LocKeys
    {
        public static partial class PlayerMode
        {
            public const string SoloHint = "br.ui.playerMode.soloHint";       // en: Solo: one cat, one cue
        }

        public static partial class Hud
        {
            public const string BallsLabel = "br.hud.ballsLabel";             // en: Balls
            public const string Next = "br.hud.next";                         // en: NEXT
            public const string BonusBalls = "br.hud.bonusBalls";             // en: Bonus +{0} (smart)
            public const string Shooter = "br.hud.shooter";                   // en: Shooter
            public const string TrackingWarningPlayer = "br.hud.trackingWarningPlayer"; // en: P{0}: show both paws (smart)
        }

        public static partial class Reward
        {
            public const string HealName = "br.reward.healName";              // en: Hearty Meal
            public const string MaxHpName = "br.reward.maxHpName";            // en: Vitality Charm
            public const string LevelUp = "br.reward.levelUp";                // en: Lv {0} → Lv {1} (smart)
            // Uses arrows the pixel font has (it lacks ◀ ▶).
            public const string ChooseHint = "br.ui.reward.chooseHint";       // en: ← → choose · OK to confirm
        }

        public static partial class TrackingLost
        {
            public const string Hint = "br.ui.trackingLost.hint";             // en: Stand in front of the camera with both paws visible.
        }

        public static partial class Summary
        {
            public const string Unlocked = "br.summary.unlocked";             // en: New balls unlocked!
            public const string StatsHeader = "br.summary.statsHeader";       // en: Run stats
        }

        public static partial class Settings
        {
            public const string Hint = "br.ui.settings.hint";                 // en: ← → adjust · Back to return
        }
    }
}
