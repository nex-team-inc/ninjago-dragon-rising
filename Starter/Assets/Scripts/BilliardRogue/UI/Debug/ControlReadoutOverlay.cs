#nullable enable

using System;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Control lab readout (DebugSettings.showControlReadout, hidden by default in every build, GDD v2 §6): its own Screen Space Overlay canvas above the views and
    /// the PiP, one ControlReadoutPanel per player in the right HUD column (never over the arena). The coordinator
    /// creates it in Editor / development / ENABLE_DEBUG_SETTINGS builds only and hands it every ShotInputRouter
    /// PlayerShotInputFactory builds, so it follows the calibration test strike and then the gameplay inputs; a panel
    /// shows while its router is alive and the shown provider says so (setting on, a readout view on top).
    /// </summary>
    public sealed class ControlReadoutOverlay : MonoBehaviour
    {
        [SerializeField] CanvasGroup group = null!;
        [Tooltip("Index = player index.")]
        [SerializeField] ControlReadoutPanel[] panels = null!;
        [Tooltip("Anchored positions (bottom right): one player (index 0), two players (1 = P1 on top, 2 = P2 below).")]
        [SerializeField] Vector2[] panelPositions = { new(-32f, 32f), new(-32f, 540f), new(-32f, 32f) };

        ShotInputRouter?[] routers = Array.Empty<ShotInputRouter?>();
        Func<bool> shown = null!;
        int layoutCount = -1;
        bool visible;

        /// <summary>shown is polled every frame (e.g. the DebugSettings toggle and the top view).</summary>
        public void Initialize(Func<bool> aShown)
        {
            shown = aShown;
            routers = new ShotInputRouter?[panels.Length];
            group.alpha = 0f;
            foreach (var panel in panels)
            {
                panel.gameObject.SetActive(false);
            }
        }

        /// <summary>PlayerShotInputFactory callback: the newest router of a player replaces the previous one.</summary>
        public void Track(ShotInputRouter router)
        {
            var index = router.PlayerIndex;
            if (index < 0 || index >= panels.Length) return;
            routers[index] = router;
            panels[index].Bind(router);
            layoutCount = -1;
        }

        void Update()
        {
            var count = 0;
            for (var i = 0; i < routers.Length; i++)
            {
                if (routers[i] != null)
                {
                    count++;
                }
            }

            var show = count > 0 && shown();
            if (show != visible)
            {
                visible = show;
                group.alpha = show ? 1f : 0f;
            }

            if (!show) return;
            if (count != layoutCount)
            {
                Layout(count);
            }

            var now = Time.unscaledTime;
            for (var i = 0; i < routers.Length; i++)
            {
                if (routers[i] != null)
                {
                    panels[i].Refresh(now);
                }
            }
        }

        void Layout(int count)
        {
            layoutCount = count;
            var slot = count > 1 ? 1 : 0;
            for (var i = 0; i < panels.Length; i++)
            {
                var live = routers[i] != null;
                panels[i].gameObject.SetActive(live);
                if (!live) continue;
                ((RectTransform)panels[i].transform).anchoredPosition = panelPositions[Mathf.Min(slot, panelPositions.Length - 1)];
                slot++;
            }
        }
    }
}
