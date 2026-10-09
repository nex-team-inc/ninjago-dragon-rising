#nullable enable

using NaughtyAttributes;
using UnityEngine;

namespace Nex
{
    public class GameConfigsManager : Singleton<GameConfigsManager>
    {
        [SerializeField, Scene] string mainScene = null!;
        [SerializeField, Scene] string arGameScene = null!;
        [SerializeField, Scene] string nonARGameScene = null!;
        public string MainScene => mainScene;
        public string ARGameScene => arGameScene;
        public string NonARGameScene => nonARGameScene;

        protected override GameConfigsManager GetThis()
        {
            return this;
        }
    }
}
