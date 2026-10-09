#nullable enable

using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

namespace Nex.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    [AddComponentMenu("Localization/Nex Localized String")]
    public class NexLocalizedString : LocalizeStringEvent
    {
        TMP_Text tmpText = null!;
        public bool ShouldOverrideFontChange { get; set; }

        void Awake()
        {
            if (GameIsNotPlaying) return;
            tmpText = GetComponent<TextMeshProUGUI>();
        }

        void Start()
        {
            if (GameIsNotPlaying) return;
            SetupLocalization().Forget();
        }

        async UniTask SetupLocalization()
        {
            await LocalizationSettings.InitializationOperation.ToUniTask();
            SetupAutoUpdateString().Forget();
        }

        async UniTask SetupAutoUpdateString()
        {
            OnUpdateString.AddListener(UpdateStringAction);
            if (StringReference != null)
            {
                var localizedString = await StringReference.GetLocalizedStringAsync().WithCancellation(this.GetCancellationTokenOnDestroy());
                UpdateStringAction(localizedString);
            }
        }

        void UpdateStringAction(string localizedString)
        {
            tmpText.text = localizedString;
        }

        public void SetSmartStringArgument(string key, object value)
        {
            var dict = (Dictionary<string, object>?)StringReference.Arguments?.FirstOrDefault(args => args.GetType() == typeof(Dictionary<string, object>));
            if (dict == null)
            {
                dict = new Dictionary<string, object>();
            }
            dict[key] = value;

            StringReference.Arguments = new List<object> {
                dict
            };
            RefreshString();
        }

        /// <summary>Sets several smart string arguments with a single refresh, so no format runs with some missing.</summary>
        public void SetSmartStringArguments(params (string key, object value)[] arguments)
        {
            var dict = new Dictionary<string, object>();
            foreach (var (key, value) in arguments)
            {
                dict[key] = value;
            }

            StringReference.Arguments = new List<object> { dict };
            RefreshString();
        }

        void OnDestroy()
        {
            OnUpdateString.RemoveAllListeners();
        }

        // This is included to revert [ExecuteAlways] attribute from LocalizedMonoBehavior
        static bool GameIsNotPlaying => !Application.isPlaying;
    }
}
