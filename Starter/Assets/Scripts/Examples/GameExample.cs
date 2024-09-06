using Jazz;
using UnityEngine;

namespace Nex
{
    public class GameExample : MonoBehaviour
    {
        [SerializeField] DetectionManager detectionManager = null!;
        [SerializeField] int numOfPlayers = 1;

        void Start()
        {
            detectionManager.Initialize(numOfPlayers);
        }
    }
}
