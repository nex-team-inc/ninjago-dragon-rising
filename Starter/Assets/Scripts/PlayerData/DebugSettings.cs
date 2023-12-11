using Nex.Dev.Attributes;
using UnityEngine.SceneManagement;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global

namespace Nex
{
    public class DebugSettings
    {
        public bool hackSmackBackLevelProvider = false;

        public bool autoHitInSmackBack = false;

        public bool showPreviewFrame = false;

        public bool showDetectionDebugUI = false;

        #region FPS experiments

        public bool disableLights = false;
        public bool disableAvatar = false;

        #endregion

        #region Unlock overrides

        public bool unlockAllAvatars = false;
        public bool unlockAllDifficulties = false;
        public bool unlockAllCollectables = false;

        #endregion
    }
}
