#nullable enable

using System;
using System.Collections.Generic;
using Nex.Localization;
using TMPro;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// A pixel-font label drawn as stacked TMP layers (drop-shadow copies first, face last): bitmap fonts have no outline,
    /// so contrast comes from a dark copy offset by one font pixel. Localized layers share one key and one argument list;
    /// numbers go through TMP SetText (no allocation) and are meant for per-event HUD updates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TextLabel : MonoBehaviour
    {
        [Tooltip("Text layers back to front: shadow copies first, the face last.")]
        [SerializeField] TMP_Text[] layers = null!;
        [Tooltip("Localized layers (bound to the same key). Empty for number-only labels.")]
        [SerializeField] NexLocalizedString[] localized = Array.Empty<NexLocalizedString>();

        readonly List<object> arguments = new(3);

        public TMP_Text Face => layers[layers.Length - 1];

        public Color Color
        {
            get => Face.color;
            set => Face.color = value;
        }

        #region Localized text

        /// <summary>Switches every localized layer to key (no arguments).</summary>
        public void SetKey(string key)
        {
            arguments.Clear();
            Apply(key);
        }

        public void SetKey(string key, object arg0)
        {
            arguments.Clear();
            arguments.Add(arg0);
            Apply(key);
        }

        public void SetKey(string key, object arg0, object arg1)
        {
            arguments.Clear();
            arguments.Add(arg0);
            arguments.Add(arg1);
            Apply(key);
        }

        public void SetKey(string key, object arg0, object arg1, object arg2)
        {
            arguments.Clear();
            arguments.Add(arg0);
            arguments.Add(arg1);
            arguments.Add(arg2);
            Apply(key);
        }

        #endregion

        #region Raw text (numbers, symbols)

        /// <summary>Zero-allocation number, e.g. SetNumber(80, "{0}%").</summary>
        public void SetNumber(int value, string format = "{0}")
        {
            foreach (var layer in layers)
            {
                layer.SetText(format, value);
            }
        }

        /// <summary>Zero-allocation formatted numbers, e.g. SetNumbers("{0}/{1}", hp, max).</summary>
        public void SetNumbers(string format, int arg0, int arg1)
        {
            foreach (var layer in layers)
            {
                layer.SetText(format, arg0, arg1);
            }
        }

        /// <summary>Plain text for glyph-only content (never player-facing words: those go through SetKey).</summary>
        public void SetRaw(string text)
        {
            foreach (var layer in layers)
            {
                layer.text = text;
            }
        }

        #endregion

        void Apply(string key)
        {
            foreach (var label in localized)
            {
                var reference = label.StringReference;
                reference.Arguments = arguments;
                var before = reference.TableEntryReference;
                // A changed entry refreshes by itself (with the arguments above); an unchanged one does not.
                label.SetEntry(key);
                if (reference.TableEntryReference.Equals(before)) label.RefreshString();
            }
        }
    }
}
