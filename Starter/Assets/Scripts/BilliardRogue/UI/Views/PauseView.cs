#nullable enable

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Pause overlay: Resume / Settings / Save &amp; Quit over a dim layer. Back (remote/Escape) resumes. Reports
    /// through the ViewManager pause announcements (the opener announces Paused before pushing this view);
    /// Save &amp; Quit only announces — the coordinator unwinds the stack.
    /// </summary>
    public sealed class PauseView : RogueView
    {
        [Header("Menu")]
        [SerializeField] UnityEngine.UI.Button resumeButton = null!;
        [SerializeField] UnityEngine.UI.Button settingsButton = null!;
        [SerializeField] UnityEngine.UI.Button saveQuitButton = null!;
        [SerializeField] SettingsView settingsViewPrefab = null!;

        bool presented;
        bool leaving;

        /// <summary>Relayed from the Settings view opened from here (setting analytics id).</summary>
        public event Action<string>? SettingChanged;

        public override ViewIdentifier Identifier => ViewIdentifier.Pause;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "pause";

        protected override void Awake()
        {
            base.Awake();
            resumeButton.onClick.AddListener(HandleResume);
            settingsButton.onClick.AddListener(HandleSettings);
            saveQuitButton.onClick.AddListener(HandleSaveQuit);
        }

        public override void ViewDidBecomeTopView(bool afterPush)
        {
            base.ViewDidBecomeTopView(afterPush);
            if (presented) return;
            presented = true;
            UiTheme.PlaySfx(theme.PauseSfx);
        }

        public override void OnBackButton()
        {
            if (!IsActive) return;
            TrackBack();
            Resume();
        }

        void HandleResume()
        {
            if (!IsActive || leaving) return;
            TrackButton("resume");
            Resume();
        }

        void Resume()
        {
            if (leaving) return;
            leaving = true;
            UiTheme.PlaySfx(theme.ResumeSfx);
            Manager.AnnouncePauseViewResumeClicked();
            PopSelf().Forget();
        }

        void HandleSettings()
        {
            if (!IsActive || leaving) return;
            TrackButton("settings");
            var settings = Instantiate(settingsViewPrefab);
            settings.SettingChanged += RelaySettingChanged;
            PushView(settings).Forget();
        }

        void HandleSaveQuit()
        {
            if (!IsActive || leaving) return;
            leaving = true;
            TrackButton("save_quit");
            Manager.AnnouncePauseViewHomeClicked();
        }

        void RelaySettingChanged(string setting) => SettingChanged?.Invoke(setting);

    }
}
