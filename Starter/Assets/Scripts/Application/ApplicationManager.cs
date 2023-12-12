using System;
using UnityEngine;

namespace Nex
{
    public class ApplicationController : Singleton<ApplicationController>
    {
        protected override ApplicationController GetThis()
        {
            return this;
        }

        [SerializeField] int targetFrameRate = 60;

        protected override void Awake()
        {
            base.Awake();
            // This should be called during Awake according to
            // https://stackoverflow.com/questions/30436777/unity-android-game-screen-turns-off-during-gameplay
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Set rendering FPS
            var currentFrameRate = Screen.currentResolution.refreshRate;
            Application.targetFrameRate = Math.Min(targetFrameRate, currentFrameRate);
        }
    }
}
