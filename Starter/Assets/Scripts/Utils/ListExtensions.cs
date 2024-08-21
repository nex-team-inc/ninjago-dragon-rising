#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Utils
{
    public static class ListExtensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            var n = list.Count;
            for (var i = 0; i < n - 1; i++)
            {
                var r = Random.Range(i, n);
                (list[i], list[r]) = (list[r], list[i]);
            }
        }
    }
}
