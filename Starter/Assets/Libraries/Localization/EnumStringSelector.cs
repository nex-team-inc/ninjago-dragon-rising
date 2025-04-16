#nullable enable

using System;
using Nex.Util;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NexLocalizedString))]
    public class EnumStringSelector<TEnum> : MonoBehaviour where TEnum : Enum
    {
        [SerializeField] EnumDictionary<TEnum, LocalizedString> stringDictionary = null!;

        NexLocalizedString target = null!;
        TEnum value = default!;

        void Awake()
        {
            target = GetComponent<NexLocalizedString>();
        }

        public TEnum Value
        {
            get => value;
            set
            {
                this.value = value;
                target.StringReference = stringDictionary[value];
            }
        }
    }
}
