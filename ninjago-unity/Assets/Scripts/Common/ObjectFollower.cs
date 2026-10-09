using UnityEngine;

#nullable enable

namespace Common
{
    public class ObjectFollower : MonoBehaviour
    {
        [SerializeField] Transform target = null!;
        [SerializeField] GameObject childRoot = null!;

        void FixedUpdate()
        {
            transform.localPosition = target.localPosition;
            childRoot.SetActive(target.gameObject.activeSelf);
        }
    }
}
