using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Nex.MMF
{
	[AddComponentMenu("")]
	[FeedbackHelp("This feedback lets you play a sprite sequence over time.")]
	[FeedbackPath("Nex/UI/SpriteRendererSpriteSequence")]
    public class MMF_SpriteRendererSpriteSequence : MMF_Feedback
    {
		/// a static bool used to disable all feedbacks of this type at once
		public static bool FeedbackTypeAuthorized = true;

		[MMFInspectorGroup("Target SpriteRenderer", true, 12, true)]
		/// the SpriteRenderer to affect when playing the feedback
		[Tooltip("the SpriteRenderer to affect when playing the feedback")]
		public SpriteRenderer BoundSpriteRenderer;

        [MMFInspectorGroup("Sprite Sequence", true, 30, true)]

		[Tooltip("how long does it take to play through the whole sequence")]
		public float Duration = 1f;

        [Tooltip("The sequence of sprites to play when this is triggered")]
	    public Sprite[] Sprites;

		public override float FeedbackDuration { get { return ApplyTimeMultiplier(Duration); } set { Duration = value; } }

		protected Coroutine _coroutine;

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1)
        {
			if (!Active || !FeedbackTypeAuthorized)
			{
				return;
			}

			if (Sprites.Length == 0) return;

			if (_coroutine != null) Owner.StopCoroutine(_coroutine);
			_coroutine = Owner.StartCoroutine(ImageSequence());
        }

        protected virtual IEnumerator ImageSequence()
        {
			float journey = NormalPlayDirection ? 0f : FeedbackDuration;
			IsPlaying = true;
			while ((journey >= 0) && (journey <= FeedbackDuration) && (FeedbackDuration > 0))
			{
				float remappedTime = MMFeedbacksHelpers.Remap(journey, 0f, FeedbackDuration, 0f, 1f);

				SetSprite(remappedTime);

				journey += NormalPlayDirection ? FeedbackDeltaTime : -FeedbackDeltaTime;
				yield return null;
			}
			SetSprite(FinalNormalizedTime);
			_coroutine = null;
			IsPlaying = false;
			yield return null;
        }

        protected virtual void SetSprite(float mappedTime)
        {
	        var index = Mathf.Min(Mathf.FloorToInt(mappedTime * Sprites.Length), Sprites.Length - 1);
	        BoundSpriteRenderer.sprite = Sprites[index];
        }
    }
}
