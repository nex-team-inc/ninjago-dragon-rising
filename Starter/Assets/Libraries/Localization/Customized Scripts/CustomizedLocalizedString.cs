#nullable enable

using System;
using UnityEngine.Localization;

namespace Nex.Localization
{
    // <summary>
    // This is to do localization independent to global locale.
    // </summary>
    [Serializable]
    public class CustomizedLocalizedString
    {
        public Locale locale = null!;
        public string localizedString = null!;
    }
}
