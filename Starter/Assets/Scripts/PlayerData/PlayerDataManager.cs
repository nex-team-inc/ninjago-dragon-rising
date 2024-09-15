using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Util;
using Nex.Util.Attributes;
using UnityEngine;

namespace Nex
{
    public class PlayerDataManager : Singleton<PlayerDataManager>
    {
        #region User Data

        [SerializeField, RealtimeReactiveProperty] AsyncReactiveProperty<float> masterVolumeProperty = new(0);
        [SerializeField, RealtimeReactiveProperty] AsyncReactiveProperty<float> sfxVolumeProperty = new(0);
        [SerializeField, RealtimeReactiveProperty] AsyncReactiveProperty<float> bgmVolumeProperty = new(0);

        public IAsyncReactiveProperty<float> MasterVolumeProperty => masterVolumeProperty;
        public IAsyncReactiveProperty<float> SfxVolumeProperty => sfxVolumeProperty;
        public IAsyncReactiveProperty<float> BgmVolumeProperty => bgmVolumeProperty;

        #endregion

        #region Life Cycle

        protected override PlayerDataManager GetThis()
        {
            return this;
        }

        protected override void Awake()
        {
            base.Awake();

            DebugSettings = new DebugSettings();
            PlayerPreference = new PlayerPreference();

            // This is whether we initialize the settings.
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            LoadDebugSettings();
#endif

            LoadPlayerPreference();

            InitializeProperties();
        }

        #endregion

        #region Bindings

        void InitializeProperties()
        {
            var preference = PlayerPreference;
            masterVolumeProperty.Value = preference.masterVolume;
            sfxVolumeProperty.Value = preference.sfxVolume;
            bgmVolumeProperty.Value = preference.bgmVolume;
        }

        public void InstallTemporaryProperties(AsyncReactiveProperty<float> tempMasterVolumeProperty,
            AsyncReactiveProperty<float> tempSfxVolumeProperty, AsyncReactiveProperty<float> tempBgmVolumeProperty,
            CancellationToken cancellationToken)
        {
            tempMasterVolumeProperty.BindTo(masterVolumeProperty, cancellationToken);
            tempSfxVolumeProperty.BindTo(sfxVolumeProperty, cancellationToken);
            tempBgmVolumeProperty.BindTo(bgmVolumeProperty, cancellationToken);
        }

        #endregion

        #region Player Preference

        const string preferenceDataKey = "playerPreferenceData";

        public PlayerPreference PlayerPreference { get; private set; }

        public void ResetPlayerPreference()
        {
            PlayerPreference = new PlayerPreference();
#if !DISABLE_PERSISTENCE
            ES3.Save(preferenceDataKey, PlayerPreference);
#endif
        }

        public void SavePlayerPreference()
        {
#if !DISABLE_PERSISTENCE
            ES3.Save(preferenceDataKey, PlayerPreference);
#endif
        }

        void LoadPlayerPreference()
        {
            try
            {
#if DISABLE_PERSISTENCE
                PlayerPreference = new PlayerPreference();
#else
                PlayerPreference = ES3.Load<PlayerPreference>(preferenceDataKey) ?? new PlayerPreference();
#endif
            }
            catch
            {
                // Force initialize a blank Player Preference.
                ResetPlayerPreference();
            }
        }

        public void ScopedPlayerPreferenceUpdate(Action<PlayerPreference> modifier)
        {
            modifier(PlayerPreference);
            SavePlayerPreference();
        }

        #endregion

        #region Debug Settings / Function

        const string debugSettingsDataKey = "debugSettingsData";

        public DebugSettings DebugSettings { get; private set; }

        public void SaveDebugSettings()
        {
#if !DISABLE_PERSISTENCE
            ES3.Save(debugSettingsDataKey, DebugSettings);
#endif
        }

        void LoadDebugSettings()
        {
            try
            {
#if DISABLE_PERSISTENCE
                DebugSettings = new DebugSettings();
#else
                DebugSettings = ES3.Load<DebugSettings>(debugSettingsDataKey) ?? new DebugSettings();
#endif
            }
            catch
            {
                DebugSettings = new DebugSettings();
            }
        }

        void ResetDebugSettings()
        {
            DebugSettings = new DebugSettings();
#if !DISABLE_PERSISTENCE
            ES3.Save(debugSettingsDataKey, DebugSettings);
#endif
        }

        #endregion

        #region App View State

        public class AppViewState : AbstractViewState
        {
            public override View.ViewIdentifier ViewIdentifier => View.ViewIdentifier.Empty;

            public bool enableHighlighting = true;
        }

        // The app view state stores view state information that can be restored after coming back from the game scene.
        // It also supports some app state persistence, like last selected game mode / difficulty.
        public readonly AppViewState appViewState = new();

        #endregion

    }
}
