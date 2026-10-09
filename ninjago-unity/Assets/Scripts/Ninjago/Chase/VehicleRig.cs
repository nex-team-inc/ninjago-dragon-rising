#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>The block car or skycraft: placed by the runner, banks and pitches with its own motion.</summary>
    public class VehicleRig : MonoBehaviour
    {
        [Header("Body")]
        [SerializeField] Transform body = null!;
        [Header("Player Steer Markers")]
        [Tooltip("One per player index.")]
        [SerializeField] PlayerSteerMarker[] markers = null!;
        [Header("Bank Degrees Per Unit Speed")]
        [SerializeField] float bankPerSpeed = 4f;
        [Header("Pitch Degrees Per Unit Speed")]
        [SerializeField] float pitchPerSpeed = 5f;

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> activePlayers)
        {
            foreach (var marker in markers) marker.gameObject.SetActive(false);
            foreach (var player in activePlayers)
            {
                var marker = markers[player.PlayerIndex];
                marker.gameObject.SetActive(true);
                marker.Initialize(player.PlayerIndex, player.Color);
            }
        }

        #endregion

        #region Public API

        public Vector3 Position => transform.position;

        public void SetPose(Vector2 position, Vector2 velocity)
        {
            transform.localPosition = new Vector3(position.x, position.y, 0f);
            body.localRotation = Quaternion.Euler(-velocity.y * pitchPerSpeed, 0f, -velocity.x * bankPerSpeed);
        }

        public void SetSteer(int playerIndex, Vector2 steer, bool tracked)
        {
            markers[playerIndex].SetSteer(steer, tracked);
        }

        #endregion
    }
}
