#nullable enable

using MoreMountains.Feedbacks;
using UnityEngine;

namespace Nex.MMF
{
    [AddComponentMenu("")]
    [FeedbackHelp("This feedback will play a specific sound effect through SfxManager")]
    [FeedbackPath("Nex/SfxManager")]
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once RequiredBaseTypesIsNotInherited
    public class MMF_SfxManager : MMF_Feedback
    {
        // ReSharper disable once FieldCanBeMadeReadOnly.Global
        // ReSharper disable once MemberCanBePrivate.Global
        // ReSharper disable once ConvertToConstant.Global
        [MMFInspectorGroup("Sound Effect", true, 31, true)] [Tooltip("The sound effect to play when this is triggered")]
        public SfxManager.SoundEffect soundEffect = SfxManager.SoundEffect.GenericEnter;

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1)
        {
            if (Owner.DurationMultiplier < 0.9f) return; // Don't play sound if it is in speed mode.
            SfxManager.Instance.PlaySoundEffect(soundEffect);
        }
    }
}
