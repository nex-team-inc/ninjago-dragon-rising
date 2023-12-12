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
            GameAnalytics.Instance.SendPausePlaySessionEvent();
        }

        public void TrackResume()
        {
            GameAnalytics.Instance.SendResumePlaySessionEvent();
        }

        public void TrackGameStart(string screen, int numPlayer, string gameMode="default", GameAnalyticsProperties? props=null)
        {
            GameAnalytics.Instance.SendStartPlaySessionEvent(screen, numPlayer, gameMode, props);
        }

        public void TrackGameStop()
        {
            GameAnalytics.Instance.SendStopPlaySessionEvent();
        }

        public void TrackScreen(string screenName, Value? props = null)
        {
            Debug.Log($"Analytics track screen: {screenName}");
            GameAnalytics.Instance.SendScreenEvent(screenName, props);
        }
    }
}
