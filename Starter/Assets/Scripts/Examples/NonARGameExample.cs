using UnityEngine;

namespace Nex
{
    public class NonARGameExample : MonoBehaviour
    {
        [SerializeField] DetectionManager detectionManager = null!;
        [SerializeField] int numOfPlayers = 1;

        void Start()
        {
            detectionManager.Initialize(numOfPlayers);
        }
    }
}
