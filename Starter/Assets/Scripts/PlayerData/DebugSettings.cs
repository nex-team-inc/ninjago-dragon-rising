using Nex.Dev.Attributes;
using UnityEngine.AddressableAssets;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global

namespace Nex
{
    public class DebugSettings
    {
        public bool exampleDebugFlag = false;

        [DebugOrder(1)]
        [SaveBeforeInvoking]
        public void ReloadMainScene()
        {
            SingletonSpawner.KillAllSingletons();
            Addressables.LoadSceneAsync(ApplicationManager.Instance.mainSceneReference);
        }
    }
}
