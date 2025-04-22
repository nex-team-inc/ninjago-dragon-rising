#nullable enable

using System;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Nex.Localization.Demo
{
    public class SmartStringDemo : MonoBehaviour
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
            StartUpdateLoop().Forget();

            gameObject.SetActive(true);
        }

        async UniTask StartUpdateLoop()
        {
            await foreach (var _ in UniTaskAsyncEnumerable.Interval(TimeSpan.FromSeconds(3)).WithCancellation(destroyCancellationToken))
            {
                targetText.SetSmartStringArgument("0", Random.Range(1, 100));
            }
        }
    }
}
