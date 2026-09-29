#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Localized floating-text strings resolved once per locale (LocKeys.Float), including pre-formatted combo and
    /// heal texts, so gameplay never formats or looks up strings while balls fly. Every string comes from the
    /// string table: until it is loaded (or for a key it lacks, which is logged) the text is empty, never English.
    /// </summary>
    public sealed class FloatTextCache : IDisposable
    {
        public const int MaxCached = 64;

        static readonly string[] PlainKeys =
        {
            LocKeys.Float.Block, LocKeys.Float.Crit, LocKeys.Float.Power, LocKeys.Float.Frozen, LocKeys.Float.Miss,
            LocKeys.Float.Split, LocKeys.Float.Enraged, LocKeys.Float.Quake, LocKeys.Float.Shield, LocKeys.Float.Summon,
            LocKeys.Float.Warp, LocKeys.Float.ExtraBall, LocKeys.Float.PowerUp,
        };

        readonly string[] plain = new string[PlainKeys.Length];
        readonly string[] combo = new string[MaxCached + 1];
        readonly string[] heal = new string[MaxCached + 1];
        readonly CancellationToken token;
        bool disposed;

        public string Block => plain[0];
        public string Crit => plain[1];
        public string Power => plain[2];
        public string Frozen => plain[3];
        public string Miss => plain[4];
        public string Split => plain[5];
        public string Enraged => plain[6];
        public string Quake => plain[7];
        public string Shield => plain[8];
        public string Summon => plain[9];
        public string Warp => plain[10];
        public string ExtraBall => plain[11];
        public string PowerUp => plain[12];

        #region Life Cycle

        public FloatTextCache(CancellationToken cancellationToken)
        {
            token = cancellationToken;
            Array.Fill(plain, "");
            Array.Fill(combo, "");
            Array.Fill(heal, "");
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            RefreshAsync().Forget();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        #endregion

        #region Public Methods

        public string Combo(int count) => combo[Mathf.Clamp(count, 0, MaxCached)];

        public string Heal(int amount) => heal[Mathf.Clamp(amount, 0, MaxCached)];

        #endregion

        #region Helpers

        void HandleLocaleChanged(Locale _)
        {
            RefreshAsync().Forget();
        }

        async UniTask RefreshAsync()
        {
            try
            {
                await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: token);
                var table = await LocalizationSettings.StringDatabase.GetTableAsync(LocKeys.Table).ToUniTask(cancellationToken: token);
                if (disposed || table == null) return;
                for (var i = 0; i < PlainKeys.Length; i++)
                {
                    var entry = Entry(table, PlainKeys[i]);
                    plain[i] = entry == null ? "" : entry.GetLocalizedString();
                }

                var comboEntry = Entry(table, LocKeys.Float.Combo);
                var healEntry = Entry(table, LocKeys.Float.Heal);
                for (var n = 0; n <= MaxCached; n++)
                {
                    combo[n] = comboEntry == null ? "" : comboEntry.GetLocalizedString(n);
                    heal[n] = healEntry == null ? "" : healEntry.GetLocalizedString(n);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        // The seeder writes every key into all locales; a missing one is a build error, not a reason to show English.
        static StringTableEntry? Entry(StringTable table, string key)
        {
            var entry = table.GetEntry(key);
            if (entry == null) Debug.LogError($"[FloatTextCache] Missing localization entry '{key}' in '{table.LocaleIdentifier}'.");
            return entry;
        }

        #endregion
    }
}
