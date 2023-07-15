using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#nullable enable

namespace Nex
{
    public class BoardButton : MonoBehaviour
    {
        [SerializeField] List<string> validTags = null!;
        [SerializeField] ParticleSystem hitParticle = null!;

        public event UnityAction<BoardButton, GameObject>? Hit;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (validTags.Contains(other.tag))
            {
                hitParticle.transform.position = other.transform.position;
                hitParticle.Play();
                Hit?.Invoke(this, other.gameObject);
            }
        }
    }
}
