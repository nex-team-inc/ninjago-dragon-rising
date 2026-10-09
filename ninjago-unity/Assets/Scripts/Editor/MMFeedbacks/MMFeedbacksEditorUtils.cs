using System.Linq;
using MoreMountains.Feedbacks;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Nex.Dev.Editor
{
    // ReSharper disable once InconsistentNaming
    public static class MMFeedbacksEditorUtils
    {
        [MenuItem("Nex/MMFeedbacks/Convert Feedbacks to Unscaled Time")]
        public static void ConvertFeedbacksToUnscaledTime() {
            ConvertFeedbacksTimescale(TimescaleModes.Unscaled);
        }

        [MenuItem("Nex/MMFeedbacks/Convert Feedbacks to Scaled Time")]
        public static void ConvertFeedbacksToScaledTime() {
            ConvertFeedbacksTimescale(TimescaleModes.Scaled);
        }

        static void ConvertFeedbacksTimescale(TimescaleModes timescaleModes)
        {
            foreach (var host in Selection.transforms)
            {
                if (!host.TryGetComponent<MMFeedbacks>(out var feedbacks)) continue;
                Undo.RecordObject(feedbacks, $"Switch to {timescaleModes}");
                feedbacks.PlayerTimescaleMode = timescaleModes;
                // Check if it is MMF_Player, which has a different feedback list.
                if (feedbacks is MMF_Player player)
                {
                    foreach (var feedback in player.FeedbacksList)
                    {
                        feedback.Timing.TimescaleMode = timescaleModes;
                    }
                }

                foreach (var feedback in feedbacks.Feedbacks)
                {
                    feedback.Timing.TimescaleMode = timescaleModes;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(feedbacks);
            }
        }

        [MenuItem("Nex/MMFeedbacks/Set Forced Unscaled")]
        public static void SetForcedUnscaled() {
            SetForcedTimescaleMode(TimescaleModes.Unscaled);
        }

        [MenuItem("Nex/MMFeedbacks/Set Forced Scaled")]
        public static void SetForcedScaled() {
            SetForcedTimescaleMode(TimescaleModes.Scaled);
        }

        static void SetForcedTimescaleMode(TimescaleModes timescaleModes) {
            var feedbacksArray = Selection.transforms.SelectMany(host =>
                host.TryGetComponent<MMFeedbacks>(out var feedbacks) ? Enumerable.Repeat(feedbacks, 1) : Enumerable.Empty<MMFeedbacks>()
                ).Cast<Object>().ToArray();
            using var serializedObject = new SerializedObject(feedbacksArray);
            serializedObject.FindProperty(nameof(MMFeedbacks.ForceTimescaleMode)).boolValue = true;
            serializedObject.FindProperty(nameof(MMFeedbacks.ForcedTimescaleMode)).intValue = (int)timescaleModes;
            serializedObject.ApplyModifiedProperties();
        }

        public static void SetForcedTimescaleMode(MMFeedbacks feedbacks, TimescaleModes timescaleModes)
        {
            var changed = false;
            if (!feedbacks.ForceTimescaleMode)
            {
                feedbacks.ForceTimescaleMode = true;
                changed = true;
            }

            if (feedbacks.ForcedTimescaleMode != timescaleModes)
            {
                feedbacks.ForcedTimescaleMode = timescaleModes;
                changed = true;
            }

            if (changed) EditorUtility.SetDirty(feedbacks);
        }
    }
}
