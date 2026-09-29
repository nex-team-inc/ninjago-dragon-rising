#nullable enable

using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Device profiling in development builds: every intervalSeconds one "[Perf]" log line with fps, frames over the
    /// vsync budget, FrameTimingManager CPU main-thread work / render thread / GPU times (average and max), which side
    /// limits the frame, GC allocations, draw counts, the render tier and the flow state. Read it with
    /// `adb logcat -s Unity | grep Perf`. Runs while DebugSettings.logFrameTiming is on (default on in BR_CONTROL_DEMO
    /// builds), only in Editor / development / ENABLE_DEBUG_SETTINGS builds; release builds disable it on Awake. GPU times
    /// need Frame Timing Stats in Player Settings (RenderPipelineBuilder).
    /// </summary>
    public sealed class FrameTimingLogger : MonoBehaviour
    {
        [Header("Wiring (WorldCameraRigBuilder)")]
        [SerializeField] WorldCameraRig rig = null!;

        [Header("Log")]
        [Tooltip("Seconds per [Perf] line.")]
        [SerializeField, Range(1f, 30f)] float intervalSeconds = 5f;
        [Tooltip("Frame budget in ms: frames that take longer count as missed (16.7 = 60 fps).")]
        [SerializeField, Range(8f, 50f)] float budgetMs = 16.7f;

        const string Prefix = "[Perf] ";

        readonly FrameTiming[] latest = new FrameTiming[1];
        readonly StringBuilder line = new(320);
        ProfilerRecorder gcAllocated;
        ProfilerRecorder batches;
        ProfilerRecorder setPass;
        ProfilerRecorder triangles;
        ulong lastFrameStart;
        bool running;
        float windowStart;
        int frames;
        int missed;
        float maxDeltaMs;
        int timed;
        double mainSum, renderSum, gpuSum, waitSum;
        double mainMax, renderMax, gpuMax;
        long gcBytes;
        int gcFrames;
        int gcCollections;
        long batchSum, setPassSum, triangleSum;
        int counterFrames;

        #region Life Cycle

        void Awake()
        {
#if !(ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR)
            enabled = false;
#endif
        }

        void Update()
        {
            var on = Requested();
            if (on != running)
            {
                if (on)
                {
                    Begin();
                }
                else
                {
                    End();
                }
            }

            if (!running) return;
            Sample();
            if (Time.unscaledTime - windowStart >= intervalSeconds)
            {
                Flush();
            }
        }

        void OnDisable() => End();

        #endregion

        #region Helpers

        static bool Requested()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            return PlayerDataManager.Instance != null && PlayerDataManager.Instance.DebugSettings.logFrameTiming;
#else
            return false;
#endif
        }

        void Begin()
        {
            running = true;
            gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            Debug.Log($"{Prefix}logger on: gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType} "
                + $"shaderLevel={SystemInfo.graphicsShaderLevel} cpu={SystemInfo.processorType} x{SystemInfo.processorCount} "
                + $"screen={Screen.width}x{Screen.height}@{Screen.currentResolution.refreshRateRatio.value:0} vSync={QualitySettings.vSyncCount} "
                + $"targetFps={Application.targetFrameRate} frameTiming={(FrameTimingManager.IsFeatureEnabled() ? "on" : "off (fps only)")} "
                + $"every {intervalSeconds:0}s");
            Reset();
        }

        void End()
        {
            if (!running) return;
            running = false;
            gcAllocated.Dispose();
            batches.Dispose();
            setPass.Dispose();
            triangles.Dispose();
        }

        void Reset()
        {
            windowStart = Time.unscaledTime;
            frames = missed = timed = gcFrames = counterFrames = 0;
            maxDeltaMs = 0f;
            mainSum = renderSum = gpuSum = waitSum = mainMax = renderMax = gpuMax = 0.0;
            gcBytes = batchSum = setPassSum = triangleSum = 0;
            gcCollections = System.GC.CollectionCount(0);
        }

        void Sample()
        {
            frames++;
            var deltaMs = Time.unscaledDeltaTime * 1000f;
            if (deltaMs > budgetMs * 1.2f)
            {
                missed++;
            }

            maxDeltaMs = Mathf.Max(maxDeltaMs, deltaMs);

            if (gcAllocated.Valid)
            {
                var bytes = gcAllocated.LastValue;
                gcBytes += bytes;
                if (bytes > 0)
                {
                    gcFrames++;
                }
            }

            if (batches.Valid)
            {
                batchSum += batches.LastValue;
                setPassSum += setPass.LastValue;
                triangleSum += triangles.LastValue;
                counterFrames++;
            }

            // GPU times arrive a few frames late: each completed frame is counted once.
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, latest) == 0) return;
            var timing = latest[0];
            if (timing.frameStartTimestamp == lastFrameStart) return;
            lastFrameStart = timing.frameStartTimestamp;
            var main = timing.cpuMainThreadFrameTime - timing.cpuMainThreadPresentWaitTime;
            timed++;
            mainSum += main;
            renderSum += timing.cpuRenderThreadFrameTime;
            gpuSum += timing.gpuFrameTime;
            waitSum += timing.cpuMainThreadPresentWaitTime;
            mainMax = System.Math.Max(mainMax, main);
            renderMax = System.Math.Max(renderMax, timing.cpuRenderThreadFrameTime);
            gpuMax = System.Math.Max(gpuMax, timing.gpuFrameTime);
        }

        void Flush()
        {
            var seconds = Time.unscaledTime - windowStart;
            line.Clear();
            line.Append(Prefix).Append("fps ").Append((frames / seconds).ToString("0.0"))
                .Append(" missed ").Append(frames > 0 ? missed * 100 / frames : 0).Append("% max ").Append(maxDeltaMs.ToString("0")).Append("ms");
            if (timed > 0)
            {
                var main = mainSum / timed;
                var render = renderSum / timed;
                var gpu = gpuSum / timed;
                line.Append(" | cpu main ").Append(main.ToString("0.0")).Append('/').Append(mainMax.ToString("0.0"))
                    .Append(" render ").Append(render.ToString("0.0")).Append('/').Append(renderMax.ToString("0.0"))
                    .Append(" gpu ").Append(gpu > 0.0 ? gpu.ToString("0.0") + "/" + gpuMax.ToString("0.0") : "n/a")
                    .Append(" ms (avg/max) wait ").Append((waitSum / timed).ToString("0.0"))
                    .Append(" | ").Append(Bottleneck(main, render, gpu));
            }
            else
            {
                line.Append(" | no FrameTimingManager data");
            }

            line.Append(" | gc ").Append(frames > 0 ? gcBytes / frames : 0).Append(" B/frame on ").Append(gcFrames).Append(" frames, ")
                .Append(System.GC.CollectionCount(0) - gcCollections).Append(" GCs");
            if (counterFrames > 0)
            {
                line.Append(" | batches ").Append(batchSum / counterFrames).Append(" setpass ").Append(setPassSum / counterFrames)
                    .Append(" tris ").Append(triangleSum / counterFrames / 1000).Append('k');
            }

            line.Append(" | tier ").Append(rig.Tier).Append(" | ").Append(FlowState());
            Debug.Log(line.ToString());
            Reset();
        }

        // Averages over budget name the limiting side; all under budget while frames are still missed points at spikes
        // (see the max values) or at pacing (present wait).
        string Bottleneck(double main, double render, double gpu)
        {
            if (gpu > budgetMs && gpu >= main && gpu >= render)
            {
                return "GPU-bound";
            }

            if (main > budgetMs && main >= render)
            {
                return "CPU-main-bound";
            }

            if (render > budgetMs)
            {
                return "CPU-render-bound";
            }

            return missed * 10 > frames ? "missing frames under budget (spikes or pacing)" : "within budget";
        }

        // First two words of DebugHooks.State(): the top view on menus, the turn phase and act in a run.
        static string FlowState()
        {
            var state = DebugHooks.State();
            var first = state.IndexOf(' ');
            var second = first < 0 ? -1 : state.IndexOf(' ', first + 1);
            return second < 0 ? state : state.Substring(0, second);
        }

        #endregion
    }
}
