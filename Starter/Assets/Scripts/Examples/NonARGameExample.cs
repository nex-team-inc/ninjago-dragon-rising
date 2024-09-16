using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex
{
    public class NonARGameExample : MonoBehaviour
    {
        [SerializeField] DetectionManager detectionManager = null!;
        [SerializeField] int numOfPlayers = 1;

        void Start()
        {
            StartAsync().Forget();
        }

        async UniTask StartAsync()
        {
            detectionManager.Initialize(numOfPlayers);
            await ScreenBlockerManager.Instance.Hide();
        }
    }
}
