using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Nex
{
    public class ApplicationManager : Singleton<ApplicationManager>
    {
        [SerializeField] int targetFrameRate = 60;
        public AssetReference mainSceneReference = null!;

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
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Set rendering FPS
            var currentFrameRate = Screen.currentResolution.refreshRate;
            Application.targetFrameRate = Math.Min(targetFrameRate, currentFrameRate);
        }
    }
}
