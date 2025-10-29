#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex
{
    public class DebugPrinter : Singleton<DebugPrinter>
    {
        [SerializeField] Color textColor = new(1, 0.5f, 0.5f, 1);
        [SerializeField] int fontSize = 40;

        protected override DebugPrinter GetThis() => this;

        readonly Dictionary<string, string> textByKey = new();

        public void Print(string key, string message)
        {
            textByKey[key] = message;
        }

        // Suggested by Wangshu
        public void Print (string key, object obj)
        {
            textByKey[key] = obj.ToString();
        }

        void OnGUI()
        {
            if (PlayerDataManager.Instance == null ||
                !PlayerDataManager.Instance.DebugSettings.enableDebugPrinter)
            {
                return;
            }

            GUI.color = textColor;
            GUI.skin.label.fontSize = fontSize;

            var text = "";
            foreach (var key in textByKey.Keys)
            {
                text += $"{key}: {textByKey[key]}\n";
            }

            GUI.Label(new Rect(10, 10, 1900, 1060), text);
        }
    }
}
