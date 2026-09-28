#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
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

        public PlayerPreference PlayerPreference { get; private set; } = null!;

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

        public DebugSettings DebugSettings { get; private set; } = null!;

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

        #region Billiard Rogue Run & Meta Progress

        // Runs are saved at turn boundaries only (no in-flight balls); the file is rewritten on every save.
        const string runSaveKey = "billiardRogueRun";
        const string metaProgressKey = "billiardRogueMeta";

        MetaProgressData? metaProgress;
#if DISABLE_PERSISTENCE
        RunState? memoryRun;
#endif

        /// <summary>Loaded lazily; falls back to defaults (without overwriting the file) when unreadable.</summary>
        public MetaProgressData MetaProgress => metaProgress ??= LoadMetaProgress();

        public void SaveMetaProgress()
        {
#if !DISABLE_PERSISTENCE
            ES3.Save(metaProgressKey, MetaProgress);
#endif
        }

        public void ResetMetaProgress()
        {
            metaProgress = new MetaProgressData();
            SaveMetaProgress();
        }

        public bool HasRunSave()
        {
#if DISABLE_PERSISTENCE
            return memoryRun != null;
#else
            return ES3.KeyExists(runSaveKey);
#endif
        }

        /// <summary>The saved run or null. An unreadable save is deleted so HasRunSave() stops reporting it.</summary>
        public RunState? LoadRun()
        {
#if DISABLE_PERSISTENCE
            return memoryRun == null ? null : Copy(memoryRun);
#else
            try
            {
                return ES3.KeyExists(runSaveKey) ? ES3.Load<RunState>(runSaveKey) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerDataManager] Discarding unreadable run save: {e}");
                ES3.DeleteKey(runSaveKey);
                return null;
            }
#endif
        }

        public void SaveRun(RunState run)
        {
#if DISABLE_PERSISTENCE
            // A copy, like the file: the live RunState keeps mutating after the turn-boundary save.
            memoryRun = Copy(run);
#else
            ES3.Save(runSaveKey, run);
#endif
        }

        public void ClearRun()
        {
#if DISABLE_PERSISTENCE
            memoryRun = null;
#else
            ES3.DeleteKey(runSaveKey);
#endif
        }

#if DISABLE_PERSISTENCE
        static RunState Copy(RunState run) => JsonUtility.FromJson<RunState>(JsonUtility.ToJson(run));
#endif

        static MetaProgressData LoadMetaProgress()
        {
#if DISABLE_PERSISTENCE
            return new MetaProgressData();
#else
            try
            {
                return ES3.Load(metaProgressKey, new MetaProgressData());
            }
            catch (Exception e)
            {
                // Keep the file for diagnosis until the next real save.
                Debug.LogError($"[PlayerDataManager] Meta progress unreadable, using defaults: {e}");
                return new MetaProgressData();
            }
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
