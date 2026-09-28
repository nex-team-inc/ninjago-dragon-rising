#nullable enable

using Cysharp.Threading.Tasks.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace Nex
{
    // Single owner of the mixer volumes: applies PlayerDataManager's volume properties and writes changes back
    // into PlayerPreference (saved after a short debounce so slider drags do not rewrite the file per tick).
    public class VolumeManager  : Singleton<VolumeManager>
    {
        [SerializeField] AudioMixer audioMixer = null!;
        [Tooltip("Seconds after the last volume change before the preference file is written.")]
        [SerializeField, Range(0.1f, 5f)] float saveDebounceSeconds = 0.5f;

        bool saveQueued;
        float saveDueTime;

        protected override VolumeManager GetThis()
        {
            return this;
        }

        #region Life Cycle

        // Start, not Awake: mixer SetFloat during Awake is overwritten by the snapshot init, and the sibling
        // PlayerDataManager may not be awake yet.
        void Start()
        {
            var data = PlayerDataManager.Instance;
            data.MasterVolumeProperty.Subscribe(HandleMasterVolume, destroyCancellationToken);
            data.SfxVolumeProperty.Subscribe(HandleSfxVolume, destroyCancellationToken);
            data.BgmVolumeProperty.Subscribe(HandleBgmVolume, destroyCancellationToken);
        }

        void Update()
        {
            if (!saveQueued || Time.unscaledTime < saveDueTime) return;
            saveQueued = false;
            PlayerDataManager.Instance.SavePlayerPreference();
        }

        #endregion

        #region Public Methods

        public void SetMasterVolume(float value)
        {
            audioMixer.SetFloat(AudioMixerConstants.masterVolumeParameterName, AudioMixerUtils.GetMixerVolumeValueFrom01Value(value));
        }

        public void SetMusicVolume(float value)
        {
            audioMixer.SetFloat(AudioMixerConstants.musicVolumeParameterName, AudioMixerUtils.GetMixerVolumeValueFrom01Value(value));
        }

        public void SetSfxVolume(float value)
        {
            audioMixer.SetFloat(AudioMixerConstants.sfxVolumeParameterName, AudioMixerUtils.GetMixerVolumeValueFrom01Value(value));
        }

        #endregion

        #region Helpers

        void HandleMasterVolume(float value)
        {
            SetMasterVolume(value);
            var preference = PlayerDataManager.Instance.PlayerPreference;
            if (Mathf.Approximately(preference.masterVolume, value)) return;
            preference.masterVolume = value;
            QueueSave();
        }

        void HandleSfxVolume(float value)
        {
            SetSfxVolume(value);
            var preference = PlayerDataManager.Instance.PlayerPreference;
            if (Mathf.Approximately(preference.sfxVolume, value)) return;
            preference.sfxVolume = value;
            QueueSave();
        }

        void HandleBgmVolume(float value)
        {
            SetMusicVolume(value);
            var preference = PlayerDataManager.Instance.PlayerPreference;
            if (Mathf.Approximately(preference.bgmVolume, value)) return;
            preference.bgmVolume = value;
            QueueSave();
        }

        void QueueSave()
        {
            saveQueued = true;
            saveDueTime = Time.unscaledTime + saveDebounceSeconds;
        }

        #endregion
    }
}
