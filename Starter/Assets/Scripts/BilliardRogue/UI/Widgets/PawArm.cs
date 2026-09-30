#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// One cartoon cat arm of the reward motion pick (GDD v2 §4): a fur sleeve stretched from a fixed shoulder below the
    /// screen edge to the paw, rotated to point at it, with the paw sprite (open / grab) at the end. The root is the
    /// shoulder (pivot); positions are in the parent's local space (the stretched arms layer, origin at its centre).
    /// </summary>
    public sealed class PawArm : MonoBehaviour
    {
        [Tooltip("Rotated towards the paw; its pivot is the shoulder.")]
        [SerializeField] RectTransform shoulder = null!;
        [Tooltip("Tiled fur sleeve, pivot at its bottom centre (the shoulder); its height = arm length.")]
        [SerializeField] RectTransform sleeve = null!;
        [SerializeField] Image sleeveImage = null!;
        [Tooltip("Paw at the sleeve end, pivot at the palm centre.")]
        [SerializeField] RectTransform paw = null!;
        [SerializeField] Image pawImage = null!;
        [Tooltip("Paw rest point relative to the shoulder when no hand is tracked.")]
        [SerializeField] Vector2 restOffset = new(120f, 330f);
        [SerializeField, Range(40f, 400f)] float minLength = 140f;

        Sprite? openSprite;
        Sprite? grabSprite;
        Vector2 home;
        Vector2 current;
        bool hasCurrent;

        /// <summary>Shoulder position in the parent's space (without the rise offset).</summary>
        public Vector2 Home => home;

        /// <summary>Displayed paw position in the parent's space.</summary>
        public Vector2 PawPosition => current;

        public Vector2 RestPosition => home + restOffset;

        /// <summary>How far the paw sinks at rise 0 (the arm fully below the screen edge).</summary>
        public float DropDistance => restOffset.y + minLength;

        void Awake()
        {
            home = shoulder.localPosition;
        }

        public void SetLook(Sprite? sleeveSprite, Sprite? open, Sprite? grab)
        {
            sleeveImage.sprite = sleeveSprite;
            openSprite = open;
            grabSprite = grab;
            pawImage.sprite = open;
        }

        public void SetGrab(bool grab)
        {
            pawImage.sprite = grab && grabSprite != null ? grabSprite : openSprite;
        }

        /// <summary>Snaps the paw (no smoothing), e.g. at reveal.</summary>
        public void Snap(Vector2 target, float rise)
        {
            current = target;
            hasCurrent = true;
            Apply(rise);
        }

        /// <summary>Moves the paw towards target with exponential smoothing (sharpness 1/s, unscaled dt).</summary>
        public void Follow(Vector2 target, float sharpness, float dt, float rise)
        {
            if (!hasCurrent)
            {
                Snap(target, rise);
                return;
            }

            current = Vector2.Lerp(current, target, 1f - Mathf.Exp(-sharpness * dt));
            Apply(rise);
        }

        /// <summary>Moving shoulder (gameplay arms): the shoulder at shoulderPosition, the paw at pawPosition, no smoothing.</summary>
        public void Reach(Vector2 shoulderPosition, Vector2 pawPosition)
        {
            home = shoulderPosition;
            current = pawPosition;
            hasCurrent = true;
            Apply(1f);
        }

        /// <summary>rise 0 = shoulder and paw pulled below the screen edge, 1 = in place.</summary>
        void Apply(float rise)
        {
            var drop = (1f - rise) * DropDistance;
            var root = home + new Vector2(0f, -drop);
            shoulder.localPosition = root;
            var reach = current + new Vector2(0f, -drop) - root;
            var length = Mathf.Max(minLength, reach.magnitude);
            var angle = reach.sqrMagnitude > 1f ? Mathf.Atan2(reach.y, reach.x) * Mathf.Rad2Deg - 90f : 0f;
            shoulder.localRotation = Quaternion.Euler(0f, 0f, angle);
            var size = sleeve.sizeDelta;
            if (!Mathf.Approximately(size.y, length))
            {
                sleeve.sizeDelta = new Vector2(size.x, length);
            }

            paw.localPosition = new Vector3(0f, length, 0f);
        }
    }
}
