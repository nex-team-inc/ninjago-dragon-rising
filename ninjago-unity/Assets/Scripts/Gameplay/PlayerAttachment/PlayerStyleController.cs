#nullable enable

using UnityEngine;

namespace Nex
{
    public class PlayerStyleController : MonoBehaviour
    {
        #region Initialization

        public void Initialize(
            int styleIndex)
        {
            var pickers = gameObject.GetComponentsInChildren<PlayerStylePicker>(true);
            foreach (var picker in pickers)
            {
                picker.SetStyleIndex(styleIndex);
            }
        }

        #endregion
    }
}
