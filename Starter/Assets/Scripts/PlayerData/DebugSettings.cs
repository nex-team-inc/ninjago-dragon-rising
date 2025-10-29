
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global

namespace Nex
{
    public class DebugSettings
    {
        public bool enableDebugPrinter = false;

        public void MuteMusic()
        {
            VolumeManager.Instance.SetMusicVolume(0);
        }

        public void MuteSfx() => VolumeManager.Instance.SetSfxVolume(0);

        public void SetMusicVolume(float volume) => VolumeManager.Instance.SetMusicVolume(volume);
        public void SetSfxVolume(float volume) => VolumeManager.Instance.SetSfxVolume(volume);

        public void ReloadMainScene()
        {
            // var mainSceneName = GameConfigsManager.Instance.GetMainSceneName();
            // SingletonSpawner.KillAllSingletons();
            // SceneManager.LoadSceneAsync(mainSceneName);
        }

        public void ClearAllDataCache()
        {
            PlayerDataManager.Instance.ResetPlayerPreference();
        }
    }
}
