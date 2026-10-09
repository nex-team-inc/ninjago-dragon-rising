#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex
{
    public class PlayerStylePicker : MonoBehaviour
    {
        [SerializeField] List<GameObject> componentByStyleIndex = null!;

        public void SetStyleIndex(int styleIndex)
        {
            for (var index = 0; index < componentByStyleIndex.Count; index++)
            {
                componentByStyleIndex[index].SetActive(index == styleIndex);
            }
        }
    }
}
