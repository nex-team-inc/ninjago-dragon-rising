#nullable enable

using TMPro;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Control readout row for Hype (GDD v2 §3): the energy the game uses (router.MotionEnergy: body meter, or the
    /// debug key / mouse / bot simulation if higher) and the body meter itself (energy, detected nodes, mean node
    /// speed above the deadzone). Texts are rewritten only when the quantized value changes (TMP SetText with float
    /// arguments: no allocation).
    /// </summary>
    public sealed class ControlReadoutEnergyRow : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI energyLabel = null!;
        [SerializeField] TextMeshProUGUI bodyLabel = null!;
        [Tooltip("Energy at or above this shows in the active colour (Hype tier 1).")]
        [SerializeField, Range(0f, 1f)] float activeEnergy = 0.25f;
        [SerializeField] Color activeColor = new(1f, 0.45f, 0.95f, 1f);
        [SerializeField] Color idleColor = new(0.72f, 0.77f, 0.9f, 1f);
        [SerializeField] Color badColor = new(1f, 0.353f, 0.29f, 1f);

        // The meter reads 11 body nodes (MotionEnergyMeter).
        const string BodyFormat = "BODY {0:0.00} {1:0}/11 {2:0}in/s";

        int shownEnergy;
        int shownBody;

        public void Bind()
        {
            shownEnergy = shownBody = int.MinValue;
        }

        public void Refresh(ShotInputRouter router)
        {
            var routed = router.MotionEnergy;
            var energy = routed != null ? routed.Energy01 : 0f;
            var tracked = routed != null && routed.IsTracked;
            var code = Mathf.RoundToInt(energy * 100f) * 2 + (tracked ? 1 : 0);
            if (code != shownEnergy)
            {
                shownEnergy = code;
                energyLabel.SetText("ENERGY {0:0.00}", energy);
                energyLabel.color = !tracked ? badColor : energy >= activeEnergy ? activeColor : idleColor;
            }

            var meter = router.BodyMotion;
            if (meter == null || !meter.IsTracked)
            {
                if (shownBody == -1) return;
                shownBody = -1;
                bodyLabel.SetText("BODY -");
                bodyLabel.color = idleColor;
                return;
            }

            var body = Mathf.RoundToInt(meter.Energy01 * 100f) * 10000 + meter.DetectedNodes * 1000 + Mathf.Clamp(Mathf.RoundToInt(meter.MeanExcessSpeed), 0, 999);
            if (body == shownBody) return;
            shownBody = body;
            bodyLabel.SetText(BodyFormat, meter.Energy01, meter.DetectedNodes, Mathf.Round(meter.MeanExcessSpeed));
            bodyLabel.color = idleColor;
        }
    }
}
