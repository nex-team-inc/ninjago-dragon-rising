#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The stones a slashed rock breaks into: they spread into rows around the hang point, hover while the KICK prompt
    /// is open, then all fly back into the boss together on the kick, or fall when no kick comes.
    /// </summary>
    public class RockPieces : MonoBehaviour
    {
        [Header("Stone Prefab")]
        [SerializeField] Transform piecePrefab = null!;
        [Header("Stones Per Row")]
        [SerializeField] int columns = 4;
        [Header("Column Spacing")]
        [SerializeField] float columnSpacing = 0.75f;
        [Header("Row Spacing")]
        [SerializeField] float rowSpacing = 0.65f;
        [Header("Launch Stagger")]
        [Tooltip("Seconds between two stones leaving on the kick, so they fly as a stream.")]
        [SerializeField] float launchStaggerSeconds = 0.04f;
        [Header("Ground Height")]
        [SerializeField] float groundHeight;

        readonly List<Transform> pieces = new();
        Transform hangPoint = null!;
        Camera laneCamera = null!;
        BossRig boss = null!;
        StoneKickConfig config = null!;
        int count;

        #region Initialization

        public void Initialize(Transform aHangPoint, Camera aLaneCamera, BossRig aBoss, StoneKickConfig aConfig)
        {
            hangPoint = aHangPoint;
            laneCamera = aLaneCamera;
            boss = aBoss;
            config = aConfig;
        }

        #endregion

        #region Public API

        public async UniTask SplitAsync(Vector3 from, int stones, CancellationToken cancellationToken)
        {
            count = stones;
            while (pieces.Count < count) pieces.Add(Instantiate(piecePrefab, transform));
            for (var i = 0; i < pieces.Count; i++)
            {
                pieces[i].gameObject.SetActive(i < count);
                pieces[i].position = from;
            }

            await AnimateAsync(config.SplitSeconds, t =>
            {
                var eased = 1f - (1f - t) * (1f - t);
                for (var i = 0; i < count; i++) pieces[i].position = Vector3.Lerp(from, Slot(i), eased);
            }, cancellationToken);
        }

        public void Bob()
        {
            var time = Time.unscaledTime;
            for (var i = 0; i < count; i++) pieces[i].position = Slot(i) + Vector3.up * (Mathf.Sin(time * 3f + i * 1.7f) * 0.05f);
        }

        /// <summary>Every stone flies back into the boss; returns the seconds until the last one lands.</summary>
        public float LaunchAll(CancellationToken cancellationToken)
        {
            for (var i = 0; i < count; i++) LaunchAsync(pieces[i], i * launchStaggerSeconds, cancellationToken).Forget();
            return config.PieceFlySeconds + (count - 1) * launchStaggerSeconds;
        }

        public void DropAll(CancellationToken cancellationToken)
        {
            for (var i = 0; i < count; i++) DropAsync(pieces[i], cancellationToken).Forget();
        }

        #endregion

        #region Helpers

        // Rows of up to `columns` stones centered on the hang point, facing the camera.
        Vector3 Slot(int index)
        {
            var rows = (count + columns - 1) / columns;
            var row = index / columns;
            var inRow = row < rows - 1 ? columns : count - row * columns;
            var x = (index % columns - (inRow - 1) * 0.5f) * columnSpacing;
            var y = ((rows - 1) * 0.5f - row) * rowSpacing;
            return hangPoint.position + laneCamera.transform.right * x + Vector3.up * y;
        }

        async UniTaskVoid LaunchAsync(Transform piece, float delay, CancellationToken cancellationToken)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, cancellationToken);
            var from = piece.position;
            var to = boss.ChestPosition;
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.PieceLaunch, from);
            await AnimateAsync(config.PieceFlySeconds, t =>
            {
                piece.position = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.5f);
                piece.Rotate(new Vector3(600f, 0f, 200f) * Time.unscaledDeltaTime, Space.Self);
            }, cancellationToken);
            piece.gameObject.SetActive(false);
            boss.PlayFlinch();
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.BossHit, to);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.PieceHit);
        }

        async UniTaskVoid DropAsync(Transform piece, CancellationToken cancellationToken)
        {
            var from = piece.position;
            var to = new Vector3(from.x, groundHeight, from.z);
            await AnimateAsync(config.PieceDropSeconds, t => piece.position = Vector3.Lerp(from, to, t * t), cancellationToken);
            piece.gameObject.SetActive(false);
        }

        static async UniTask AnimateAsync(float seconds, Action<float> step, CancellationToken cancellationToken)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                step(Mathf.Clamp01(elapsed / seconds));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            step(1f);
        }

        #endregion
    }
}
