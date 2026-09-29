#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nex
{
    public class DebugPrinter : Singleton<DebugPrinter>
    {
        [SerializeField] Color textColor = new(1, 0.5f, 0.5f, 1);
        [SerializeField] Color backgroundColor = Color.white;
        [SerializeField] int fontSize = 40;
        [SerializeField] Font font = null!;

        readonly Dictionary<string, string> textByKey = new();
        GUIStyle? textStyle;
        Texture2D? backgroundTexture;
        bool subscribed;

        #region Life Cycle

        protected override DebugPrinter GetThis() => this;

        void OnDisable()
        {
            if (backgroundTexture != null)
            {
                Destroy(backgroundTexture);
                backgroundTexture = null;
            }
            textStyle = null;
        }

        protected override void OnDestroy()
        {
            if (subscribed && PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.DebugSettingsSaved -= Wake;
            }

            base.OnDestroy();
        }

        #endregion

        #region Helpers

        void CreateStyleIfNeeded()
        {
            if (backgroundTexture == null)
            {
                backgroundTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                backgroundTexture.SetPixel(0, 0, backgroundColor);
                backgroundTexture.Apply();
            }

            if (textStyle == null)
            {
                textStyle = new GUIStyle
                {
                    fontSize = fontSize,
                    fontStyle = FontStyle.Normal,
                    font = font,
                    normal =
                    {
                        textColor = textColor,
                        background = backgroundTexture
                    }
                };
            }
        }

        #endregion

        #region Public Methods

        public void Print(string key, string message)
        {
            textByKey[key] = message;
        }

        // Suggested by Wangshu
        public void Print(string key, object obj)
        {
            textByKey[key] = obj.ToString();
        }

        public void Clear()
        {
            textByKey.Clear();
        }

        #endregion

        #region IMGUI

        void Wake() => enabled = true;

        void OnGUI()
        {
            var playerData = PlayerDataManager.Instance;
            if (playerData == null) return;
            if (!playerData.DebugSettings.enableDebugPrinter)
            {
                // Any enabled OnGUI costs an IMGUI layout + repaint pass (and garbage) every frame even when it draws
                // nothing, so the printer sleeps until the debug settings are saved again.
                if (!subscribed)
                {
                    playerData.DebugSettingsSaved += Wake;
                    subscribed = true;
                }

                enabled = false;
                return;
            }

            var text = string.Join("\n", textByKey.Select(kv => $"{kv.Key}: {kv.Value}"));
            var content = new GUIContent(text);
            if (textStyle == null)
            {
                CreateStyleIfNeeded();
            }
            var size = textStyle!.CalcSize(content);

            GUI.Label(new Rect(10, 10, size.x, size.y), text, textStyle);
        }

        #endregion
    }
}
