using System.Collections.Generic;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class GameBoardManager : MonoBehaviour
    {
        [SerializeField] List<BoardButton> buttons = null!;
        [SerializeField] AudioSource audioSource = null!;
        [SerializeField] AudioClip hitClip = null!;

        #region Life Cycle

        void Awake()
        {
            foreach (var button in buttons)
            {
                button.Hit += ButtonOnHit;
            }
        }

        #endregion

        #region Event

        void ButtonOnHit(BoardButton button, GameObject target)
        {
            audioSource.PlayOneShot(hitClip);
        }

        #endregion
    }
}
