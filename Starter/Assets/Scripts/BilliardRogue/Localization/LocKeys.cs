#nullable enable

using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    // Base key set (TDD §11). Every constant carries the final English copy in its trailing "// en:" comment;
    // "(smart)" marks smart-string entries with {n} arguments. Modules add keys in their own partial file.
    public static partial class LocKeys
    {
        public const string Table = "LocalizationTable";

        #region Common & Title

        public static class Common
        {
            public const string Ok = "br.ui.common.ok";                       // en: OK
            public const string Back = "br.ui.common.back";                   // en: Back
            public const string Yes = "br.ui.common.yes";                     // en: Yes
            public const string No = "br.ui.common.no";                       // en: No
            public const string Player = "br.ui.common.player";               // en: Player {0} (smart)
            public const string ExitConfirm = "br.ui.common.exitConfirm";     // en: Leave the game?
        }

        public static class Title
        {
            public const string Tagline = "br.ui.title.tagline";              // en: A cat, a cue, a dungeon.
            public const string Continue = "br.ui.title.continue";            // en: Continue
            public const string ContinueInfo = "br.ui.title.continueInfo";    // en: Act {0} · Stage {1} · {2} HP (smart)
            public const string NewRun = "br.ui.title.newRun";                // en: New Run
            public const string Settings = "br.ui.title.settings";            // en: Settings
            public const string Best = "br.ui.title.best";                    // en: Best run: Act {0} · Stage {1} (smart)
            public const string BestVictory = "br.ui.title.bestVictory";      // en: Best run: Dungeon cleared!
            public const string NoBest = "br.ui.title.noBest";                // en: No runs yet
            public const string Runs = "br.ui.title.runs";                    // en: Runs {0} · Wins {1} (smart)
        }

        public static class PlayerMode
        {
            public const string Prompt = "br.ui.playerMode.prompt";           // en: How many players?
            public const string OnePlayer = "br.ui.playerMode.onePlayer";     // en: 1 Player
            public const string TwoPlayers = "br.ui.playerMode.twoPlayers";   // en: 2 Players
            public const string CoopHint = "br.ui.playerMode.coopHint";       // en: Co-op: shared HP, alternating shots
        }

        #endregion

        #region Calibration & Setup

        public static class Calibration
        {
            public const string Header = "br.ui.calibration.header";          // en: Camera setup
            public const string MoveIn = "br.ui.calibration.moveIn";          // en: Move into the frame
            public const string RaiseHand = "br.ui.calibration.raiseHand";    // en: Raise your hand
            public const string PoseTutorial = "br.ui.calibration.poseTutorial"; // en: Hold your left paw up: that's the ball. Your right paw is the cue.
            public const string TestStrike = "br.ui.calibration.testStrike";  // en: Snap your right paw into your left paw!
            public const string Ready = "br.ui.calibration.ready";            // en: Ready!
            public const string PlayerReady = "br.ui.calibration.playerReady"; // en: Player {0} ready (smart)
            public const string Waiting = "br.ui.calibration.waiting";        // en: Waiting for player {0}… (smart)
            public const string LeftHandedHint = "br.ui.calibration.leftHandedHint"; // en: Left-handed cue is on: paws are swapped.
        }

        // Keys shared with SetupWarningMessage (starter setup hints).
        public static class Setup
        {
            public const string NoPlayer = "br.setup.noPlayer";               // en: No player
            public const string StepBack = "br.setup.stepBack";               // en: Step back
            public const string MoveCloser = "br.setup.moveCloser";           // en: Move closer
            public const string MoveToCenter = "br.setup.moveToCenter";       // en: Move to center
            public const string HoldStill = "br.setup.holdStill";             // en: Hold still
            public const string Good = "br.setup.good";                       // en: Good!
            public const string ShowBothPaws = "br.setup.showBothPaws";       // en: Show both paws
        }

        #endregion

        #region Gameplay HUD & floats

        public static class Hud
        {
            public const string Hp = "br.hud.hp";                             // en: HP
            public const string Balls = "br.hud.balls";                       // en: Balls {0}/{1} (smart)
            public const string Stage = "br.hud.stage";                       // en: Act {0} · Stage {1} (smart)
            public const string Boss = "br.hud.boss";                         // en: BOSS
            public const string Turn = "br.hud.turn";                         // en: Turn {0} (smart)
            public const string TurnBanner = "br.hud.turnBanner";             // en: Turn {0} — shoot! (smart)
            public const string ShooterBanner = "br.hud.shooterBanner";       // en: P{0}'s shot! (smart)
            public const string PlayerTag = "br.hud.playerTag";               // en: P{0} (smart)
            public const string FastForward = "br.hud.fastForward";           // en: Fast-forward »
            public const string ExtraBall = "br.hud.extraBall";               // en: +1 Ball
            public const string PowerArmed = "br.hud.powerArmed";             // en: Power ready!
            public const string TrackingWarning = "br.hud.trackingWarning";   // en: Show both paws
            public const string Pause = "br.hud.pause";                       // en: Pause
            public const string EnemiesLeft = "br.hud.enemiesLeft";           // en: Enemies {0} (smart)
            public const string WavesLeft = "br.hud.wavesLeft";               // en: Waves {0} (smart)
        }

        public static class Float
        {
            public const string Block = "br.float.block";                     // en: BLOCK
            public const string Crit = "br.float.crit";                       // en: CRIT!
            public const string Power = "br.float.power";                     // en: POWER!
            public const string Combo = "br.float.combo";                     // en: x{0} COMBO (smart)
            public const string FastForward = "br.float.fastForward";         // en: FAST-FORWARD »
            public const string Frozen = "br.float.frozen";                   // en: FROZEN
            public const string Heal = "br.float.heal";                       // en: +{0} (smart)
            public const string Miss = "br.float.miss";                       // en: MISS
        }

        #endregion

        #region Overlays: stage intro, reward, pause, tracking lost

        public static class StageIntro
        {
            public const string Header = "br.ui.stageIntro.header";           // en: Act {0} · Stage {1} (smart)
            public const string BossStage = "br.ui.stageIntro.bossStage";     // en: Boss Stage
            public const string BossAppears = "br.ui.stageIntro.bossAppears"; // en: {0} appears! (smart)
            public const string StageClear = "br.ui.stageIntro.stageClear";   // en: STAGE CLEAR
        }

        public static class Reward
        {
            public const string Header = "br.ui.reward.header";               // en: Choose a reward
            public const string Hint = "br.ui.reward.hint";                   // en: ◀ ▶ choose · Enter confirm
            public const string KindNewBall = "br.reward.kind.newBall";       // en: New ball
            public const string KindUpgrade = "br.reward.kind.upgrade";       // en: Level up
            public const string KindHeal = "br.reward.kind.heal";             // en: Heal
            public const string KindMaxHp = "br.reward.kind.maxHp";           // en: Max HP
            public const string HealDesc = "br.reward.healDesc";              // en: Restore {0} HP (smart)
            public const string MaxHpDesc = "br.reward.maxHpDesc";            // en: +{0} Max HP (smart)
            public const string UpgradeDesc = "br.reward.upgradeDesc";        // en: {0} → Lv {1} (smart)
            public const string Level = "br.reward.level";                    // en: Lv {0} (smart)
            public const string RarityCommon = "br.reward.rarity.common";     // en: Common
            public const string RarityUncommon = "br.reward.rarity.uncommon"; // en: Uncommon
            public const string RarityRare = "br.reward.rarity.rare";         // en: Rare
            public const string BagFull = "br.reward.bagFull";                // en: Bag full
            public const string Unlocked = "br.reward.unlocked";              // en: New ball unlocked: {0}! (smart)
        }

        public static class Pause
        {
            public const string Header = "br.ui.pause.header";                // en: Paused
            public const string Resume = "br.ui.pause.resume";                // en: Resume
            public const string Settings = "br.ui.pause.settings";            // en: Settings
            public const string SaveQuit = "br.ui.pause.saveQuit";            // en: Save & Quit
        }

        public static class TrackingLost
        {
            public const string Header = "br.ui.trackingLost.header";         // en: Step back into view
            public const string Body = "br.ui.trackingLost.body";             // en: Waiting for player {0}… (smart)
            public const string Resuming = "br.ui.trackingLost.resuming";     // en: Found you! Resuming…
        }

        #endregion

        #region Summary & Settings

        public static class Summary
        {
            public const string Victory = "br.summary.victory";               // en: Victory!
            public const string Defeat = "br.summary.defeat";                 // en: Defeat
            public const string NewRecord = "br.summary.newRecord";           // en: New record!
            public const string StageReached = "br.summary.stageReached";     // en: Reached Act {0} · Stage {1} (smart)
            public const string Turns = "br.summary.turns";                   // en: Turns
            public const string Shots = "br.summary.shots";                   // en: Shots
            public const string Hits = "br.summary.hits";                     // en: Hits
            public const string Kills = "br.summary.kills";                   // en: Enemies defeated
            public const string DamageDealt = "br.summary.damageDealt";       // en: Damage dealt
            public const string DamageTaken = "br.summary.damageTaken";       // en: Damage taken
            public const string BestCombo = "br.summary.bestCombo";           // en: Best combo
            public const string Bosses = "br.summary.bosses";                 // en: Bosses defeated
            public const string Time = "br.summary.time";                     // en: Time
            public const string PlayAgain = "br.summary.playAgain";           // en: Play again
            public const string ToTitle = "br.summary.toTitle";               // en: Title
        }

        public static class Settings
        {
            public const string Header = "br.ui.settings.header";             // en: Settings
            public const string Language = "br.ui.settings.language";         // en: Language
            public const string MasterVolume = "br.ui.settings.masterVolume"; // en: Master volume
            public const string MusicVolume = "br.ui.settings.musicVolume";   // en: Music volume
            public const string SfxVolume = "br.ui.settings.sfxVolume";       // en: Sound effects
            public const string AimGuide = "br.ui.settings.aimGuide";         // en: Aim guide
            public const string AimGuideShort = "br.ui.settings.aimGuide.short";   // en: Short
            public const string AimGuideNormal = "br.ui.settings.aimGuide.normal"; // en: Normal
            public const string AimGuideLong = "br.ui.settings.aimGuide.long";     // en: Long
            public const string LeftHanded = "br.ui.settings.leftHanded";     // en: Left-handed cue
            public const string ScreenShake = "br.ui.settings.screenShake";   // en: Screen shake
            public const string On = "br.ui.settings.on";                     // en: On
            public const string Off = "br.ui.settings.off";                   // en: Off
            public const string LanguageEn = "br.ui.settings.language.en";         // en: English
            public const string LanguageFrCa = "br.ui.settings.language.fr-CA";    // en: Français
            public const string LanguageZhHans = "br.ui.settings.language.zh-Hans"; // en: 简体中文
            public const string LanguageZhHant = "br.ui.settings.language.zh-Hant"; // en: 繁體中文
            public const string LanguageJa = "br.ui.settings.language.ja";         // en: 日本語
        }

        #endregion

        #region Balls, enemies, acts

        public static class Ball
        {
            // Indexed by (int)BallType. Descriptions: [type][level - 1].
            public static readonly string[] Names =
            {
                "br.ball.basic.name",     // en: Cue Ball
                "br.ball.flame.name",     // en: Ember
                "br.ball.frost.name",     // en: Frost
                "br.ball.thunder.name",   // en: Volt
                "br.ball.bomb.name",      // en: Boom
                "br.ball.splitter.name",  // en: Twin
                "br.ball.piercer.name",   // en: Lance
                "br.ball.iron.name",      // en: Iron
                "br.ball.venom.name",     // en: Venom
                "br.ball.vampire.name",   // en: Fang
                "br.ball.rubber.name",    // en: Bouncy
                "br.ball.lucky.name",     // en: Lucky
            };

            public static readonly string[][] Descriptions =
            {
                new[] { "br.ball.basic.desc.1", "br.ball.basic.desc.2", "br.ball.basic.desc.3" },
                // en: 1 damage. | 2 damage. | 3 damage.
                new[] { "br.ball.flame.desc.1", "br.ball.flame.desc.2", "br.ball.flame.desc.3" },
                // en: 1 damage. Burns for 2. | 1 damage. Burns for 3. | 1 damage. Burns for 4. Burn spreads to a neighbour on kill.
                new[] { "br.ball.frost.desc.1", "br.ball.frost.desc.2", "br.ball.frost.desc.3" },
                // en: 1 damage. 35% chance to freeze for a turn. | 1 damage. 50% chance to freeze for a turn. | 1 damage. 65% chance to freeze. Frozen foes take +2.
                new[] { "br.ball.thunder.desc.1", "br.ball.thunder.desc.2", "br.ball.thunder.desc.3" },
                // en: 1 damage. Chains to 2 nearby enemies. | 1 damage. Chains to 3 nearby enemies. | 1 damage. Chains to 4 nearby enemies for 2.
                new[] { "br.ball.bomb.desc.1", "br.ball.bomb.desc.2", "br.ball.bomb.desc.3" },
                // en: First hit blasts a 3×3 area for 3. | First hit blasts a 3×3 area for 4. | First hit blasts a 5×5 cross for 5.
                new[] { "br.ball.splitter.desc.1", "br.ball.splitter.desc.2", "br.ball.splitter.desc.3" },
                // en: First hit splits into 2 minis. | First hit splits into 3 minis. | First hit splits into 3 minis dealing 2 each.
                new[] { "br.ball.piercer.desc.1", "br.ball.piercer.desc.2", "br.ball.piercer.desc.3" },
                // en: Pierces up to 4 enemies for 2 each. | Pierces up to 6 enemies for 2 each. | Pierces up to 8 enemies for 3 each.
                new[] { "br.ball.iron.desc.1", "br.ball.iron.desc.2", "br.ball.iron.desc.3" },
                // en: 3 damage. Slow, fewer bounces. | 4 damage. Slow, fewer bounces. | 5 damage. Slow, fewer bounces.
                new[] { "br.ball.venom.desc.1", "br.ball.venom.desc.2", "br.ball.venom.desc.3" },
                // en: 1 damage. Poison 1. | 1 damage. Poison 2. | 1 damage. Poison 3.
                new[] { "br.ball.vampire.desc.1", "br.ball.vampire.desc.2", "br.ball.vampire.desc.3" },
                // en: 1 damage. Heals 1 per hit (max 2 per shot). | 1 damage. Heals 1 per hit (max 3 per shot). | 1 damage. Heals 1 per hit (max 4 per shot).
                new[] { "br.ball.rubber.desc.1", "br.ball.rubber.desc.2", "br.ball.rubber.desc.3" },
                // en: 1 damage, +1 per wall bounce (max +4). | 1 damage, +1 per wall bounce (max +6). | 1 damage, +1 per wall bounce (max +8).
                new[] { "br.ball.lucky.desc.1", "br.ball.lucky.desc.2", "br.ball.lucky.desc.3" },
                // en: 1 damage. 25% chance to crit ×3. | 1 damage. 35% chance to crit ×3. | 1 damage. 45% chance to crit ×4.
            };

            public static string Name(BallType type) => Names[(int)type];

            public static string Description(BallType type, int level) => Descriptions[(int)type][level - 1];
        }

        public static class Enemy
        {
            // Indexed by (int)EnemyType.
            public static readonly string[] Names =
            {
                "br.enemy.slime.name",        // en: Slime
                "br.enemy.bat.name",          // en: Bat
                "br.enemy.skeleton.name",     // en: Skeleton
                "br.enemy.shieldKnight.name", // en: Shield Knight
                "br.enemy.mage.name",         // en: Imp Mage
                "br.enemy.healer.name",       // en: Shroom
                "br.enemy.bomber.name",       // en: Beetle
                "br.enemy.totem.name",        // en: Totem
                "br.enemy.kingSlime.name",    // en: King Slime
                "br.enemy.boneLich.name",     // en: Bone Lich
                "br.enemy.crystalGolem.name", // en: Crystal Golem
                "br.enemy.boneWall.name",     // en: Bone Wall
            };

            public static string Name(EnemyType type) => Names[(int)type];
        }

        public static class Act
        {
            // Indexed by act index 0..2.
            public static readonly string[] Names =
            {
                "br.act.1.name", // en: Mossy Ruins
                "br.act.2.name", // en: Sunken Crypt
                "br.act.3.name", // en: Crystal Hollow
            };

            public static string Name(int actIndex) => Names[actIndex];
        }

        #endregion
    }
}
