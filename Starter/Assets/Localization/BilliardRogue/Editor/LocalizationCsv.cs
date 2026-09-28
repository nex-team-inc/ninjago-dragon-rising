#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Reads and validates BilliardRogueStrings.csv (columns key, en, fr-CA, zh-Hans, zh-Hant, ja, isSmart; rows whose first
    /// cell starts with # are comments). <see cref="Load"/> throws with every problem found, so a malformed file never
    /// half-writes the string tables: empty value, duplicate or non-br.* key, wrong column count, placeholders that differ
    /// between locales, or placeholders on a non-smart row. fr-CA values get a typography pass (no-break space before
    /// : and % and inside « », typographic apostrophe) so designers can type plain spaces.
    /// </summary>
    public static class LocalizationCsv
    {
        public const string CsvPath = "Assets/Localization/BilliardRogue/BilliardRogueStrings.csv";
        public const string OwnedPrefix = "br.";
        public static readonly string[] LocaleCodes = { "en", "fr-CA", "zh-Hans", "zh-Hant", "ja" };

        static readonly string[] header = { "key", "en", "fr-CA", "zh-Hans", "zh-Hant", "ja", "isSmart" };
        const int FrenchIndex = 1;
        const char NoBreakSpace = ' ';

        public sealed class Row
        {
            public string Key = "";
            public string[] Values = Array.Empty<string>(); // LocaleCodes order
            public bool Smart;
            public int Line;
        }

        #region Loading

        public static List<Row> Load(string path, List<string> warnings)
        {
            var fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"[LocalizationCsv] {path} not found", fullPath);
            }

            var records = Parse(File.ReadAllText(fullPath, Encoding.UTF8).TrimStart('﻿'));
            var errors = new List<string>();
            var rows = new List<Row>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var headerSeen = false;
            foreach (var (cells, line) in records)
            {
                if (IsBlankOrComment(cells)) continue;
                if (!headerSeen)
                {
                    headerSeen = true;
                    if (!IsHeader(cells))
                    {
                        errors.Add($"line {line}: header must be {string.Join(",", header)}");
                    }

                    continue;
                }

                var row = ParseRow(cells, line, errors, warnings);
                if (row == null) continue;
                if (!keys.Add(row.Key))
                {
                    errors.Add($"line {line}: duplicate key {row.Key}");
                }

                rows.Add(row);
            }

            if (!headerSeen)
            {
                errors.Add("no header row");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"[LocalizationCsv] {path} rejected, nothing written: " + string.Join(" | ", errors));
            }

            return rows;
        }

        static bool IsBlankOrComment(List<string> cells)
        {
            var first = cells[0].Trim();
            if (first.StartsWith("#", StringComparison.Ordinal)) return true;
            if (first.Length > 0) return false;
            foreach (var cell in cells)
            {
                if (cell.Trim().Length > 0) return false;
            }

            return true;
        }

        static bool IsHeader(List<string> cells)
        {
            if (cells.Count < header.Length) return false;
            for (var i = 0; i < header.Length; i++)
            {
                if (!string.Equals(cells[i].Trim(), header[i], StringComparison.OrdinalIgnoreCase)) return false;
            }

            return true;
        }

        static Row? ParseRow(List<string> cells, int line, List<string> errors, List<string> warnings)
        {
            var key = cells[0].Trim();
            if (!HasColumnCount(cells))
            {
                errors.Add($"line {line} ({key}): {cells.Count} columns, expected {header.Length}");
                return null;
            }

            if (!key.StartsWith(OwnedPrefix, StringComparison.Ordinal) || key.IndexOf(' ') >= 0)
            {
                errors.Add($"line {line}: key '{key}' must start with {OwnedPrefix} and contain no spaces");
                return null;
            }

            var row = new Row { Key = key, Line = line, Values = new string[LocaleCodes.Length] };
            for (var i = 0; i < LocaleCodes.Length; i++)
            {
                var value = cells[i + 1].Trim(' ', '\t');
                if (i == FrenchIndex)
                {
                    value = FrenchTypography(value);
                }

                if (value.Length == 0)
                {
                    errors.Add($"line {line} ({key}): empty {LocaleCodes[i]} value");
                }

                row.Values[i] = value;
            }

            if (!TryParseBool(cells[header.Length - 1].Trim(), out row.Smart))
            {
                errors.Add($"line {line} ({key}): isSmart must be true or false");
            }

            ValidatePlaceholders(row, errors, warnings);
            return row;
        }

        /// <summary>Exactly the header's column count; trailing empty cells (spreadsheet exports) are tolerated.</summary>
        static bool HasColumnCount(List<string> cells)
        {
            if (cells.Count < header.Length) return false;
            for (var i = header.Length; i < cells.Count; i++)
            {
                if (cells[i].Trim().Length > 0) return false;
            }

            return true;
        }

        #endregion

        #region Validation

        static void ValidatePlaceholders(Row row, List<string> errors, List<string> warnings)
        {
            var reference = Placeholders(row.Values[0]);
            if (reference == null)
            {
                errors.Add($"line {row.Line} ({row.Key}): unbalanced braces in en");
                return;
            }

            for (var i = 1; i < LocaleCodes.Length; i++)
            {
                var placeholders = Placeholders(row.Values[i]);
                if (placeholders == reference) continue;
                errors.Add($"line {row.Line} ({row.Key}): {LocaleCodes[i]} placeholders [{placeholders ?? "unbalanced"}] differ from en [{reference}]");
            }

            if (!row.Smart && reference.Length > 0)
            {
                errors.Add($"line {row.Line} ({row.Key}): has placeholders but isSmart is false (braces would print literally)");
            }

            if (row.Smart && reference.Length == 0)
            {
                warnings.Add($"{row.Key}: isSmart without placeholders");
            }
        }

        /// <summary>Sorted, comma-joined {…} tokens of a value; null when braces do not pair up.</summary>
        static string? Placeholders(string value)
        {
            var tokens = new List<string>();
            var start = -1;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '{')
                {
                    if (start >= 0)
                    {
                        return null;
                    }

                    start = i;
                }
                else if (value[i] == '}')
                {
                    if (start < 0)
                    {
                        return null;
                    }

                    tokens.Add(value.Substring(start, i - start + 1));
                    start = -1;
                }
            }

            if (start >= 0)
            {
                return null;
            }

            tokens.Sort(StringComparer.Ordinal);
            return string.Join(",", tokens);
        }

        static string FrenchTypography(string value) =>
            value.Replace(" :", NoBreakSpace + ":")
                .Replace(" %", NoBreakSpace + "%")
                .Replace("« ", "«" + NoBreakSpace)
                .Replace(" »", NoBreakSpace + "»")
                .Replace('\'', '’');

        static bool TryParseBool(string text, out bool value)
        {
            switch (text.ToLowerInvariant())
            {
                case "true" or "1" or "yes":
                    value = true;
                    return true;
                case "false" or "0" or "no":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        #endregion

        #region CSV Reader

        /// <summary>RFC 4180: quoted fields may hold commas, doubled quotes and line breaks; a quote inside an unquoted field is literal.</summary>
        static List<(List<string> Cells, int Line)> Parse(string text)
        {
            var records = new List<(List<string>, int)>();
            var cells = new List<string>();
            var cell = new StringBuilder();
            var inQuotes = false;
            var fieldStart = true;
            var line = 1;
            var recordLine = 1;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        if (c == '\n')
                        {
                            line++;
                        }

                        cell.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"' when fieldStart:
                        inQuotes = true;
                        fieldStart = false;
                        break;
                    case ',':
                        cells.Add(cell.ToString());
                        cell.Clear();
                        fieldStart = true;
                        break;
                    case '\r':
                        break;
                    case '\n':
                        cells.Add(cell.ToString());
                        records.Add((cells, recordLine));
                        cells = new List<string>();
                        cell.Clear();
                        fieldStart = true;
                        line++;
                        recordLine = line;
                        break;
                    default:
                        cell.Append(c);
                        fieldStart = false;
                        break;
                }
            }

            if (cell.Length > 0 || cells.Count > 0)
            {
                cells.Add(cell.ToString());
                records.Add((cells, recordLine));
            }

            return records;
        }

        #endregion
    }
}
