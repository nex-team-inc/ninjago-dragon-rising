#nullable enable

using UnityEngine;

namespace Nex
{
    public class PlayerAttachmentPin : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spriteRenderer = null!;
        [SerializeField] PoseNodeIndex nodeIndex;
        [SerializeField] Vector2 offsetInInches = Vector2.zero;

        public PoseNodeIndex NodeIndex => nodeIndex;
        public Vector2 OffsetInInches => offsetInInches;

        void Awake()
        {
            spriteRenderer.enabled = false;
        }
    }
}
