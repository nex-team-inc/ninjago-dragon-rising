#nullable enable

using DG.Tweening;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Animated pose tutorial for the calibration step: the right paw (cue) thrusts into the left paw (ball) in a
    /// loop. Flash() punches the ball on a successful test strike. Unscaled time, tweens die with the object.
    /// </summary>
    public sealed class CalibrationTutorialIllustration : MonoBehaviour
    {
        [Header("Paws")]
        [SerializeField] RectTransform leftPaw = null!;
        [SerializeField] RectTransform rightPaw = null!;

        [Header("Motion (UI px, relative to the left paw)")]
        [SerializeField] Vector2 restOffset = new(230f, -130f);
        [SerializeField] Vector2 contactOffset = new(70f, -30f);
        [SerializeField, Range(0.05f, 1f)] float thrustSeconds = 0.18f;
        [SerializeField, Range(0.1f, 3f)] float returnSeconds = 0.7f;
        [SerializeField, Range(0f, 3f)] float restSeconds = 0.6f;
        [Tooltip("Extra ball scale on the success flash.")]
        [SerializeField, Range(0f, 1f)] float flashPunch = 0.35f;

        Sequence? loop;

        public void Play()
        {
            Stop();
            var ball = leftPaw.anchoredPosition;
            rightPaw.anchoredPosition = ball + restOffset;
            loop = DOTween.Sequence()
                .AppendInterval(restSeconds)
                .Append(rightPaw.DOAnchorPos(ball + contactOffset, thrustSeconds).SetEase(Ease.InQuad))
                .Append(leftPaw.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.6f))
                .Append(rightPaw.DOAnchorPos(ball + restOffset, returnSeconds).SetEase(Ease.OutSine))
                .SetLoops(-1)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        public void Flash()
        {
            leftPaw.DOKill(true);
            leftPaw.DOPunchScale(Vector3.one * flashPunch, 0.35f, 8, 0.5f).SetUpdate(true).SetLink(gameObject);
        }

        public void Stop()
        {
            loop?.Kill();
            loop = null;
        }

        void OnDisable()
        {
            Stop();
        }
    }
}
