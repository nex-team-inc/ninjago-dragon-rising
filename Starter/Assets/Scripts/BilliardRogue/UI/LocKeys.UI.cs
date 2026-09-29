#nullable enable

namespace Nex.BilliardRogue
{
    // UI-Views additions to the base key set (LocKeys.cs). English copy in the trailing "// en:" comment;
    // "(smart)" marks smart-string entries with {n} arguments.
    public static partial class LocKeys
    {
        public static partial class Title
        {
            public const string NoRecordYet = "br.ui.title.noRecordYet";      // en: Best run: none yet
        }

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
            // Hype meter (GDD v2 §3): title and the tier 1..3 callouts.
            public const string Hype = "br.hud.hype";                         // en: POWER
            public const string HypeTier1 = "br.hud.hypeTier1";               // en: POWER ×1.4!
            public const string HypeTier2 = "br.hud.hypeTier2";               // en: ×1.9!!
            public const string HypeTier3 = "br.hud.hypeTier3";               // en: MAX!!!
            public const string MovePrompt = "br.hud.movePrompt";             // en: MOVE!
            public const string MoveHint = "br.hud.moveHint";                 // en: Dance to power up the balls!

            public static readonly string[] HypeTiers = { Hype, HypeTier1, HypeTier2, HypeTier3 };
        }

        public static partial class Reward
        {
            public const string HealName = "br.reward.healName";              // en: Hearty Meal
            public const string MaxHpName = "br.reward.maxHpName";            // en: Vitality Charm
            public const string LevelUp = "br.reward.levelUp";                // en: Lv {0} → Lv {1} (smart)
            // Uses arrows the pixel font has (it lacks ◀ ▶).
            public const string ChooseHint = "br.ui.reward.chooseHint";       // en: ← → choose · OK to confirm
            // Motion pick (GDD v2 §4).
            public const string PickHeader = "br.ui.reward.pickHeader";       // en: Pick a ball!
            public const string PawHint = "br.ui.reward.pawHint";             // en: Hold both paws on a ball
            public const string ChooserBanner = "br.ui.reward.chooserBanner"; // en: P{0} picks! (smart)
        }

        public static partial class Ball
        {
            // 1-3 word effect shown on reward balls (GDD v2 §1), indexed by (int)BallType.
            public static readonly string[] Shorts =
            {
                "br.ball.basic.short",    // en: Plain shot
                "br.ball.flame.short",    // en: Burns
                "br.ball.frost.short",    // en: Freezes
                "br.ball.thunder.short",  // en: Chain zap
                "br.ball.bomb.short",     // en: Explodes
                "br.ball.splitter.short", // en: Splits in 2 (Lv3: 3)
                "br.ball.piercer.short",  // en: Pierces
                "br.ball.iron.short",     // en: Heavy hit
                "br.ball.venom.short",    // en: Poisons
                "br.ball.vampire.short",  // en: Heals you
                "br.ball.rubber.short",   // en: Wall bounce power
                "br.ball.lucky.short",    // en: Lucky crits
            };

            public static string Short(Simulation.BallType type) => Shorts[(int)type];
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
