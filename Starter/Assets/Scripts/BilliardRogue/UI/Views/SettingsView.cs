#nullable enable

using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Language, master/music/SFX volume, aim guide length, left-handed cue and screen shake. Up/Down picks a row,
    /// Left/Right (or Enter) changes it. Changes apply and persist immediately (volumes through PlayerDataManager's
    /// reactive properties, which VolumeManager applies and saves); Back just closes.
    /// </summary>
    public sealed class SettingsView : RogueView
    {
        const string LanguageKeyPrefix = "br.ui.settings.language.";
        const float VolumeStep = 0.1f;
        const int AimGuideOptions = 3;

        [Header("Rows")]
        [SerializeField] SettingRow languageRow = null!;
        [SerializeField] SettingRow masterRow = null!;
        [SerializeField] SettingRow musicRow = null!;
        [SerializeField] SettingRow sfxRow = null!;
        [SerializeField] SettingRow aimGuideRow = null!;
        [SerializeField] SettingRow leftHandedRow = null!;
        [SerializeField] SettingRow screenShakeRow = null!;

        static readonly string[] aimGuideKeys =
        {
            LocKeys.Settings.AimGuideShort, LocKeys.Settings.AimGuideNormal, LocKeys.Settings.AimGuideLong,
        };

        /// <summary>Setting name (analytics id) after it changed; gameplay re-reads PlayerPreference.</summary>
        public event Action<string>? SettingChanged;

        public override ViewIdentifier Identifier => ViewIdentifier.Settings;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "settings";

        #region Life Cycle

        protected override void Awake()
        {
            base.Awake();
            languageRow.Stepped += HandleLanguage;
            masterRow.Stepped += HandleVolume;
            musicRow.Stepped += HandleVolume;
            sfxRow.Stepped += HandleVolume;
            aimGuideRow.Stepped += HandleAimGuide;
            leftHandedRow.Stepped += HandleToggle;
            screenShakeRow.Stepped += HandleToggle;
            RefreshAll();
        }

        public override void ViewDidBecomeTopView(bool afterPush)
        {
            base.ViewDidBecomeTopView(afterPush);
            RefreshAll();
        }

        public override void OnBackButton()
        {
            if (!IsActive) return;
            TrackBack();
            base.OnBackButton();
        }

        #endregion

        #region Row handlers

        void HandleLanguage(SettingRow row, int direction)
        {
            if (!IsActive) return;
            if (!LocalizationSettings.InitializationOperation.IsDone) return;
            var locales = LocalizationSettings.AvailableLocales.Locales;
            if (locales.Count < 2) return;
            var index = Mathf.Max(0, locales.IndexOf(LocalizationSettings.SelectedLocale));
            var next = locales[(index + direction + locales.Count) % locales.Count];
            LocalizationSettings.SelectedLocale = next;
            var code = next.Identifier.Code;
            PlayerDataManager.Instance.ScopedPlayerPreferenceUpdate(preference => preference.localeCode = code);
            Changed("language", code);
            // Not RefreshLanguage: the locale change restarts InitializationOperation, so its IsDone gate would skip
            // the label and leave the previous language's name on the row.
            ShowLanguage(next);
        }

        void HandleVolume(SettingRow row, int direction)
        {
            if (!IsActive) return;
            var data = PlayerDataManager.Instance;
            var property = row == masterRow ? data.MasterVolumeProperty : row == musicRow ? data.BgmVolumeProperty : data.SfxVolumeProperty;
            var value = Mathf.Clamp01(Mathf.Round((property.Value + direction * VolumeStep) * 10f) / 10f);
            if (Mathf.Approximately(value, property.Value)) return;
            property.Value = value;
            row.SetFraction(value);
            Changed(row == masterRow ? "master_volume" : row == musicRow ? "music_volume" : "sfx_volume", value);
        }

        void HandleAimGuide(SettingRow row, int direction)
        {
            if (!IsActive) return;
            var current = PlayerDataManager.Instance.PlayerPreference.aimGuideLength;
            var value = (current + direction + AimGuideOptions) % AimGuideOptions;
            PlayerDataManager.Instance.ScopedPlayerPreferenceUpdate(preference => preference.aimGuideLength = value);
            RefreshAimGuide();
            Changed("aim_guide_length", value);
        }

        void HandleToggle(SettingRow row, int direction)
        {
            if (!IsActive) return;
            var leftHanded = row == leftHandedRow;
            var preference = PlayerDataManager.Instance.PlayerPreference;
            var value = !(leftHanded ? preference.leftHandedCue : preference.screenShake);
            PlayerDataManager.Instance.ScopedPlayerPreferenceUpdate(p =>
            {
                if (leftHanded)
                {
                    p.leftHandedCue = value;
                }
                else
                {
                    p.screenShake = value;
                }
            });
            RefreshToggle(row, value);
            Changed(leftHanded ? "left_handed_cue" : "screen_shake", value ? "on" : "off");
        }

        #endregion

        #region Helpers

        void Changed(string setting, string value)
        {
            RunAnalytics.SettingChanged(setting, value);
            UiTheme.PlaySfx(theme.MoveSfx);
            SettingChanged?.Invoke(setting);
        }

        void Changed(string setting, float value)
        {
            RunAnalytics.SettingChanged(setting, value);
            UiTheme.PlaySfx(theme.MoveSfx);
            SettingChanged?.Invoke(setting);
        }

        void RefreshAll()
        {
            var data = PlayerDataManager.Instance;
            RefreshLanguage();
            masterRow.SetFraction(data.MasterVolumeProperty.Value);
            musicRow.SetFraction(data.BgmVolumeProperty.Value);
            sfxRow.SetFraction(data.SfxVolumeProperty.Value);
            RefreshAimGuide();
            RefreshToggle(leftHandedRow, data.PlayerPreference.leftHandedCue);
            RefreshToggle(screenShakeRow, data.PlayerPreference.screenShake);
        }

        void RefreshLanguage()
        {
            if (!LocalizationSettings.InitializationOperation.IsDone) return;
            var locale = LocalizationSettings.SelectedLocale;
            if (locale == null) return;
            ShowLanguage(locale);
        }

        void ShowLanguage(Locale locale)
        {
            languageRow.ValueLabel!.SetKey(LanguageKeyPrefix + locale.Identifier.Code);
        }

        void RefreshAimGuide()
        {
            var index = Mathf.Clamp(PlayerDataManager.Instance.PlayerPreference.aimGuideLength, 0, AimGuideOptions - 1);
            aimGuideRow.ValueLabel!.SetKey(aimGuideKeys[index]);
        }

        void RefreshToggle(SettingRow row, bool on)
        {
            var label = row.ValueLabel!;
            label.SetKey(on ? LocKeys.Settings.On : LocKeys.Settings.Off);
            label.Color = on ? theme.Positive : theme.TextMuted;
        }

        #endregion
    }
}
