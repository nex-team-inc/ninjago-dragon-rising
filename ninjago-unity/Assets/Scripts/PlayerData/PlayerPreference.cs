// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
namespace Nex
{
    // The PlayerPreference stores things that the user can adjust through the settings page.
    // This is also managed by the PlayerDataManager, and stored locally through ES3.
    // ES3 keeps field initializers for fields missing from an older save, so new fields are safe to append.
    public class PlayerPreference
    {
        #region Volume

        public float masterVolume = 1f;
        public float sfxVolume = 1f;
        public float bgmVolume = 1f;

        #endregion

        #region Billiard Rogue

        // "" = platform/system locale.
        public string localeCode = "";
        public int numPlayers = 1;
        /// <summary>Unused since GDD v2 §16 (the upper paw is always the ball); kept so older saves load unchanged.</summary>
        public bool leftHandedCue;
        // 0 short, 1 normal, 2 long.
        public int aimGuideLength = 1;
        public bool screenShake = true;

        #endregion
    }
}
