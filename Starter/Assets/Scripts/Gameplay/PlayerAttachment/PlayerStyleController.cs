#nullable enable

using UnityEngine;

namespace Nex
{
    public class PlayerStyleController : MonoBehaviour
    {
        OnePlayerPreviewPoseEngine playerPreviewPoseEngine = null!;

        #region Initialization

        public void Initialize(
            int styleIndex,
            OnePlayerPreviewPoseEngine aPlayerPreviewPoseEngine)
        {
            playerPreviewPoseEngine = aPlayerPreviewPoseEngine;

            var pickers = gameObject.GetComponentsInChildren<PlayerStylePicker>(true);
            foreach (var picker in pickers)
            {
                picker.SetStyleIndex(styleIndex);
            }
        }

        #endregion
    }
}
