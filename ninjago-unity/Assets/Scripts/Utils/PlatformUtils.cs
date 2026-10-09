namespace Nex.Utils
{
    public static class PlatformUtils
    {
        public static bool IsEditor
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsOnIosDevice
        {
            get
            {
#if !UNITY_EDITOR && UNITY_IOS
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsOnAndroidDevice
        {
            get
            {
#if !UNITY_EDITOR && UNITY_ANDROID
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsProdBuildOnMacOrPC => IsOnStandaloneWindowsDevice || IsOnStandaloneMacOsDevice;

        public static bool IsOnStandaloneMacOsDevice
        {
            get
            {
#if !UNITY_EDITOR && UNITY_STANDALONE_OSX
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsOnStandaloneWindowsDevice
        {
            get
            {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsProduction
        {
            get
            {
#if PRODUCTION
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsOnOlympia
        {
            get
            {
#if OLYMPIA
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsOnSkyTv
        {
            get
            {
#if SKY_TV_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsForVideoShooting
        {
            get
            {
#if VIDEO_SHOOTING
                return true;
#else
                return false;
#endif
            }
        }
    }
}
