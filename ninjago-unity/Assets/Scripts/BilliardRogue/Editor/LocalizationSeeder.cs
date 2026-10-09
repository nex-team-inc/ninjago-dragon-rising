#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Upserts the designer-edited <see cref="LocalizationCsv.CsvPath"/> into the existing LocalizationTable collection
    /// (TDD §11, research/localization-fonts.md §7). Only br.* keys are touched: rows are added or updated (GetEntry before
    /// AddKey, IsSmart per entry), br.* keys no longer in the file are removed, the starter keys stay. A malformed file is
    /// rejected before anything is written (see <see cref="LocalizationCsv"/>). Sets the Preload flag so synchronous lookups
    /// after init are cheap. Idempotent: an unchanged file dirties no asset. Also reports LocKeys constants without a row
    /// (they would show "No translation found"). Re-run FontAssetsBuilder afterwards when CJK text changed.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.LocalizationSeeder.Run();' --project-path .../Starter
    /// Check: unity command eval 'return Nex.BilliardRogue.Editor.LocalizationSeeder.Verify();' (StringDatabase lookups).
    /// </summary>
    public static class LocalizationSeeder
    {
        static readonly object[] verifyArguments = { "1", "2", "3" };
        const int MaxReportedFailures = 10;

        #region Entry Points

        [MenuItem("Nex/Billiard Rogue/Localization Seeder", priority = 31)]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        /// <summary>Seeds the table; returns a one-line summary with the counts. Throws when the file is rejected.</summary>
        public static string Run()
        {
            var warnings = new List<string>();
            var rows = LocalizationCsv.Load(LocalizationCsv.CsvPath, warnings);
            var summary = new StringBuilder("[LocalizationSeeder] ");
            Upsert(rows, summary);
            AppendKeyCoverage(rows, summary);
            if (warnings.Count > 0)
            {
                summary.Append("; warnings: ").Append(string.Join(" | ", warnings));
            }

            return summary.ToString();
        }

        /// <summary>
        /// Looks every row up through LocalizationSettings.StringDatabase in every locale (smart rows formatted with "1", "2",
        /// "3") and compares with the file; returns "OK n/n" or the first mismatches.
        /// </summary>
        public static string Verify()
        {
            var rows = LocalizationCsv.Load(LocalizationCsv.CsvPath, new List<string>());
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            var database = LocalizationSettings.StringDatabase;
            var checkedCount = 0;
            var failures = new List<string>();
            for (var column = 0; column < LocalizationCsv.LocaleCodes.Length; column++)
            {
                var code = LocalizationCsv.LocaleCodes[column];
                var locale = LocalizationEditorSettings.GetLocale(new LocaleIdentifier(code));
                if (locale == null)
                {
                    throw new InvalidOperationException($"Locale {code} not found");
                }

                foreach (var row in rows)
                {
                    var expected = row.Smart ? Substitute(row.Values[column]) : row.Values[column];
                    var actual = row.Smart
                        ? database.GetLocalizedString(LocKeys.Table, row.Key, locale, FallbackBehavior.DontUseFallback, verifyArguments)
                        : database.GetLocalizedString(LocKeys.Table, row.Key, locale, FallbackBehavior.DontUseFallback);
                    checkedCount++;
                    if (actual == expected || failures.Count >= MaxReportedFailures) continue;
                    failures.Add($"{code} {row.Key}: '{actual}' != '{expected}'");
                }
            }

            return failures.Count == 0
                ? $"[LocalizationSeeder.Verify] OK {checkedCount}/{checkedCount}"
                : "[LocalizationSeeder.Verify] FAIL " + string.Join(" | ", failures);
        }

        #endregion

        #region Table Upsert

        static void Upsert(IReadOnlyList<LocalizationCsv.Row> rows, StringBuilder summary)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocKeys.Table);
            if (collection == null)
            {
                throw new InvalidOperationException($"String table collection '{LocKeys.Table}' not found");
            }

            var shared = collection.SharedData;
            var tables = new StringTable[LocalizationCsv.LocaleCodes.Length];
            for (var i = 0; i < tables.Length; i++)
            {
                var table = collection.GetTable(LocalizationCsv.LocaleCodes[i]) as StringTable;
                if (table == null)
                {
                    throw new InvalidOperationException($"{LocKeys.Table} has no {LocalizationCsv.LocaleCodes[i]} string table");
                }

                tables[i] = table;
            }

            int added = 0, updated = 0, unchanged = 0, smart = 0;
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                wanted.Add(row.Key);
                smart += row.Smart ? 1 : 0;
                var sharedEntry = shared.GetEntry(row.Key); // GetEntry first: AddKey on an existing key creates "key 1"
                var isNew = sharedEntry == null;
                sharedEntry ??= shared.AddKey(row.Key);
                var changed = WriteEntries(tables, sharedEntry.Id, row);
                if (isNew)
                {
                    added++;
                }
                else if (changed)
                {
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }

            var removed = RemoveStale(collection, shared, wanted);
            var preloadChanged = !collection.IsPreloadTableFlagSet();
            if (preloadChanged)
            {
                collection.SetPreloadTableFlag(true);
            }

            var tablesChanged = added + updated + removed > 0;
            if (tablesChanged)
            {
                EditorUtility.SetDirty(shared);
                foreach (var table in tables)
                {
                    EditorUtility.SetDirty(table);
                }

                LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            }

            if (tablesChanged || preloadChanged)
            {
                AssetDatabase.SaveAssets();
            }

            summary.Append($"{rows.Count} rows ({smart} smart): added {added}, updated {updated}, unchanged {unchanged}, removed stale {removed}; per locale");
            for (var i = 0; i < tables.Length; i++)
            {
                summary.Append(' ').Append(LocalizationCsv.LocaleCodes[i]).Append('=').Append(CountOwned(shared, tables[i]));
            }

            summary.Append(preloadChanged ? "; preload flag set" : "; preload flag already set");
        }

        /// <summary>Writes one row into every locale table; true when any value or smart flag changed.</summary>
        static bool WriteEntries(StringTable[] tables, long id, LocalizationCsv.Row row)
        {
            var changed = false;
            for (var i = 0; i < tables.Length; i++)
            {
                var entry = tables[i].GetEntry(id);
                if (entry == null)
                {
                    entry = tables[i].AddEntry(id, row.Values[i]);
                    changed = true;
                }
                else if (entry.Value != row.Values[i])
                {
                    entry.Value = row.Values[i];
                    changed = true;
                }

                if (entry.IsSmart == row.Smart) continue;
                entry.IsSmart = row.Smart;
                changed = true;
            }

            return changed;
        }

        /// <summary>Removes br.* keys that are no longer in the file (never any other key).</summary>
        static int RemoveStale(StringTableCollection collection, SharedTableData shared, HashSet<string> wanted)
        {
            var stale = new List<long>();
            foreach (var entry in shared.Entries)
            {
                if (!entry.Key.StartsWith(LocalizationCsv.OwnedPrefix, StringComparison.Ordinal)) continue;
                if (wanted.Contains(entry.Key)) continue;
                stale.Add(entry.Id);
            }

            foreach (var id in stale)
            {
                collection.RemoveEntry(id);
            }

            return stale.Count;
        }

        static int CountOwned(SharedTableData shared, StringTable table)
        {
            var count = 0;
            foreach (var entry in shared.Entries)
            {
                if (!entry.Key.StartsWith(LocalizationCsv.OwnedPrefix, StringComparison.Ordinal)) continue;
                if (string.IsNullOrEmpty(table.GetEntry(entry.Id)?.Value)) continue;
                count++;
            }

            return count;
        }

        #endregion

        #region LocKeys Coverage

        static void AppendKeyCoverage(IReadOnlyList<LocalizationCsv.Row> rows, StringBuilder summary)
        {
            var declared = new HashSet<string>(StringComparer.Ordinal);
            CollectKeys(typeof(LocKeys), declared);
            var inFile = new HashSet<string>(StringComparer.Ordinal);
            var fileOnly = new List<string>();
            foreach (var row in rows)
            {
                inFile.Add(row.Key);
                if (declared.Contains(row.Key)) continue;
                fileOnly.Add(row.Key);
            }

            var missing = new List<string>();
            foreach (var key in declared)
            {
                if (inFile.Contains(key)) continue;
                missing.Add(key);
            }

            missing.Sort(StringComparer.Ordinal);
            summary.Append($"; LocKeys declared {declared.Count}, missing rows {missing.Count}");
            if (missing.Count > 0)
            {
                summary.Append(" (").Append(string.Join(", ", missing)).Append(')');
                Debug.LogWarning($"[LocalizationSeeder] LocKeys without a row in {LocalizationCsv.CsvPath}: {string.Join(", ", missing)}");
            }

            summary.Append($", file-only {fileOnly.Count}");
            if (fileOnly.Count > 0)
            {
                summary.Append(" (").Append(string.Join(", ", fileOnly)).Append(')');
            }
        }

        /// <summary>Every br.* string in LocKeys: constants plus the static readonly key arrays (balls, enemies, acts).</summary>
        static void CollectKeys(Type type, HashSet<string> keys)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                AddKeys(field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null), keys);
            }

            foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
            {
                CollectKeys(nested, keys);
            }
        }

        static void AddKeys(object? value, HashSet<string> keys)
        {
            switch (value)
            {
                case string key:
                    if (key.StartsWith(LocalizationCsv.OwnedPrefix, StringComparison.Ordinal))
                    {
                        keys.Add(key);
                    }

                    break;
                case IEnumerable list:
                    foreach (var item in list)
                    {
                        AddKeys(item, keys);
                    }

                    break;
            }
        }

        static string Substitute(string value)
        {
            for (var i = 0; i < verifyArguments.Length; i++)
            {
                value = value.Replace("{" + i + "}", (string)verifyArguments[i]);
            }

            return value;
        }

        #endregion
    }
}
