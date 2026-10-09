#nullable enable

using System.Collections.Generic;
using Nex.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>One localized line of the result view (a smart string filled with named numbers).</summary>
    public class ResultLine : MonoBehaviour
    {
        public readonly struct Data
        {
            public readonly LocalizedString template;
            public readonly Color color;
            public readonly IReadOnlyList<(string key, object value)> arguments;

            public Data(LocalizedString template, Color color, params (string key, object value)[] arguments)
            {
                this.template = template;
                this.color = color;
                this.arguments = arguments;
            }
        }

        [Header("Label")]
        [SerializeField] NexLocalizedString label = null!;
        [Header("Label Text")]
        [SerializeField] TMP_Text text = null!;

        #region Initialization

        public void Initialize(Data data)
        {
            var arguments = new (string key, object value)[data.arguments.Count];
            for (var i = 0; i < arguments.Length; i++)
            {
                arguments[i] = data.arguments[i];
            }

            label.StringReference = LocalizedStrings.WithArguments(data.template, arguments);
            text.color = data.color;
        }

        #endregion
    }
}
