#nullable enable

using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Upserts every ninjago.* line into the LocalizationTable collection (English first pass). Other locales have
    /// no entry yet and fall back to English, so the string database fallback is switched on.
    /// </summary>
    public static class NinjagoLocalizationSeeder
    {
        // (key, English, smart)
        static readonly (string key, string english, bool smart)[] rows =
        {
            ("ninjago.mode.slip_and_spin", "Slip and Spin", false),
            ("ninjago.mode.chest_chase", "Chest Chase", false),
            ("ninjago.players.title", "How many players?", false),
            ("ninjago.players.one", "1 Player", false),
            ("ninjago.players.two", "2 Players", false),
            ("ninjago.player.tag", "P{player}", true),
            ("ninjago.setup.stand", "Stand in the frame", false),
            ("ninjago.setup.hold", "Hold still", false),
            ("ninjago.setup.ready", "Ready!", false),
            ("ninjago.setup.solo", "Only one player found. Starting solo!", false),
            ("ninjago.setup.status_waiting", "Step in", false),
            ("ninjago.setup.status_holding", "Hold still", false),
            ("ninjago.setup.status_ready", "Ready!", false),
            ("ninjago.fight.get_ready", "Get ready!", false),
            ("ninjago.fight.slip", "SLIP", false),
            ("ninjago.fight.spin_hint", "Move your hands!", false),
            ("ninjago.fight.sweep", "Sweep {current}/{total}", true),
            ("ninjago.fight.out", "OUT", false),
            ("ninjago.fight.finished", "Finished!", false),
            ("ninjago.chase.car", "Car", false),
            ("ninjago.chase.skycraft", "Skycraft", false),
            ("ninjago.chase.lean_to_steer", "Lean to steer", false),
            ("ninjago.chase.whole_chest", "Use your whole chest", false),
            ("ninjago.chase.dodges", "Dodges {count}", true),
            ("ninjago.chase.hits", "Hits {count}", true),
            ("ninjago.result.victory", "Victory!", false),
            ("ninjago.result.defeat", "Defeated", false),
            ("ninjago.result.chase_title", "Ride complete!", false),
            ("ninjago.result.fight_player", "P{player}    Slips {slips}/{sweeps}    Fight-backs {backs}/{sweeps}    Hearts {hearts}/{maxHearts}", true),
            ("ninjago.result.fight_best", "Best    Slips {slips}    Fight-backs {backs}    Hearts {hearts}", true),
            ("ninjago.result.chase_car", "Car    Dodges {dodges}    Hits {hits}", true),
            ("ninjago.result.chase_sky", "Skycraft    Dodges {dodges}    Hits {hits}", true),
            ("ninjago.result.chase_steer", "P{player} steered {share}%", true),
            ("ninjago.result.chase_best", "Best    Car dodges {car}    Sky dodges {sky}", true),
            ("ninjago.result.new_best", "New best!", false),
            ("ninjago.result.retry", "Retry", false),
            ("ninjago.result.menu", "Menu", false),
            ("ninjago.mode.stone_kick", "Stone Kick", false),
            ("ninjago.mode.earth_seal", "Earth Seal", false),
            ("ninjago.setup.hands", "Raise a hand into your circle", false),
            ("ninjago.kick.kick", "KICK", false),
            ("ninjago.kick.count", "{count}/{total}", true),
            ("ninjago.kick.throw", "Throw {current}/{total}", true),
            ("ninjago.kick.slash_hint", "Slash the rock!", false),
            ("ninjago.seal.wave", "Wave {current}/{total}", true),
            ("ninjago.seal.wave_banner", "Wave {current}", true),
            ("ninjago.seal.hold_hint", "Hold a hand on the cracks!", false),
            ("ninjago.result.kick_player", "P{player}    Slashes {slashes}/{throws}    Full returns {returns}/{throws}    Hearts {hearts}/{maxHearts}", true),
            ("ninjago.result.kick_kicks", "P{player}    Kicks {kicks}/{possible}    Avg gap {gap:0.00} s", true),
            ("ninjago.result.kick_kicks_no_gap", "P{player}    Kicks {kicks}/{possible}    Avg gap -", true),
            ("ninjago.result.kick_best", "Best    Slashes {slashes}    Full returns {returns}    Kicks {kicks}", true),
            ("ninjago.result.seal_held", "The wall held!", false),
            ("ninjago.result.seal_broke", "The wall broke!", false),
            ("ninjago.result.seal_wall", "Seals {seals}    Breakthroughs {breaks}    Hearts {hearts}/{maxHearts}", true),
            ("ninjago.result.seal_team", "Two-hand seals {twoHands}    Dropped seals {drops}", true),
            ("ninjago.result.seal_player", "P{player}    Held {held} seals    On cracks {seconds:0.0} s    Dropped {drops}", true),
            ("ninjago.result.seal_best", "Best    Seals {seals}    Two-hand seals {twoHands}", true),
        };

        #region Entry Point

        public static string Run()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(NinjagoEditorUtils.TableName);
            var shared = collection.SharedData;
            var english = (StringTable)collection.GetTable("en");
            foreach (var (key, value, smart) in rows)
            {
                var sharedEntry = shared.GetEntry(key) ?? shared.AddKey(key);
                var entry = english.GetEntry(sharedEntry.Id) ?? english.AddEntry(sharedEntry.Id, value);
                if (entry.Value != value) entry.Value = value;
                if (entry.IsSmart != smart) entry.IsSmart = smart;
            }

            var database = LocalizationEditorSettings.ActiveLocalizationSettings.GetStringDatabase();
            database.UseFallback = true;
            EditorUtility.SetDirty(LocalizationEditorSettings.ActiveLocalizationSettings);
            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(english);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            AssetDatabase.SaveAssets();
            return $"localization {rows.Length} keys";
        }

        #endregion
    }
}
