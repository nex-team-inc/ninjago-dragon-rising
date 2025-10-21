#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Dev
{
    public class DebugTimeScaleChanger : MonoBehaviour
    {
        [SerializeField] List<float> timeScales = new();

        void Update()
        {
            if (DebugInput.GetKeyDown(KeyCode.T))
            {
                var index = timeScales.FindIndex(i => Mathf.Approximately(i, Time.timeScale));
                var newTimeScale = 1f;

                if (index >= 0)
                {
                    newTimeScale = timeScales[(index + 1) % timeScales.Count];
                }
                Time.timeScale = newTimeScale;

                Debug.Log($"[DebugTimeScaleChanger] Time scale changed to {Time.timeScale}.");
            }
        }
    }
}
