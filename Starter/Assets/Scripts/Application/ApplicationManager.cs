using System;
using UnityEngine;

namespace Nex
{
    public class ApplicationManager : Singleton<ApplicationManager>
    {
        const float animationIntroShownTimeGapInSeconds = 10800f; // 3 hrs

        [SerializeField] int targetFrameRate = 60;

        public bool FirstAppStart { get; set; } = true;

        protected override ApplicationManager GetThis()
        {
            return this;
        }

        protected override void Awake()
        {
            base.Awake();
            // This should be called during Awake according to
            // https://stackoverflow.com/questions/30436777/unity-android-game-screen-turns-off-during-gameplay
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            // Set rendering FPS
            var currentFrameRate = (int)(Screen.currentResolution.refreshRateRatio.value + 0.5);  // Round to int.
            Application.targetFrameRate = Math.Min(targetFrameRate, currentFrameRate);
        }
    }
}
