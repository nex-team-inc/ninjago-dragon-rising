#nullable enable

using DG.Tweening;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Animated controls illustration for the calibration step (GDD v2 §16): the upper paw holds the ball, the lower paw
    /// (the cue) rests below, thrusts up into the ball paw, a burst flashes and the ball shoots up, then the cue paw
    /// returns and a new ball appears. Flash() punches the ball on a successful test strike. Unscaled time, tweens
    /// die with the object.
    /// </summary>
    public sealed class CalibrationTutorialIllustration : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField] RectTransform ballPaw = null!;
        [SerializeField] RectTransform cuePaw = null!;
        [SerializeField] RectTransform ball = null!;
        [SerializeField] CanvasGroup ballGroup = null!;
        [SerializeField] RectTransform burst = null!;
        [SerializeField] CanvasGroup burstGroup = null!;

        [Header("Motion (UI units, relative to the ball paw)")]
        [SerializeField] Vector2 restOffset = new(240f, -120f);
        [SerializeField] Vector2 contactOffset = new(88f, -24f);
        [Tooltip("Where the ball sits on the ball paw.")]
        [SerializeField] Vector2 ballOffset = new(0f, 72f);
        [SerializeField, Range(0f, 600f)] float ballFlyHeight = 200f;
        [SerializeField, Range(0.05f, 1f)] float thrustSeconds = 0.16f;
        [SerializeField, Range(0.1f, 3f)] float returnSeconds = 0.6f;
        [SerializeField, Range(0f, 3f)] float restSeconds = 0.7f;
        [Tooltip("Extra ball scale on the success flash.")]
        [SerializeField, Range(0f, 1f)] float flashPunch = 0.35f;

        Sequence? loop;

        public void Play()
        {
            if (loop != null) return;
            var paw = ballPaw.anchoredPosition;
            var ballHome = paw + ballOffset;
            cuePaw.anchoredPosition = paw + restOffset;
            ball.anchoredPosition = ballHome;
            ballGroup.alpha = 1f;
            burstGroup.alpha = 0f;
            burst.anchoredPosition = (paw + contactOffset + ballHome) * 0.5f;
            loop = DOTween.Sequence()
                .AppendInterval(restSeconds)
                .Append(cuePaw.DOAnchorPos(paw + contactOffset, thrustSeconds).SetEase(Ease.InQuad))
                .AppendCallback(() => burst.localScale = Vector3.one * 0.5f)
                .Append(burstGroup.DOFade(1f, 0.04f))
                .Join(burst.DOScale(1.2f, 0.25f).SetEase(Ease.OutQuad))
                .Join(ballPaw.DOPunchScale(Vector3.one * 0.12f, 0.25f, 6, 0.6f))
                .Join(ball.DOAnchorPos(ballHome + new Vector2(0f, ballFlyHeight), 0.4f).SetEase(Ease.OutQuad))
                .Join(ballGroup.DOFade(0f, 0.4f).SetEase(Ease.InQuad))
                .Insert(restSeconds + thrustSeconds + 0.12f, burstGroup.DOFade(0f, 0.2f))
                .AppendCallback(() => ball.anchoredPosition = ballHome)
                .Append(cuePaw.DOAnchorPos(paw + restOffset, returnSeconds).SetEase(Ease.OutSine))
                .Join(ballGroup.DOFade(1f, returnSeconds * 0.5f))
                .SetLoops(-1)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        public void Flash()
        {
            ball.DOKill(true);
            ball.DOPunchScale(Vector3.one * flashPunch, 0.35f, 8, 0.5f).SetUpdate(true).SetLink(gameObject);
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
