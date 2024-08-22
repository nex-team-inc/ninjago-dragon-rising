using Nex.BuildPipeline;
using UnityEngine;

namespace Nex
{
    public static class VersionUtils
    {
        public static string GetAppVersion() => Application.version;

        public static string GetBuildNumber()
        {
            var nbpInfo = NBPInfo.GetCurrent;
            return nbpInfo == null ? "--unknown--" : $"{nbpInfo.buildNumber}";
        }

        public static string GetVersionDisplayString()
        {
            var nbpInfo = NBPInfo.GetCurrent;
            return nbpInfo == null ? "--non-nbp--" : $"{Application.version} ({nbpInfo.buildNumber})";
        }
    }
}
