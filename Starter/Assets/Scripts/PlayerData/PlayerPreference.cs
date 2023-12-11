// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
namespace Nex
{
    // The PlayerPreference stores things that the user can adjust through the settings page.
    // This is also managed by the PlayerDataManager, and stored locally through ES3.
    public class PlayerPreference
    {
        #region Volume

        public float masterVolume = 1f;
        public float sfxVolume = 1f;
        public float bgmVolume = 1f;

        #endregion
    }
}
