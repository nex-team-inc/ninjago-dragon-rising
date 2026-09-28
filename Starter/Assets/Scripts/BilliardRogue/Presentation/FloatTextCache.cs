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
    /// heal texts, so gameplay never formats or looks up strings while balls fly. English copy is the fallback.
    /// </summary>
    public sealed class FloatTextCache : IDisposable
    {
        public const int MaxCached = 64;

        static readonly (string key, string english)[] Plain =
        {
            (LocKeys.Float.Block, "BLOCK"), (LocKeys.Float.Crit, "CRIT!"), (LocKeys.Float.Power, "POWER!"),
            (LocKeys.Float.Frozen, "FROZEN"), (LocKeys.Float.Miss, "MISS"), (LocKeys.Float.Split, "SPLIT!"),
            (LocKeys.Float.Enraged, "ENRAGED!"), (LocKeys.Float.Quake, "QUAKE!"), (LocKeys.Float.Shield, "SHIELD"),
            (LocKeys.Float.Summon, "SUMMON!"), (LocKeys.Float.Warp, "WARP!"), (LocKeys.Float.ExtraBall, "+1 BALL"),
            (LocKeys.Float.PowerUp, "POWER UP"),
        };

        readonly string[] plain = new string[Plain.Length];
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
            FillEnglish();
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

        void FillEnglish()
        {
            for (var i = 0; i < Plain.Length; i++)
            {
                plain[i] = Plain[i].english;
            }

            for (var n = 0; n <= MaxCached; n++)
            {
                combo[n] = "x" + n + " COMBO";
                heal[n] = "+" + n;
            }
        }

        async UniTask RefreshAsync()
        {
            try
            {
                await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: token);
                var table = await LocalizationSettings.StringDatabase.GetTableAsync(LocKeys.Table).ToUniTask(cancellationToken: token);
                if (disposed || table == null) return;
                for (var i = 0; i < Plain.Length; i++)
                {
                    var entry = table.GetEntry(Plain[i].key);
                    if (entry != null) plain[i] = entry.GetLocalizedString();
                }

                var comboEntry = table.GetEntry(LocKeys.Float.Combo);
                var healEntry = table.GetEntry(LocKeys.Float.Heal);
                for (var n = 0; n <= MaxCached; n++)
                {
                    if (comboEntry != null) combo[n] = Format(comboEntry, n, combo[n]);
                    if (healEntry != null) heal[n] = Format(healEntry, n, heal[n]);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        static string Format(StringTableEntry entry, int n, string fallback)
        {
            var value = entry.GetLocalizedString(n);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        #endregion
    }
}
