#nullable enable

using System.Collections.Generic;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Smart-string labels keep an empty reference in their prefab (a bound smart string would format on enable,
    /// before its arguments exist) and get a fresh instance with its arguments from this helper.
    /// </summary>
    public static class LocalizedStrings
    {
        public static LocalizedString WithArguments(LocalizedString template, params (string key, object value)[] arguments)
        {
            var values = new Dictionary<string, object>();
            foreach (var (key, value) in arguments)
            {
                values[key] = value;
            }

            return new LocalizedString(template.TableReference, template.TableEntryReference)
            {
                Arguments = new List<object> { values },
            };
        }
    }
}
