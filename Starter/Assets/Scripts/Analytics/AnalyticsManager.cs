using mixpanel;
using Nex.Platform;
using Nex.Platform.AnalyticsExtension;
using UnityEngine;

#pragma warning disable CS8604

#nullable enable

namespace Nex
{
    public class AnalyticsManager : Singleton<AnalyticsManager>
    {
        protected override AnalyticsManager GetThis()
        {
            return this;
        }

        // ReSharper disable once UnusedMember.Global
        public void TrackEvent(string eventName, Value? props = default)
        {
            GameAnalytics.Instance.Track(eventName, props);
        }

        public void TrackPause()
        {
            Debug.Log($"[Analytics] PAUSE");
            GameAnalytics.Instance.SendPausePlaySessionEvent();
        }

        public void TrackResume()
        {
            Debug.Log($"[Analytics] RESUME");
            GameAnalytics.Instance.SendResumePlaySessionEvent();
        }

        public void TrackGameStart(string screen, int numPlayer, string gameMode="default", GameAnalyticsProperties? props=null)
        {
            Debug.Log($"[Analytics] START props:{props?.ToJsonString()}");
            GameAnalytics.Instance.SendStartPlaySessionEvent(screen, numPlayer, gameMode, props);
        }

        public void TrackGameStop(GameAnalyticsProperties? props=null)
        {
            Debug.Log($"[Analytics] STOP props:{props?.ToJsonString()}");
            GameAnalytics.Instance.SendStopPlaySessionEvent(props);
        }

        public void TrackScreen(string screenName, GameAnalyticsProperties? props = null)
        {
            Debug.Log($"[Analytics] SCREEN: {screenName} props:{props?.ToJsonString()}");
            GameAnalytics.Instance.SendScreenEvent(screenName, props);
        }
    }
}
