#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Applies the saved locale (PlayerPreference.localeCode) at boot, or maps the system language onto one of the
    /// project locales when nothing was saved. The Localization package only walks parent locales, so fr-FR or
    /// zh-CN would otherwise fall back to English.
    /// </summary>
    public static class LocaleRestore
    {
        public static async UniTask ApplyAsync(PlayerPreference preference, CancellationToken ct)
        {
            await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: ct);

            var code = preference.localeCode;
            if (string.IsNullOrEmpty(code))
            {
                code = FromSystemLanguage(Application.systemLanguage);
            }

            if (code == null) return;

            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale == null)
            {
                Debug.LogWarning($"[LocaleRestore] Locale '{code}' is not in the project; keeping the default selector.");
                return;
            }

            if (LocalizationSettings.SelectedLocale != locale)
            {
                LocalizationSettings.SelectedLocale = locale;
            }
        }

        /// <summary>Project locale code for a system language, or null to keep what the system selector picked.</summary>
        public static string? FromSystemLanguage(SystemLanguage language)
        {
            return language switch
            {
                SystemLanguage.French => "fr-CA",
                SystemLanguage.Japanese => "ja",
                SystemLanguage.ChineseTraditional => "zh-Hant",
                SystemLanguage.ChineseSimplified or SystemLanguage.Chinese => "zh-Hans",
                _ => null,
            };
        }
    }
}
