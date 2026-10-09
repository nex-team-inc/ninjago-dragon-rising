#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Chest Chase world: the car on a three-wide road, then the skycraft in an open air lane. The vehicle stays at
    /// z = 0 and the world streams toward it. Every active player's chest steers the one vehicle through SharedSteer.
    /// </summary>
    public class RunnerWorld : MonoBehaviour
    {
        static readonly VehicleType[] segments = { VehicleType.Car, VehicleType.Skycraft };

        [Header("World Camera")]
        [SerializeField] Camera worldCamera = null!;
        [Header("Car Camera Pose")]
        [SerializeField] Transform carCameraPose = null!;
        [Header("Skycraft Camera Pose")]
        [SerializeField] Transform skyCameraPose = null!;
        [Header("Car")]
        [SerializeField] VehicleRig car = null!;
        [Header("Skycraft")]
        [SerializeField] VehicleRig skycraft = null!;
        [Header("Road Scenery")]
        [SerializeField] GameObject roadScenery = null!;
        [Header("Sky Scenery")]
        [SerializeField] GameObject skyScenery = null!;
        [Header("Road Strip")]
        [SerializeField] ScrollingStrip roadStrip = null!;
        [Header("Sky Strip")]
        [SerializeField] ScrollingStrip skyStrip = null!;
        [Header("Brick Barrier Pool")]
        [SerializeField] ObstacleBlockPool barrierPool = null!;
        [Header("Brick Barrier Prefab")]
        [SerializeField] ObstacleBlock barrierPrefab = null!;
        [Header("Floating Block Pool")]
        [SerializeField] ObstacleBlockPool floatingBlockPool = null!;
        [Header("Floating Block Prefab")]
        [SerializeField] ObstacleBlock floatingBlockPrefab = null!;
        [Header("Barrier Size")]
        [Tooltip("Height and depth of a road barrier; its width is the lane width.")]
        [SerializeField] Vector2 barrierHeightDepth = new(1.1f, 0.7f);
        [Header("Floating Block Depth")]
        [SerializeField] float floatingBlockDepth = 0.9f;
        [Header("Cell Fill")]
        [Tooltip("Fraction of a cell an obstacle covers; the rest of the cell is a forgiving margin.")]
        [SerializeField, Range(0.5f, 1f)] float cellFill = 0.9f;
        [Header("Despawn Z")]
        [SerializeField] float despawnZ = -8f;
        [Header("Bump Shake")]
        [SerializeField] float bumpShakeAmount = 0.18f;
        [Header("Bump Shake Seconds")]
        [SerializeField] float bumpShakeSeconds = 0.3f;

        sealed class Row
        {
            public float z;
            public int mask;
            public int index;
            public bool resolved;
            public readonly List<ObstacleBlock> blocks = new();
        }

        readonly List<Row> rows = new();
        readonly SharedSteer sharedSteer = new();
        readonly Vector2?[] steers = new Vector2?[SimulatedBody.MaxPlayers];
        readonly int[] dodges = new int[segments.Length];
        readonly int[] hits = new int[segments.Length];
        IReadOnlyList<PlayerBody> players = null!;
        ChaseConfig config = null!;
        VehicleRig vehicle = null!;
        Vector2 vehiclePosition;
        Vector2 vehicleVelocity;
        float speedFactor = 1f;
        float shakeLeft;
        int obstacleCount;

        /// <summary>A vehicle appeared (the car at start, the skycraft at the transition).</summary>
        public event Action<VehicleType>? VehicleShown;
        public event Action? CountsChanged;

        public VehicleType CurrentVehicle { get; private set; }
        public float TimeLeft { get; private set; }
        public int GetDodges(VehicleType type) => dodges[(int)type];
        public int GetHits(VehicleType type) => hits[(int)type];
        public float SteerShare(int playerIndex) => sharedSteer.EffortShare(playerIndex);

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> activePlayers, ChaseConfig aConfig, RenderTexture target)
        {
            players = activePlayers;
            config = aConfig;
            worldCamera.targetTexture = target;
            car.Initialize(activePlayers);
            skycraft.Initialize(activePlayers);
            barrierPool.Initialize(barrierPrefab, 6);
            floatingBlockPool.Initialize(floatingBlockPrefab, 12);
            ShowSegment(VehicleType.Car);
        }

        #endregion

        #region Run

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            await DriveAsync(config.IntroSeconds, cancellationToken);
            for (var i = 0; i < segments.Length; i++)
            {
                if (i > 0)
                {
                    ShowSegment(segments[i]);
                    await DriveAsync(config.TransitionSeconds, cancellationToken);
                }

                await RunSegmentAsync(segments[i], cancellationToken);
            }
        }

        async UniTask RunSegmentAsync(VehicleType type, CancellationToken cancellationToken)
        {
            var settings = config.GetVehicle(type);
            var patterns = new ObstaclePatterns(type);
            NinjagoAnalytics.SegmentStart(type, players.Count);
            var elapsed = 0f;
            var nextSpawn = settings.openingSeconds;
            var lastSpawn = settings.segmentSeconds - settings.obstacleLeadSeconds;
            while (elapsed < settings.segmentSeconds)
            {
                var deltaTime = Time.deltaTime;
                elapsed += deltaTime;
                TimeLeft = Mathf.Max(0f, settings.segmentSeconds - elapsed);
                if (elapsed >= nextSpawn && nextSpawn <= lastSpawn)
                {
                    Spawn(type, settings, patterns.Next());
                    nextSpawn += settings.obstacleSpacingSeconds;
                }

                Step(settings, deltaTime);
                MoveRows(type, settings, settings.speed * speedFactor * deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            ClearRows();
        }

        // Steering and scenery without obstacles: the intro and the car-to-skycraft transition.
        async UniTask DriveAsync(float seconds, CancellationToken cancellationToken)
        {
            var settings = config.GetVehicle(CurrentVehicle);
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                Step(settings, Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        #endregion

        #region Simulation

        void Step(ChaseConfig.VehicleSettings settings, float deltaTime)
        {
            for (var i = 0; i < steers.Length; i++) steers[i] = null;
            foreach (var player in players)
            {
                if (player.TryGetLeanInches(out var lean))
                {
                    steers[player.PlayerIndex] = ChestSteer.Steer(lean, settings.usesVerticalLean, config.DeadzoneInches,
                        config.FullLeanInches, config.FullRiseInches);
                }

                var steer = steers[player.PlayerIndex];
                vehicle.SetSteer(player.PlayerIndex, steer ?? Vector2.zero, steer.HasValue);
            }

            var blended = sharedSteer.Blend(steers, config.PlayerOneSteerShare, deltaTime);
            var target = new Vector2(blended.x * settings.halfWidth, settings.centerHeight + blended.y * settings.halfHeight);
            vehiclePosition.x = Mathf.SmoothDamp(vehiclePosition.x, target.x, ref vehicleVelocity.x, settings.steerSmoothingSeconds,
                settings.maxSteerSpeed, deltaTime);
            vehiclePosition.y = Mathf.SmoothDamp(vehiclePosition.y, target.y, ref vehicleVelocity.y, settings.steerSmoothingSeconds,
                settings.maxSteerSpeed, deltaTime);
            vehicle.SetPose(vehiclePosition, vehicleVelocity);

            speedFactor = Mathf.MoveTowards(speedFactor, 1f, config.BumpSpeedDrop / config.BumpRecoverSeconds * deltaTime);
            var distance = settings.speed * speedFactor * deltaTime;
            (CurrentVehicle == VehicleType.Car ? roadStrip : skyStrip).Scroll(distance);
            ShakeCamera(deltaTime);
        }

        void Spawn(VehicleType type, ChaseConfig.VehicleSettings settings, int mask)
        {
            var row = new Row { z = settings.speed * settings.obstacleLeadSeconds, mask = mask, index = ++obstacleCount };
            var rowCount = type == VehicleType.Car ? 1 : ObstaclePatterns.Rows;
            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                for (var column = 0; column < ObstaclePatterns.Columns; column++)
                {
                    if (!ObstaclePatterns.IsBlocked(mask, column, rowIndex)) continue;
                    var block = type == VehicleType.Car ? barrierPool.Get() : floatingBlockPool.Get();
                    var center = CellCenter(type, settings, column, rowIndex);
                    var size = type == VehicleType.Car
                        ? new Vector3(settings.cellWidth * cellFill, barrierHeightDepth.x, barrierHeightDepth.y)
                        : new Vector3(settings.cellWidth * cellFill, settings.cellHeight * cellFill, floatingBlockDepth);
                    var y = type == VehicleType.Car ? size.y * 0.5f : center.y;
                    block.Show(new Vector3(center.x, y, row.z), size);
                    row.blocks.Add(block);
                }
            }

            rows.Add(row);
        }

        void MoveRows(VehicleType type, ChaseConfig.VehicleSettings settings, float distance)
        {
            for (var i = rows.Count - 1; i >= 0; i--)
            {
                var row = rows[i];
                row.z -= distance;
                foreach (var block in row.blocks)
                {
                    var position = block.transform.localPosition;
                    block.transform.localPosition = new Vector3(position.x, position.y, row.z);
                }

                if (!row.resolved && row.z <= 0f)
                {
                    row.resolved = true;
                    Resolve(type, settings, row);
                }

                if (row.z >= despawnZ) continue;
                ReleaseRow(row);
                rows.RemoveAt(i);
            }
        }

        void Resolve(VehicleType type, ChaseConfig.VehicleSettings settings, Row row)
        {
            var playerOneSteer = steers[0] ?? Vector2.zero;
            var playerTwoSteer = steers[1] ?? Vector2.zero;
            if (!Overlaps(type, settings, row.mask))
            {
                dodges[(int)type]++;
                NinjagoAnalytics.ObstacleDodge(type, row.index, sharedSteer.DominantPlayer, playerOneSteer, playerTwoSteer);
                CountsChanged?.Invoke();
                return;
            }

            hits[(int)type]++;
            speedFactor = 1f - config.BumpSpeedDrop;
            shakeLeft = bumpShakeSeconds;
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.VehicleBump);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.VehicleBump, vehicle.Position + Vector3.forward * 0.8f);
            NinjagoAnalytics.ObstacleHit(type, row.index, sharedSteer.DominantPlayer, playerOneSteer, playerTwoSteer);
            CountsChanged?.Invoke();
        }

        bool Overlaps(VehicleType type, ChaseConfig.VehicleSettings settings, int mask)
        {
            var rowCount = type == VehicleType.Car ? 1 : ObstaclePatterns.Rows;
            var halfCell = new Vector2(settings.cellWidth, settings.cellHeight) * (cellFill * 0.5f);
            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                for (var column = 0; column < ObstaclePatterns.Columns; column++)
                {
                    if (!ObstaclePatterns.IsBlocked(mask, column, rowIndex)) continue;
                    var center = CellCenter(type, settings, column, rowIndex);
                    var overlapX = Mathf.Abs(vehiclePosition.x - center.x) < halfCell.x + settings.vehicleHalfExtents.x;
                    // The car never leaves the road, so a barrier in its lane always hits.
                    var overlapY = type == VehicleType.Car
                        || Mathf.Abs(vehiclePosition.y - center.y) < halfCell.y + settings.vehicleHalfExtents.y;
                    if (overlapX && overlapY) return true;
                }
            }

            return false;
        }

        #endregion

        #region Helpers

        static Vector2 CellCenter(VehicleType type, ChaseConfig.VehicleSettings settings, int column, int rowIndex)
        {
            var x = (column - 1) * settings.cellWidth;
            var y = type == VehicleType.Car ? 0f : settings.centerHeight + (rowIndex - 1) * settings.cellHeight;
            return new Vector2(x, y);
        }

        void ShowSegment(VehicleType type)
        {
            CurrentVehicle = type;
            var isCar = type == VehicleType.Car;
            roadScenery.SetActive(isCar);
            skyScenery.SetActive(!isCar);
            car.gameObject.SetActive(isCar);
            skycraft.gameObject.SetActive(!isCar);
            vehicle = isCar ? car : skycraft;
            var settings = config.GetVehicle(type);
            vehiclePosition = new Vector2(0f, settings.centerHeight);
            vehicleVelocity = Vector2.zero;
            vehicle.SetPose(vehiclePosition, vehicleVelocity);
            var pose = isCar ? carCameraPose : skyCameraPose;
            worldCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            TimeLeft = settings.segmentSeconds;
            VehicleShown?.Invoke(type);
        }

        void ShakeCamera(float deltaTime)
        {
            var pose = CurrentVehicle == VehicleType.Car ? carCameraPose : skyCameraPose;
            if (shakeLeft <= 0f)
            {
                worldCamera.transform.position = pose.position;
                return;
            }

            shakeLeft -= deltaTime;
            worldCamera.transform.position = pose.position + UnityEngine.Random.insideUnitSphere * (bumpShakeAmount * shakeLeft / bumpShakeSeconds);
        }

        void ClearRows()
        {
            foreach (var row in rows) ReleaseRow(row);
            rows.Clear();
        }

        static void ReleaseRow(Row row)
        {
            foreach (var block in row.blocks) block.Release();
            row.blocks.Clear();
        }

        #endregion
    }
}
