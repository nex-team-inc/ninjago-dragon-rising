using UnityEngine;

namespace Common
{
    public class ObjectFollower : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] GameObject childRoot;

        void FixedUpdate()
        {
            transform.localPosition = target.localPosition;
            childRoot.SetActive(target.gameObject.activeSelf);
        }
    }
}
