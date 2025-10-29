#nullable enable

using NaughtyAttributes;
using UnityEngine;

namespace Nex
{
    public class PlayerAttachment : MonoBehaviour
    {
        [Header("Pins")]
        [SerializeField] PlayerAttachmentPin basePin1 = null!;
        [SerializeField] PlayerAttachmentPin basePin2 = null!;
        [SerializeField] PlayerAttachmentPin rotationPin = null!;

        [Header("Transform Layers")]
        [SerializeField] Transform revertPositionLayer = null!;
        [SerializeField] Transform revertRotationLayer = null!;
        [SerializeField] Transform transformLayer = null!;

        [Header("Smoothness")]
        [SerializeField] bool useSmoothedPosition;

        [Header("Ruler (for inch-based scale)")]
        [SerializeField] bool useRulerScaleForLength;
        [SerializeField] bool useRulerScaleForThickness;
        [ShowIf(EConditionOperator.Or, nameof(useRulerScaleForLength), nameof(useRulerScaleForThickness))]
        [SerializeField] SpriteRenderer ruler = null!;
        [ShowIf(EConditionOperator.Or, nameof(useRulerScaleForLength), nameof(useRulerScaleForThickness))]
        [SerializeField] float inchesForWholeRuler = 8;

        [Header("Content")]
        [SerializeField] GameObject content = null!;

        PlayerAttachmentDataSource? dataSource;
        float localPinScale = 1;

        void Awake()
        {
            // Don't render it during runtime. It's only for editor.
            ruler.enabled = false;

            Initialize(null);
        }

        public void Initialize(
            PlayerAttachmentDataSource? aDataSource)
        {
            dataSource = aDataSource;

            var pin1Pos = revertPositionLayer.InverseTransformPoint(basePin1.transform.position);
            var pin2Pos = revertPositionLayer.InverseTransformPoint(basePin2.transform.position);
            var rotPin2Pos = revertPositionLayer.InverseTransformPoint(rotationPin.transform.position);
            var pinCenter = (pin1Pos + pin2Pos) * 0.5f;
            var pinVec = pin2Pos - pin1Pos;
            var rotationVec = rotPin2Pos - pin1Pos;

            revertPositionLayer.transform.localPosition = new Vector3(-pinCenter.x, -pinCenter.y, 0);
            revertRotationLayer.transform.localEulerAngles = new Vector3(0, 0, -Vector2.SignedAngle(Vector2.right, rotationVec));

            localPinScale = pinVec.magnitude;
        }

        void Update()
        {
            if (dataSource != null)
            {
                bool useSmooth = useSmoothedPosition;
                var pos1 = dataSource.GetNodePosition(basePin1.NodeIndex, useSmooth);
                var pos2 = dataSource.GetNodePosition(basePin2.NodeIndex, useSmooth);
                var rotPos = dataSource.GetNodePosition(rotationPin.NodeIndex, useSmooth);

                if (pos1 != null && pos2 != null && rotPos != null)
                {
                    var dpi = dataSource.DistancePerInch;
#pragma warning disable CS8620
                    pos1 += basePin1.OffsetInInches * dpi;
                    pos2 += basePin2.OffsetInInches * dpi;
#pragma warning restore CS8620
                    var pinCenter = (pos1!.Value + pos2!.Value) * 0.5f;
                    var pinVec = pos2.Value - pos1.Value;
                    var rotationVec = rotPos.Value - pos1.Value;
                    transformLayer.localPosition = pinCenter;
                    transformLayer.right = rotationVec.normalized;

                    var pinScale = localPinScale == 0 ? 1 : pinVec.magnitude / localPinScale;

                    var rulerScale = dpi * inchesForWholeRuler / ruler.transform.localScale.y;
                    var scaleX = useRulerScaleForLength ? rulerScale : pinScale;
                    var scaleY = useRulerScaleForThickness ? rulerScale : pinScale;
                    transformLayer.localScale = new Vector3(scaleX, scaleY, 1);

                    content.gameObject.SetActive(true);
                }
                else
                {
                    content.gameObject.SetActive(false);
                }
            }
        }
    }
}
