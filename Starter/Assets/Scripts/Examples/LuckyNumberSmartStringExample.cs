#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Nex.Localization.Examples
{
    public class LuckyNumberSmartStringExample : MonoBehaviour
    {
        [SerializeField] NexLocalizedString targetText = null!;

        void Start()
        {
            gameObject.SetActive(false);
            SetupSmartString().Forget();
        }

        async UniTask SetupSmartString()
        {
            await targetText.StringReference.GetLocalizedStringAsync();
            targetText.SetSmartStringArgument("0", Random.Range(1, 100));
            gameObject.SetActive(true);
        }
    }
}
