# Detection, Setup, Camera Previews, Player Indicators, Multiplayer

Reference note for Billiard Rogue implementation agents. Everything here was read from source unless tagged
**[UNVERIFIED]**. Paths are relative to `Starter/` unless absolute.

- MDK: `Library/PackageCache/team.nex.mdk@54ffe9eb7301` (v3.4.3, namespace `Jazz`)
- MDK body: `Library/PackageCache/team.nex.mdk.body@97d2a24126aa` (v3.4.3, namespace `Jazz`)
- NexCamera: `Library/PackageCache/team.nex.nex-camera@4df0a2cd4800`
- Starter code: `Assets/Scripts/Gameplay/**` (namespace `Nex`)

---

## 0. TL;DR answers

| # | Question | Answer |
|---|----------|--------|
| 1 | Is `LeftHand` anatomical left? Which preview side? | Yes, assuming the camera frame is mirrored, and it is: NexCamera always mirrors on Mac/Win Editor and mirrors front-facing cameras on AOSP. `BodyPoseDetector.prefab` sets `autoFlipAsFrontFacingPose: 1`, so `Left*` shoulder/elbow/wrist always have a **smaller x** than `Right*` in the frame. The preview shows that same mirrored frame, so the player's left paw appears on the **left side of the preview/screen**, like a mirror. `Nex.PoseNodeIndex.LeftHand` = `lerp(LeftElbow, LeftWrist, 1.35)`. Code-verified guarantee: "Left = screen-left side". The claim that this is anatomical is by design; on-device mirroring happens in native code **[UNVERIFIED on hardware]**. |
| 2 | Per-frame hand position and velocity, body-relative, robust to height and distance | Use one `OnePlayerDetectionEngine` per player. On `NewDetectionCapturedAndProcessed`, compute `(GetNodePosition(X, true) - GetNodePosition(Chest, true)) / DistancePerInch`. The engine pre-scales every pose around the chest by `1/(RawPpi*58)`, so the result is in **ppi-inches**. ppi comes from limb lengths ÷ adult reference limb sizes, which makes these units normalized for both distance and body size. Velocity: finite difference per event (the smoothed stream runs at about 60 Hz), or finite difference of Original (raw) nodes only when `LastBodyPoseDetectionResult.original.frameTime` changes (about 30 Hz, true camera timestamps). See §15.2. |
| 3 | 1 vs 2 players | Call `DetectionManager.Initialize(numOfPlayers)` once per scene. Then instantiate one `OnePlayerDetectionEngine` per `playerIndex` and call `Initialize(playerIndex, bpdm)`. `PreviewsManager.Initialize(numOfPlayers, …)` creates one setup frame per player, and `SetupStateManager` waits for **all** players. Player 0 stands left (x ratio 0.3) and player 1 stands right (0.7). Changing the count means reloading the scene, because Initialize is not idempotent. |
| 4 | Editor testing without a person or camera | The **S key** resolves the *currently awaited* `WaitForGoodPlayerPosition` / `WaitForRaiseHand`, so press it once per stage. It works with no camera. The Mac webcam works through `NexWebCamera` (WebCamTexture, mirrored). Video input works through `CvDetectionManager.frameProviderType = VideoClip` plus the inactive `VideoFrameProvider` child. MDK has no pose mock API. Build an `IPawInputSource` keyboard mock and keep strike logic in a pure C# class that EditMode tests can drive. |
| 5 | Corner PiP with head indicators and active-player highlight | Place an `AreaPreviewFrame` (RawImage) plus a `PlayerIndicatorsManager` on a **Screen Space Overlay** HUD canvas, so URP post effects don't touch the feed. Copy the ARGameExample wiring. Highlighting needs a small additive extension: keep the indicator list and add `SetActivePlayer(int)`. See §15.5. |

---

## 1. Data flow

```
NexCamera (mirrored frame) ─► CameraFrameProvider ─► CvDetectionManager (Jazz)
                                                        │  numOfPlayers, playerPositions
                                                        ▼
                                          BodyPoseDetectionManager (Jazz, "BodyPoseDetector")
      raw px, top-left origin ┌───────────────┤ captureBodyPoseDetection(BodyPoseDetection)        ─► Nex.SetupDetector (setup issues)
                              │               │ captureAspectNormalizedDetection(Result{orig==proc}) ─► PlayAreaController, PreviewFramePlayerIndicator,
                              │               │   (unsmoothed, camera rate ≤30 Hz)                     PlayerFocusPreviewFrame, PlayerPreviewSprite
                              │               ▼
                              │    BodyPoseDefaultSmoother "DefaultSmoother" (OneEuro per node, emits EVERY Update)
                              │               │ processed.captureAspectNormalizedDetection(Result{original=raw, processed=smoothed})
                              │               ├─► BaseOnePlayerDetectionEngine (per player) ─► nodes (Transforms) + NewDetectionCapturedAndProcessed
                              │               ├─► OnePlayerSetupStateTracker (raise-hand check)
                              │               ├─► OnePlayerPhotoTracker
                              │               └─► DetectionManager.WaitForFirstDetection
PreviewController (static CvDetectionManager.previewController) ─LateUpdate─► IPreviewTextureHandler.OnTextureUpdated(tex, uv)
      handlers: PlayerSetupPreviewFrame, AreaPreviewFrame, PlayerFocusPreviewFrame, PlayerPreviewSprite, OnePlayerPhotoTracker
```

`bodyPoseDetectionManager.processed` returns the `finalPostProcessor` (the DefaultSmoother) when one is set, and returns the manager itself otherwise.

---

## 2. Coordinate spaces

| Space | Origin / axes | Range | Used by |
|---|---|---|---|
| Raw frame (`captureBodyPoseDetection`) | top-left, y **down**, pixels | 0..frameSize | `Nex.SetupDetector`, MDK action detectors |
| Aspect-normalized (`captureAspectNormalizedDetection`, both streams) | bottom-left, y **up** | x 0..aspect (16/9), y 0..1 | engines, play area, indicators. `pose.pixelsPerInch` here = frame-heights per inch (≈0.007–0.017 at normal distances) |
| Normalized / UV | bottom-left | 0..1 both | `IPreviewTextureHandler.GetPreviewRegion()`, `RawImage.uvRect`, `PreviewRectInNormalizedSpace()`, `GetPlayAreaInNormalizedSpace()` |
| `OnePlayerDetectionEngine` node space | centre of `ReferenceFrame` RectTransform (17.78×10 in the prefab, matches an ortho size 5 camera) | ±8.89, ±5 | smoothed/original node transforms. Pose is pre-normalized around the chest (§7.2) |
| `OnePlayerPreviewPoseEngine` node space | world coords of the preview `RawImage` | preview rect | AR attachments over the preview |
| Indicator local | centre of preview frame RectTransform | ±w/2, ±h/2 | `PreviewFramePlayerIndicator.indicator.anchoredPosition` |

- `Nex.DetectionUtils.AspectNormalizedFrameSize = new(16f/9f, 1f)` is **hard-coded** (`Assets/Scripts/Utils/DetectionUtils.cs`). A webcam that isn't 16:9 will misalign mappings. `GlobalOptions.frameResolution` defaults to (1280, 720).
- Aspect normalization (`Jazz.BodyPoseNormalizer`, `Mode.AspectNormalize`) computes `x = px/width*aspect` and `y = 1 - py/height`, then calls `InvalidatePpi()`. ppi is recomputed lazily in the new units.
- +x in every space points to **screen right**, which is the player's right because the frame is mirrored. No flip is needed when mapping to game screen x.

---

## 3. MDK (Jazz) API essentials

### 3.1 `Jazz.CvDetectionManager` (`team.nex.mdk/Scripts/cv/core/CvDetectionManager.cs`)
```csharp
public int numOfPlayers = 1;                      // CoreDetectionManager
public List<Vector2> playerPositions;             // normalized x per player, y=0.5 (DetectionManager fills it)
public FrameProviderType frameProviderType;       // Camera | VideoClip | VideoUrl | Replay | Adapter (prefab: Camera)
public bool autoStart = true;                     // StartRunning() in Start()
public CameraFrameProvider cameraFrameProvider; public VideoFrameProvider videoFrameProvider; ...
public static readonly DewarpAutoTiltController dewarpAutoTiltController;
public static readonly PreviewController previewController;   // add IPreviewTextureHandler here
public DisplayTextureSource displayTextureSource;             // CameraPreview | DebugFrame (debug overlay, costly)
public void SetProviderType(FrameProviderType type);          // LogError if already started
public void StartRunning(); public void StopRunning();         // pause-intention based
public FrameProvider GetFrameProvider();                       // TurnOnPreviewTexture()/TurnOffPreviewTexture()
public void ToggleDetectionHud();
```
- `Awake` sets `processExternalDetection = GlobalOptions.processExternalDetection || processExternalDetection || DeviceQuery.CameraDeviceCount() == 0`. With **no camera device**, there are no frames and no detections, and no frame provider is initialized (VideoClip included).

### 3.2 `Jazz.BodyPoseDetectionManager` (`team.nex.mdk.body/Scripts/body/BodyPoseDetectionManager.cs`)
```csharp
public struct BodyPoseDetectionResult { public BodyPoseDetection original; public BodyPoseDetection processed; }
public bool shouldDetect;                                        // Detector base
public float maxPoseDetectionFps = 30;
public bool autoFlipAsFrontFacingPose;                           // prefab: true
public PlayerTrackingConfig trackingConfig;                      // .enableConsistency toggled by DetectionManager
public event Action<BodyPoseDetection> captureBodyPoseDetection;               // raw px, top-left; gated during auto-tilt init
public event Action<BodyPoseDetectionResult> captureAspectNormalizedDetection; // original == processed (same object, NOT smoothed)
public IBodyPoseDetectionProvider processed { get; }             // DefaultSmoother if set → smoothed stream
public BodyPoseDetection latestBodyPoseDetection;                // debug only
```
Prefab values (`BodyPoseDetector.prefab` + `MDK.prefab` overrides): `poseScaleLevel: 234`, `maxPoseDetectionFps: 30`, `autoFlipAsFrontFacingPose: 1`, `trackingConfig.strategy: PositionAndIdealHalfBodyScale`, `enableConsistency: 1`, `enforceHorizontalPlayerOrder: 0`, `suddenMoveDistanceThresholdInInches: 16`. DVP config in MDK.prefab: `playerBodyRangeType: 3`, margins L/R 32, T 24, B 34 in, `minDetectionViewportToRawFrameRatio: 0.3`.

### 3.3 `Jazz.BodyPoseDefaultSmoother` (child `DefaultSmoother`)
- `upstream` is the BodyPoseDetectionManager. `updateMode = Update` (prefab 0) and `timeScaleIndependent = false`.
- MDK.prefab overrides: `filterPreset: Custom(2)`, `minCutoff: 2`, `beta: 5`, `dCutoff: 1`. Filters are keyed `(playerIdx, nodeIdx)` and applied only to nodes where `isDetected == true`.
- **It re-emits the last detection every `Update`**, re-filtered with a new `Time.time`. Consumers of `processed` therefore run at render rate and receive stale-but-converging data between camera frames. To detect new camera data, compare `result.original.frameTime`. The smoother keeps emitting even while detection is paused (`shouldDetect=false`).

### 3.4 Pose types
```csharp
public class BodyPoseDetection : CvDetection {     // CvDetection: double frameTime (s, unix epoch), Vector2 frameSize, TransformInfo
  public List<PlayerPose> playerPoseByPlayerIndex { get; }   // entries may be null
  public PlayerPose GetPlayerPose(int playerIndex);          // null if missing/out of range
  public int NumOfPlayers(); public int NumDetectedPlayers();
  public CoordinateSpace coordinateSpace;                    // DisplayFrame | Normalize | AspectNormalize
  public BodyPoseDetection Clone();
}
public class PlayerPose { public readonly BodyPose bodyPose; public readonly double frameTime; public readonly int playerIndex; }
public class BodyPose : ICloneable {
  public static int nodeNumber = 18;
  public PoseNode[] nodes; public int trackId;
  public PoseNode GetNode(NodeIndex i); Nose() Chest() LeftShoulder() LeftElbow() LeftWrist() RightShoulder() ...
  public float pixelsPerInch { get; }   // lazy; 0 if < 2 usable limbs
  public void InvalidatePpi(); public Rect BBox();
}
public struct PoseNode { public float x, y; public bool isDetected; public Vector2 ToVector2(); }
```
`BodyPose.NodeIndex` (new definition, `JAZZ_OLD_POSE_DEFINITION` not defined): Nose 0, Chest 1, LeftShoulder 2, LeftElbow 3, LeftWrist 4, RightShoulder 5, RightElbow 6, RightWrist 7, LeftHip 8, LeftKnee 9, LeftAnkle 10, RightHip 11, RightKnee 12, RightAnkle 13, LeftEye 14, RightEye 15, LeftEar 16, RightEar 17. There are no hand nodes.

**ppi** (`BodyPoseUtils.ComputePixelsPerInch`) takes each detected limb among 0..12 and computes `length / limbRefSize`. Reference sizes: chest→shoulder 6.5, upper arm 11, forearm 10, chest→hip 20, thigh 16, shin 15, chest→nose 8 (adult inches). It then averages the sorted [1/4..1/2] slice. Because it is anchored on body proportions, one "ppi-inch" is 1/13 of *that* player's shoulder width. A child and an adult produce similar ppi-inch measurements. This is the height and distance normalization.

### 3.5 Mirroring and handedness (Q1 detail)
- `CameraFrameProvider.cs` states that NexCamera handles every flip, and that the buffer starts at the top-left *of the preview*. AOSP/iOS mirror when the camera is front facing. Mac and Windows Editor always mirror.
- `BodyPoseUtils.PerformAutoFlipAsFrontFacingBodyPose`: if `LeftShoulder.x > RightShoulder.x`, it swaps the L/R shoulder, elbow, and wrist nodes. Hips, knees, and ankles are swapped by hip order, and eyes and ears by eye order. The result is **Left* = smaller x (screen-left)**, decided by the shoulders. Crossing the wrists keeps the labels, but turning sideways can swap them.
- VideoClip input is **not** mirrored by NexCamera. Set `VideoFrameProvider.shouldApplyExtraFlipH = true` for normal (unmirrored) recordings.

---

## 4. `Nex.DetectionManager` (`Assets/Scripts/Gameplay/Core/DetectionManager.cs`)
```csharp
[SerializeField] CvDetectionManager cvDetectionManager; BodyPoseDetectionManager bodyPoseDetectionManager;
[SerializeField] SetupStateManager setupStateManager; BasePlayAreaController playAreaController;
public CvDetectionManager CvDetectionManager { get; }  BodyPoseDetectionManager BodyPoseDetectionManager { get; }
public SetupStateManager SetupStateManager { get; }    BasePlayAreaController PlayAreaController { get; }

public void Initialize(int aNumOfPlayers);   // ConfigMdk(): cvdm.numOfPlayers, playerPositions[(GetXRatioForPlayer(i,n),0.5)],
                                             // GlobalOptions.shared.enableNativeZoom = true; playArea.Initialize; setupState.Initialize
public void ConfigForSetup();                // playArea unlocked, dewarp auto-tilt Recovery, consistency OFF, trackers ON, SetupDetectorMode.Base
public void ConfigForGameplay(bool shouldLockPlayerAreaAndDewarp = true); // locked, auto-tilt Off, consistency ON, SetupDetectorMode.Gameplay
public UniTask WaitForFirstDetection();      // first processed event; never completes without camera; overwrites source per call
public void StopDetection();                 // shouldDetect=false + cvdm.StopRunning()
public void PauseDetection();                // shouldDetect=false + TurnOffPreviewTexture()
public void UnPauseDetection();
```
- Setup trackers are **not** disabled by `ConfigForGameplay`. They keep running during gameplay, which is what produces `PlayingButNoPose`.
- `Initialize` is **not idempotent**: `PlayAreaController.Initialize` subscribes again, and SetupStateManager keeps its old state. Call it once per scene.

---

## 5. Prefab hierarchies (read-only YAML inspection)

```
Assets/Prefabs/Detection/Core/DetectionManager.prefab
DetectionManager                 [Nex.DetectionManager]
├─ MDK                           (instance of MDK.prefab)
├─ SetupStateManager             (instance of Setup/SetupStateManager.prefab) [Nex.SetupStateManager]
└─ PlayAreaController            [Nex.PlayAreaController] config: smooth 1s, L/R 32, T 24, B 34 in, min 0.3, max 1, deadZone 0, aspect 1.7778

Assets/Prefabs/Detection/Core/MDK.prefab
MDK
├─ CvDetectionManager            (instance of team.nex.mdk/Prefabs/Detection/CvDetectionManager.prefab) [Jazz.CvDetectionManager]
│  ├─ FrameProviders
│  │  ├─ VideoFrameProvider      (INACTIVE; VideoPlayer clip none, loop off)
│  │  ├─ VideoURLFrameProvider   (INACTIVE)
│  │  ├─ ReplayFrameProvider     (INACTIVE)
│  │  └─ CameraFrameProvider
│  └─ Utils: DetectionHudManager, RuntimeStatusBar, PipelineStatusReporter, StatisticReporter, DiagnosticGUI
└─ BodyPoseDetector              (instance of team.nex.mdk.body/Prefabs/BodyPoseDetector.prefab) [Jazz.BodyPoseDetectionManager]
   └─ DefaultSmoother            [Jazz.BodyPoseDefaultSmoother]  (finalPostProcessor)

Detection/DetectionEngine/OnePlayerDetectionEngine.prefab
OnePlayerDetectionEngine [Nex.OnePlayerDetectionEngine: originalNodePrefab=HiddenPoseNode, smoothedNodePrefab=CirclePoseNode, referenceTransform=ReferenceFrame]
├─ ReferenceFrame  (RectTransform 17.777779 × 10, localPos 0)
├─ Original        (nodes spawned here)
└─ Smoothed        (nodes spawned here)
Detection/DetectionEngine/OnePlayerPreviewPoseEngine.prefab: OnePlayerPreviewPoseEngine > Original, Smoothed (same node prefabs)
CirclePoseNode > Circle [SpriteRenderer]   ← VISIBLE in the world
HiddenPoseNode (empty)

Detection/Setup/SetupStateManager.prefab   [SetupStateManager: onePlayerSetupStateTrackerPrefab, setupDetectorWarningConfig=Assets/Configs/Setup/SetupDetectorWarningConfig.asset]
Detection/Setup/OnePlayerSetupStateTracker.prefab [OnePlayerSetupStateTracker: setupDetectorPrefab]
Detection/Setup/SetupDetector.prefab       [Nex.SetupDetector]

Detection/Preview/PreviewsManager.prefab
PreviewsManager [PreviewsManager: setupInfoPreviewFramePrefab, previewsContainer, setupPromptText, moveIn/OutAnimator]
├─ Animators > MoveInAnimator [MMF_Player: MMF_Position UIRoot y 2000→0, 0.5s], MoveOutAnimator [0→2000]
└─ UI [Canvas ScreenSpaceOverlay, sortingOrder 0; CanvasScaler 1920×1080 match 0; GraphicRaycaster]
   └─ UIRoot (stretch)
      ├─ SetupText [TextMeshProUGUI, anchor top-centre, y -200, size 70]
      └─ PreviewsContainer [HorizontalLayoutGroup, spacing 100, centre] (stretch, sizeDelta -400,-200)

Detection/Preview/SetupInfoPreviewFrame.prefab (300×540)
SetupInfoPreviewFrame [SetupInfoPreviewFrame: previewFrame, warningMessage, playerIndicatorsManager]
├─ Background [RoundedRectModifier, Image]
├─ PreviewFrame (instance of PreviewFrame.prefab)
├─ SetupWarningMessage (instance; anchor bottom-centre, 200×50, y -30)
└─ PlayerIndicatorsManager [PlayerIndicatorsManager: playerIndicatorPrefab=PreviewFramePlayerIndicator, ratio 0.1]

Detection/Preview/PreviewFrame.prefab
PreviewFrame [PlayerSetupPreviewFrame(rawImage, canvasGroup), CanvasGroup] (stretch, pivot 0,0)
└─ RawImage [RawImage, RoundedRectModifier (team.nex.npuikit)]

Detection/Preview/PreviewFramePlayerIndicator.prefab
PreviewFramePlayerIndicator [PreviewFramePlayerIndicator: indicator=Root, nodeToFollow=Nose(0), offsetInInches=(0,8),
                             spriteByPlayerIndex=[indicator1, indicator2, indicator3, indicator1], image=Icon]
└─ Root (RectTransform; toggled active; anchoredPosition set per detection)
   └─ Icon [Image 25×25, pivot (0.5,0)]  ← sizeDelta overwritten per update
Sprites: Assets/Sprites/PlayerIndicators/player-indicator{1,2,3}.png, 75×75 down-pointing triangles: 1 red/coral, 2 blue, 3 green.

Detection/Preview/SetupWarningMessage.prefab: [TextMeshProUGUI, SetupWarningMessage]
Detection/Preview/PlayerPreviewSprite.prefab: [SpriteRenderer, PlayerPreviewSprite]
```

---

## 6. `PoseNodeIndex` and `PlayerAttachmentDataSource`
```csharp
public enum PoseNodeIndex { LeftHand=0, RightHand=1, Chest=2, Nose=3, LeftShoulder=4, RightShoulder=5, LeftElbow=6, RightElbow=7,
  LeftWrist=8, RightWrist=9, LeftHip=10, RightHip=11, LeftKnee=12, RightKnee=13, LeftEye=14, RightEye=15, LeftEar=16, RightEar=17,
  HipCenter=18, LeftAnkle=19, RightAnkle=20 }            // Nex enum; NOT the same indices as Jazz BodyPose.NodeIndex
public interface PlayerAttachmentDataSource { float DistancePerInch { get; } Vector3? GetNodePosition(PoseNodeIndex nodeIndex, bool smoothed); }
```

---

## 7. Detection engines (`Assets/Scripts/Gameplay/Detection/`)

### 7.1 `BaseOnePlayerDetectionEngine : MonoBehaviour, PlayerAttachmentDataSource` (abstract)
```csharp
[SerializeField] GameObject originalNodesContainer, smoothedNodesContainer, originalNodePrefab, smoothedNodePrefab;
public float DistancePerInch { get; }                 // world units per ppi-inch (subclass-defined)
public float RawPpi { get; }                          // FloatHistory(0.5s) average of original pose ppi (aspect-normalized units)
public BodyPoseDetectionResult LastBodyPoseDetectionResult { get; }
public event UnityAction<BodyPoseDetectionResult>? NewDetectionCapturedAndProcessed;   // fires on every processed event once RawPpi>0 (~every Update)
public Transform LeftHand, RightHand, Chest, Nose, LeftShoulder, ... HipCenter;         // smoothed node transforms
public Transform OriginalLeftHand, ...;                                                  // raw node transforms
protected void Initialize(int aPlayerIndex, BodyPoseDetectionManager bpdm, bool aEnableOriginalNodes = true, bool aEnableSmoothedNodes = true);
public Vector3? GetNodePosition(PoseNodeIndex i, bool smoothed);   // world position, null if node missing/inactive
public Transform GetNodeByPoseNodeIndex(PoseNodeIndex i, bool smoothed);
protected abstract Vector3 AspectNormalSpaceToWorldSpace(Vector2 v); protected abstract float ConvertRawPpiToDpi(float rawPpi);
protected abstract BodyPose? CloneAndProcessPoseIfNeeded(BodyPose? pose);
```
Mechanics:
- It subscribes to `bpdm.processed.captureAspectNormalizedDetection`. ppi comes from `result.original` (raw). Smoothed nodes come from `result.processed` and original nodes from `result.original`.
- Supported nodes: LeftHand, RightHand, Chest, Nose, L/R Shoulder, L/R Elbow, L/R Hip, L/R Knee, L/R Eye, HipCenter. **Wrists, ears, and ankles are commented out.**
- Derived nodes:
  - `LeftHand = LerpUnclamped(LeftElbow, LeftWrist, 1.35f)`. This extrapolates 35% of the forearm past the wrist. RightHand works the same way. Both source nodes must be detected.
  - `HipCenter = lerp(LeftHip, RightHip, 0.5)`.
- Positions are written to `localPosition` of the node under its container.
- Auto-hide: a node is `SetActive(false)` when it has not been detected for `0.2 s` (`Time.fixedTime`). While in that grace window it keeps its last position.

### 7.2 `OnePlayerDetectionEngine` (the gameplay engine)
```csharp
[SerializeField] RectTransform referenceTransform;
public new void Initialize(int aPlayerIndex, BodyPoseDetectionManager bpdm, bool aEnableOriginalNodes = true, bool aEnableSmoothedNodes = true);
```
- `CloneAndProcessPoseIfNeeded` clones the pose and scales every node around the **chest** by `1/(RawPpi*58)` (`totalHeightInInches = 58`). The body therefore has a constant size, and the chest stays at its real position in the frame.
- `AspectNormalSpaceToWorldSpace` maps `[0..16/9]×[0..1]` to `referenceTransform.rect` centred on its localPosition. (Bug: localPosition is added twice. It's harmless at 0.)
- `DistancePerInch = referenceTransform.rect.height / 58` = **10/58 ≈ 0.1724** for the stock prefab. It is constant.
- Result: `(node - chest) / DistancePerInch` gives ppi-inches in x and y, isotropic.

### 7.3 `OnePlayerPreviewPoseEngine` (AR overlay engine)
```csharp
public void Initialize(int aPlayerIndex, BodyPoseDetectionManager bpdm, PreviewFrameBase aPreviewFrame, bool aEnableOriginalNodes = true, bool aEnableSmoothedNodes = true);
```
- The pose is not normalized. It maps the preview's `PreviewRectInNormalizedSpace()` to `PreviewRectInWorldSpace()`, so nodes line up with the camera image.
- `DistancePerInch = RawPpi / previewRectHeightAspectNorm * previewWorldHeight`. It varies with the player's distance.
- It returns *world* coordinates but writes them to `localPosition`. This is only correct when the containers sit at the world origin with an identity transform.

---

## 8. Play area and player positions (`Assets/Scripts/Gameplay/Preview/`, `Player/`)
```csharp
public abstract class BasePlayAreaController : MonoBehaviour {
  public virtual void Initialize(int numOfPlayers, CvDetectionManager cvdm, BodyPoseDetectionManager bpdm);
  public virtual void SetPlayAreaLocked(bool value);
  public abstract Rect GetPlayAreaInNormalizedSpace(); public abstract Rect GetPlayAreaInAspectNormalizedSpace();
  public abstract void RefreshPlayArea(); public abstract void ForceSetPlayArea(Rect rect);
  protected virtual void UpdateTrackingPosition();   // writes cvdm.playerPositions[i] = (playArea.x + w*GetXRatioForPlayer(i,n), 0.5)
}
public static class PlayerPositionDefinition { public static float GetXRatioForPlayer(int playerIndex, int numOfPlayers); }
// 1P: 0.5 | 2P: 0.3, 0.7 | 3P: 0.25, 0.5, 0.75 | 4P: 0.2..0.8 ; throws for other values
```
- `PlayAreaController` (the one in the prefab) works from the raw stream. For each player it takes a box of chest ± margins (inches × ppi) and keeps the min margin to each frame edge, smoothed over 1 s. It anchors x at the centre and builds a 16:9 ROI clamped to [0.3, 1] of the frame. It stops updating while locked (gameplay).
- `WeightedAveragePlayAreaController` is an alternative that refreshes every 5 s. `MixedPlayAreaController` is for A/B testing only; don't ship it.

---

## 9. Setup (`Assets/Scripts/Gameplay/Setup/`)

### 9.1 `SetupStateManager`
```csharp
[SerializeField] OnePlayerSetupStateTracker onePlayerSetupStateTrackerPrefab; [SerializeField] SetupDetectorWarningConfig setupDetectorWarningConfig;
public bool AllPlayersAreInGoodPosition { get; }                     // all states > WaitingForGoodPlayerPosition
public event UnityAction<(int playerIndex, SetupSummary setupSummary)>? PlayerTrackerUpdated;   // per raw detection, per player
public void Initialize(int n, BodyPoseDetectionManager bpdm, BasePlayAreaController playArea);
public void SetTrackingEnabled(bool shouldTrack);   // creates/destroys one tracker per player
public void ResetSetupStates();                     // BUG: see §16 #2
public void SetAllowPassingRaisingHandState(bool value);
public UniTask WaitForGoodPlayerPosition();  public UniTask WaitForRaiseHand();
public void SetSetupDetectorMode(SetupDetectorMode mode);   // Base | Gameplay
// Update(): if (Input.GetKeyDown(KeyCode.S)) { SetAllowPassingRaisingHandState(true); ResolveGoodPlayerPosition(); ResolveRaiseHand(); }
```

### 9.2 `OnePlayerSetupStateTracker`, `SetupStateType`, `SetupSummary`
```csharp
public enum SetupStateType { Preparing=0, WaitingForGoodPlayerPosition=1, WaitingForRaisingHand=2, Playing=3, PlayingButNoPose=4 }
public struct SetupSummary { SetupStateType setupStateType; SetupIssueType currentSetupIssue; bool isStateChanged;
                             float goodPositionProgress; float raiseHandProgress; float noPlayerDuration; static SetupSummary CreateDummy(); }
public event UnityAction<SetupSummary>? Updated;
```
State machine. It is driven by each `SetupDetector.captureDetection`, which fires once per raw detection. Time is `Time.fixedTime`.
- `Preparing` → `WaitingForGoodPlayerPosition` when `hasEnoughData`. That means detections have been arriving for longer than `issueEvaluationTimeWindow` (0.5 s), whether or not a pose was present.
- `WaitingForGoodPlayerPosition` → `WaitingForRaisingHand` when the good-position ratio over 1.5 s exceeds 0.7 (`goodPositionProgress = ratio/0.7`).
- `WaitingForRaisingHand` → `Playing` when the raise-hand ratio over 1 s (since the state started) exceeds 0.7 **and** `allowPassingRaisingHandState` is set. It drops back to good-position when that ratio falls below 0.5. Raise hand means `wrist.y > shoulder.y` for either arm on the **processed** pose.
- `Playing` → `PlayingButNoPose` when the player hasn't been seen for more than 2 s. It goes back to `Playing` when the good-position ratio exceeds 0.7.

### 9.3 `Nex.SetupDetector` and `SetupDetectorWarningConfig`
```csharp
public enum SetupIssueType { None, NoPose, ChestTooHigh, ChestTooLow, ChestTooLeft, ChestTooRight, TooFar, TooClose, TooFarInPlayArea, TooCloseInPlayArea, NotAtCenter }
public struct SetupDetection { public bool hasEnoughData; public SetupIssueType currentIssue; }
public event UnityAction<SetupDetection> captureDetection;
public void Initialize(int playerIndex, BodyPoseDetectionManager bpdm, BasePlayAreaController playArea, SetupDetectorWarningConfig cfg, List<SetupIssueType> priority = null);
public void SetDetectorMode(SetupDetectorMode mode);
```
- It uses the **raw** `captureBodyPoseDetection` stream (pixels, y down).
- The tracker's priority list is NotAtCenter, TooClose, TooFar, TooCloseInPlayArea, TooFarInPlayArea, ChestTooHigh, ChestTooLow, NoPose. ChestTooLeft/Right are never raised.
- Base thresholds (`Assets/Configs/Setup/SetupDetectorWarningConfig.asset`): chest at least 12" from the top, 18" from the bottom, 12" from the left and right. Chest x within 10" of the player's expected x. Frame height 40–280 in. Play-area height 40–150 in. Hysteresis ±1" and ±0.03. Activation needs more than 80% of the 0.5 s window, cancellation needs more than 40%.
- The **Gameplay entry has `overrideWarningConfig: 0`**, so it inherits Base entirely. Its 14" and 33" values are ignored.
- `SetupWarningMessage.Initialize(int playerIndex, SetupStateManager)` shows **hard-coded English** ("No player", "Step back", "Move closer", "Move to center"). Billiard Rogue must localize these.

---

## 10. Previews (`Assets/Scripts/Gameplay/Preview/`)

### 10.1 `Jazz.IPreviewTextureHandler` and `PreviewController`
```csharp
public interface IPreviewTextureHandler { Rect GetPreviewRegion(); void OnTextureUpdated(Texture2D newTexture, Rect newUV); }
CvDetectionManager.previewController.AddPreviewTextureHandler(h); RemovePreviewTextureHandler(h); PreviewWidth; PreviewHeight;
```
- In `LateUpdate`, every live handler gets `OnTextureUpdated(detectionTexture, requestedUV)`. When multi-window dewarp is active, it gets the native texture with remapped UV instead.
- The region every handler returns also feeds the native crop/zoom request. That request uses the min x/y and the max width/height across handlers. `DetectionManager` sets `enableNativeZoom = true`. The device effect is **[UNVERIFIED]**.
- Handlers are removed automatically once they are destroyed (null).

### 10.2 `PreviewFrameBase : MonoBehaviour` (abstract)
```csharp
[SerializeField] protected RawImage rawImage; [SerializeField] protected CanvasGroup canvasGroup;
public bool isPreviewRectInWorldSpaceInfoValid { get; }
public abstract Rect PreviewRectInNormalizedSpace();
public Rect PreviewRectInAspectNormalizedSpace();     // x,width × 16/9
public Rect PreviewRectInWorldSpace();                // cached from rawImage world corners ONCE (first texture)
```

| Derivative | Initialize | Region shown | Notes |
|---|---|---|---|
| `PlayerSetupPreviewFrame` (+IPreviewTextureHandler) | `(int playerIndex, int numOfPlayers, BodyPoseDetectionManager, BasePlayAreaController)` | a vertical slice of the play area centred at `GetXRatioForPlayer`, width = frameAspect/16:9 | used inside SetupInfoPreviewFrame; `rawImage.uvRect = newUV` |
| `AreaPreviewFrame` | `(BasePlayAreaController)` | centre crop of the play area fitted to the frame's aspect | `enableSmoothing`/`smoothFactor`/`enableSmoothingAfterPeriod`; ARGameExample uses it; **the PiP choice** |
| `PlayerFocusPreviewFrame` | `(int playerIndex, int numOfPlayers, BodyPoseDetectionManager)` | follows one player's chest with margins in inches (24/34/32/32), OneEuro(1,2) plus a stable-Y history | listens to the raw manager event |
| `SetupInfoPreviewFrame` (not a PreviewFrameBase) | `(int playerIndex, int numOfPlayers, bpdm, playArea, setupStateManager)` | wraps PlayerSetupPreviewFrame + SetupWarningMessage + PlayerIndicatorsManager(`new List<int>{playerIndex}`) | |

All frames fade `canvasGroup` 0→1 over 0.5 s (DOTween) on the first texture.

### 10.3 `PreviewsManager`
```csharp
public void Initialize(int aNumOfPlayers, BodyPoseDetectionManager bpdm, BasePlayAreaController playArea, SetupStateManager ssm); // spawns N SetupInfoPreviewFrames, MoveOut(false)
public void SetPromptText(string text);
public UniTask MoveIn(bool animated); public UniTask MoveOut(bool animated);   // MMFeedbacks PlayAsUniTask
```

---

## 11. Player indicators (`Assets/Scripts/Gameplay/Preview/Indicator/`)
```csharp
public class PlayerIndicatorsManager : MonoBehaviour {
  [SerializeField] PreviewFramePlayerIndicator playerIndicatorPrefab; [SerializeField] float playerIndicatorSizeRatioToPreviewHeight = 0.15f;
  public void Initialize(int n, List<int> playerIndexList, PreviewFrameBase previewFrame, BodyPoseDetectionManager bpdm);
  public void Initialize(int n, PreviewFrameBase previewFrame, BodyPoseDetectionManager bpdm);   // all players (LINQ Range, once)
}   // instantiates indicators under previewFrame.transform; does NOT keep references
public class PreviewFramePlayerIndicator : MonoBehaviour {
  [SerializeField] RectTransform indicator; BodyPose.NodeIndex nodeToFollow; Vector2 offsetInInches; List<Sprite> spriteByPlayerIndex; Image image;
  public void Initialize(int playerIndex, BodyPoseDetectionManager bpdm, PreviewFrameBase previewFrame, float sizeRatioToPreviewHeight);
}
```
- The indicator subscribes to `bpdm.captureAspectNormalizedDetection`. That is the raw, detection-rate stream.
- On each event it takes the node plus `offsetInInches*ppi` (ppi averaged over 0.3 s) and applies OneEuro(4,10). It then maps `PreviewRectInAspectNormalizedSpace()` into the frame's rect, clamped so the icon stays inside, and sets `anchoredPosition`.
- It hides when the pose, the node, or ppi is missing. The icon size is `previewHeight*ratio`.
- There is no highlight or active-player API.

---

## 12. Player images
- `PlayerPreviewSprite : MonoBehaviour, IPreviewTextureHandler` has `Initialize(int playerIndex, CvDetectionManager, BodyPoseDetectionManager)`. It shows a live SpriteRenderer crop around the chest or nose. `Sprite.Create` runs on every texture update (per frame). `PlayerPreviewSpritesController.Initialize(...)` initializes every child PlayerPreviewSprite.
- `PlayerPhotoManager` API:
  - `Initialize(int n, BodyPoseDetectionManager)` creates `OnePlayerPhotoTracker` per player via `new GameObject` and registers them as preview handlers.
  - `TakePhoto(int)` makes a Nose-centred crop of about 7.4×7.4 in with rotation and copies it into a Texture2D. The old texture is never destroyed.
  - `ClearPhoto(int)` and `GetTrackerByPlayerIndex(int)`.
  - `OnePlayerPhotoTracker.GetPlayerPhotoData(int)` returns `PlayerPhotoData{texture, uvRect}`. Before a photo is taken, that is the live preview crop.
  - The `PhotoUpdated` event fires several times per processed event (about 60 Hz).
- `PlayerPhotoSprite` (via `PlayerPhotoSpritesManager.Initialize(playerIndex, manager)`) calls `Sprite.Create` on **every** `PhotoUpdated`. That is GC and native churn, so avoid it on low-end hardware. For turn banners, prefer a UI `RawImage` that sets `texture` + `uvRect` from `GetPlayerPhotoData`.

---

## 13. Example scenes

**`Assets/Scenes/Examples/NonARGameExample.unity`** (roots):
```
NonARGameSceneExample (INACTIVE; SingletonSpawner.activatePostSpawn) [NonARGameExample: numOfPlayers=1,
    detectionEnginePrefab=OnePlayerDetectionEngine.prefab, playerPrefab=ExampleNonARPlayer, previewsManager, detectionManager, playerPhotoManager]
├─ PlayersContainer
└─ PlayerPhotoManager [PlayerPhotoManager]
SingletonSpawner [SingletonSpawner]      Main Camera (orthographic, size 5, z -10, URP)      EventSystem [StandaloneInputModule]
PreviewsManager (prefab instance, overlay canvas)      DetectionManager (prefab instance incl. MDK → camera starts at scene load)
```
Flow (`NonARGameExample.StartAsync`):
1. `detectionManager.Initialize(n)`, then `previewsManager.Initialize(...)`, then `playerPhotoManager.Initialize(n, bpdm)`.
2. Per player, with the container inactive: `engine.Initialize(i, bpdm)` and `player.Initialize(i, engine, photoMgr)`.
3. `await ScreenBlockerManager.Instance.Hide()`.
4. `RunSetup`:
   1. `ConfigForSetup()`, then `MoveIn(true)`.
   2. Prompt "Move into the frame", then `await WaitForGoodPlayerPosition()`.
   3. Prompt "Raise your hand to start", then `SetAllowPassingRaisingHandState(true)` and `await WaitForRaiseHand()`.
   4. `MoveOut(true)`.
5. `TakePhoto(i)`.
6. `ConfigForGameplay()` and activate the container.

It shows **no gameplay preview**, and Escape uses raw `Input`.

**`Assets/Scenes/Examples/ARGameExample.unity`** (roots):
```
Main Camera (ortho 5)
Preview [Canvas WORLD SPACE 1920×1080, scale 0.009259 (=10/1080, fills the ortho view), CanvasScaler, GraphicRaycaster]
├─ Background [Image]
├─ PreviewFrame [AreaPreviewFrame (smoothing off), CanvasGroup] (stretch) > RawImage [RawImage] (stretch)
└─ PlayerIndicatorsManager [PlayerIndicatorsManager, ratio 0.07]
ARGameExample [ARGameExample: numOfPlayers=2, onePlayerPreviewPoseEnginePrefab, playerPrefab=ExampleARPlayer] > PlayersContainer
EventSystem
Detection > PlayAreaController [PlayAreaController], MDK (prefab instance)   ← raw MDK, no DetectionManager/setup, no SingletonSpawner
```
Flow: set `cvdm.numOfPlayers`, then `playArea.Initialize`, `previewFrame.Initialize(playArea)`, and `indicators.Initialize(n, previewFrame, bpdm)`. Per player, `OnePlayerPreviewPoseEngine.Initialize(i, bpdm, previewFrame)`. Copy only the `AreaPreviewFrame + PlayerIndicatorsManager` part.

Also: `PlayerFocusPreviewExample` (PlayerFocusPreviewFrame + OnePlayerPreviewPoseEngine) and `PreviewSpriteExample` (PlayerPreviewSprite per player).

---

## 14. Answers in detail

### Q3: 1 vs 2 players
- Choose `numOfPlayers` before the gameplay scene loads, persisted by the menu flow. Reload the scene to change it.
- `DetectionManager.Initialize(n)` sets `cvdm.numOfPlayers` and `playerPositions`. The MDK engine re-initializes internally when `numOfPlayers` changes (`BodyPoseDetectionManager.PoseEngineInitializationIsNeeded`), but the Starter wrappers do not.
- Create one `OnePlayerDetectionEngine` and one `PawInputController` per `playerIndex`. Both players stay tracked all the time, and only the active one's shots are accepted.
- Identity:
  - Index 0 is the left standing spot and index 1 the right, in the mirrored frame, which is also the room as the players see it.
  - Setup frames lay out P1 left and P2 right (HorizontalLayoutGroup order).
  - `ConfigForGameplay` enables `trackingConfig.enableConsistency`, so identities stick.
  - Players must not swap places. Tell them to stay in their spot.
- Setup waits for **all** players. A 2P game with one person present never passes, except via the S key.

### Q4: Editor testing without a person or camera
1. **S key** (`SetupStateManager.Update`, raw `Input`, **not dev-gated**): it resolves whichever of `WaitForGoodPlayerPosition` / `WaitForRaiseHand` is currently awaited, so press it once per stage. Tracker states don't change, so `PlayerTrackerUpdated` keeps reporting Preparing/NoPose. Don't `await DetectionManager.WaitForFirstDetection()` without a timeout.
2. **Mac webcam**: `DeviceQuery` uses `InitializeWebcamDevices()`, which becomes `NexWebCamera` (WebCamTexture), always mirrored. Grant Unity camera permission in macOS Privacy settings. The examples set `Application.runInBackground = true` in the Editor. Check that the frame is 16:9 via `CvDetectionManager.previewController.PreviewWidth/Height`.
3. **Recorded video** **[UNVERIFIED end-to-end]**:
   1. Before `CvDetectionManager.Start` runs, set `cvdm.frameProviderType = CvDetectionManager.FrameProviderType.VideoClip`. Use a dev-only prefab variant or scene built by an editor script, or `SetProviderType` from an earlier `Awake`.
   2. Activate the `VideoFrameProvider` child and assign `videoPlayer.clip`. Set `isLooping`.
   3. Set `shouldApplyExtraFlipH = true` for unmirrored footage.
   4. This needs at least one camera device present (see §3.1).
4. **MDK replay**: `DetectionMonitorManager.instance.ToggleReplayLastDetectionSession()` and `ReplayFrameProvider` replay recorded "troubleshoot" sessions from the MDK DebugOptionPane. This is internal tooling; not recommended.
5. There is **no pose mock API**. Provide `KeyboardPawInputSource` gated by `Nex.Dev.DebugInput` (§15.4), and unit-test `PawStrikeDetector` with synthetic samples in an EditMode test asmdef.
6. Extras:
   - `cvdm.displayTextureSource = DisplayTextureSource.DebugFrame` shows the skeleton overlay (costly). `cvdm.ToggleDetectionHud()`.
   - `DebugPrinter.Instance.Print(key, value)`.
   - Cmd+Shift+M in the Editor toggles camera mute (`CherryIntegrationManager`), for testing pause/camera-muted flows.

---

## 15. How Billiard Rogue should use this

### 15.1 Scene wiring and flow
Gameplay scene (it needs a `SingletonSpawner` only if it must also run standalone):
```
GameplayCoordinator (inactive until SingletonSpawner activates it)
DetectionManager (prefab instance)                  PreviewsManager (prefab instance, used by the Setup view)
PawInputRoot (identity transform at world origin, far from the arena or on a non-rendered layer)
   └─ spawned per player: OnePlayerDetectionEngine (VARIANT) + PawInputController
HUD overlay canvas → GameplayCameraPip (new prefab, §15.5)
```
- Build `BilliardOnePlayerDetectionEngine.prefab` with an **Editor script**, as a prefab variant of `OnePlayerDetectionEngine.prefab` whose `smoothedNodePrefab` is `HiddenPoseNode`. The stock `CirclePoseNode` circles would otherwise render in the 3D arena.
- Keep the `ReferenceFrame` at 17.78×10 so that `DistancePerInch = 10/58`.

```csharp
// GameplayCoordinator (orchestration only). numOfPlayers comes from the menu / PlayerDataManager.
detectionManager.Initialize(numOfPlayers);
previewsManager.Initialize(numOfPlayers, detectionManager.BodyPoseDetectionManager,
    detectionManager.PlayAreaController, detectionManager.SetupStateManager);
for (var playerIndex = 0; playerIndex < numOfPlayers; playerIndex++)
{
    var engine = Instantiate(detectionEnginePrefab, pawInputRoot);   // OnePlayerDetectionEngine variant
    engine.Initialize(playerIndex, detectionManager.BodyPoseDetectionManager);
    var paw = Instantiate(pawInputControllerPrefab, pawInputRoot);
    paw.Initialize(playerIndex, engine);
    pawInputs.Add(paw);
}

// SetupView (a SimpleCanvasView pushed on ViewManager). Prompts must be localized strings.
detectionManager.ConfigForSetup();
await previewsManager.MoveIn(true);
previewsManager.SetPromptText(moveIntoFramePrompt.GetLocalizedString());   // UnityEngine.Localization.LocalizedString field
await detectionManager.SetupStateManager.WaitForGoodPlayerPosition();
previewsManager.SetPromptText(raisePawPrompt.GetLocalizedString());
detectionManager.SetupStateManager.SetAllowPassingRaisingHandState(true);
await detectionManager.SetupStateManager.WaitForRaiseHand();
await previewsManager.MoveOut(true);
detectionManager.ConfigForGameplay();
cameraPip.Initialize(numOfPlayers, detectionManager.PlayAreaController, detectionManager.BodyPoseDetectionManager);
```

Pause menu: call `detectionManager.PauseDetection()` / `UnPauseDetection()` and ignore paw input while paused, because the smoother keeps re-emitting stale poses.

### 15.2 Body-relative hand sampling (Q2)
```csharp
// Inside a Nex.* namespace with `using Jazz;` at FILE TOP. Then Nex.OneEuroFilter wins over Jazz.OneEuroFilter.
static bool TryGetBodyInches(OnePlayerDetectionEngine engine, PoseNodeIndex node, bool smoothed, out Vector2 inches)
{
    inches = default;
    var chest = engine.GetNodePosition(PoseNodeIndex.Chest, smoothed);
    var target = engine.GetNodePosition(node, smoothed);
    if (chest == null || target == null) return false;
    inches = (Vector2)(target.Value - chest.Value) / engine.DistancePerInch;   // x → screen right, y → up
    return true;
}
```
- Call it only inside `NewDetectionCapturedAndProcessed`, or after `engine.RawPpi > 0`. Before the first detection, `GetNodePosition` returns a bogus non-null value.
- The engine root must have an identity transform. If it doesn't, use `engine.transform.InverseTransformPoint`.
- Useful ppi-inch magnitudes from the MDK reference limbs: chest→shoulder 6.5, upper arm 11, forearm 10. That puts the hand about 13.5 past the elbow and gives a full reach from the chest of about 31. Shoulder width is about 13.
- **Smoothed stream** (about 60 Hz, OneEuro 2/5 lag): use it for ball X and the aim line.
- **Raw stream** (camera rate, exact timestamps): use it for the strike. Sample `GetNodePosition(..., smoothed: false)` only when `result.original.frameTime` increases. The time step is `dt = (float)(frameTime - lastFrameTime)` in seconds, and anything outside `(0, 0.25]` is treated as a gap.

### 15.3 `PawInputController` design (recommended)
Files: `Assets/Scripts/Gameplay/PawInput/`.
- `PawInputConfig` is a ScriptableObject asset created via an Editor script, with tunables mirrored into `DebugSettings`.
- `PawFrame` is a struct.
- `IPawInputSource` has two real implementations.
- `PawInputController` is a MonoBehaviour, one per player.
- `PawStrikeDetector` is pure C# so it can be unit-tested.

Starting values below are guesses; tune them on device.

| Config (ppi-inches, seconds) | Start | Meaning |
|---|---|---|
| `ballXMinInches` / `ballXMaxInches` | -26 / +8 | left-paw x relative to the chest, mapped to ball x 0..1 |
| `ballXMinCutoff` / `ballXBeta` | 1.0 / 0.05 | extra Nex.OneEuroFilter on ball x (inch units) |
| `minAimDistanceInches` | 4 | below this distance between the paws, keep the previous aim |
| `minAimAngleDeg` | 12 | clamp the shot to [12°, 168°], pointing up the arena |
| `armDistanceInches` | 10 | the paws must be at least this far apart to arm a strike |
| `strikeSpeedInchesPerSec` | 40 | right-paw velocity toward the left paw that starts a strike |
| `minStrikeTravelInches` | 6 | the distance between the paws must shrink by at least this much |
| `contactDistanceInches` | 8 | the strike fires on reaching this distance (or on speed collapse after travel) |
| `maxStrikeDuration` | 0.35 | the strike must complete within this window |
| `cooldown` | 0.6 | re-arming also needs the paws apart again by ≥ `armDistanceInches` |
| `fullPowerSpeedInchesPerSec` | 120 | peak speed mapped to power 1 |

```csharp
public struct PawFrame
{
    public bool isTracked;          // chest + both paws visible (engine auto-hide 0.2 s already applied)
    public Vector2 leftInches;      // left paw rel. chest (smoothed)
    public Vector2 rightInches;
    public float ballX01;
    public Vector2 aimDirection;    // unit vector right→left paw, clamped upward
    public bool isAimValid;
}
public struct PawShot { public int playerIndex; public Vector2 direction; public float power01; }

public interface IPawInputSource   // 2 real uses: body tracking + keyboard mock
{
    PawFrame Current { get; }
    event UnityAction<PawShot>? Shot;
    void SetShootingEnabled(bool enabled);   // only the active player's source shoots
}
```

```csharp
public class PawInputController : MonoBehaviour, IPawInputSource
{
    [SerializeField] PawInputConfig config = null!;

    OnePlayerDetectionEngine engine = null!;
    PawStrikeDetector strikeDetector = null!;
    OneEuroFilter ballXFilter = null!;           // Nex.OneEuroFilter (Assets/Scripts/Gameplay/Smoothing/Filter.cs)
    int playerIndex;
    double lastRawFrameTime = -1;
    Vector2 aim = Vector2.up;

    public PawFrame Current { get; private set; }
    public event UnityAction<PawShot>? Shot;

    public void Initialize(int aPlayerIndex, OnePlayerDetectionEngine aEngine)
    {
        playerIndex = aPlayerIndex;
        engine = aEngine;
        strikeDetector = new PawStrikeDetector(config);
        ballXFilter = new OneEuroFilter(config.BallXMinCutoff, config.BallXBeta);
        engine.NewDetectionCapturedAndProcessed += HandleDetection;
    }

    void OnDestroy() => engine.NewDetectionCapturedAndProcessed -= HandleDetection;

    public void SetShootingEnabled(bool enabled) => strikeDetector.SetEnabled(enabled);

    void HandleDetection(BodyPoseDetectionResult result)
    {
        var tracked = TryGetBodyInches(engine, PoseNodeIndex.LeftHand, true, out var left)
                      & TryGetBodyInches(engine, PoseNodeIndex.RightHand, true, out var right);
        var frame = new PawFrame { isTracked = tracked, leftInches = left, rightInches = right };
        if (tracked)
        {
            var ballX = ballXFilter.Filter(left.x, Time.time);
            frame.ballX01 = RemapUtils.RemapAndClamp(ballX, config.BallXMinInches, config.BallXMaxInches, 0f, 1f);
            var toLeft = left - right;
            frame.isAimValid = toLeft.magnitude >= config.MinAimDistanceInches;
            if (frame.isAimValid && !strikeDetector.IsStriking) aim = ClampAimUpward(toLeft.normalized, config.MinAimAngleDeg);
            frame.aimDirection = aim;
        }
        Current = frame;
        SampleRawForStrike(result.original.frameTime);
    }

    void SampleRawForStrike(double frameTime)
    {
        if (frameTime <= lastRawFrameTime) return;           // smoother re-emits; only new camera frames
        lastRawFrameTime = frameTime;
        if (!TryGetBodyInches(engine, PoseNodeIndex.LeftHand, false, out var left) ||
            !TryGetBodyInches(engine, PoseNodeIndex.RightHand, false, out var right))
        {
            strikeDetector.Cancel();                         // lost paw → disarm
            return;
        }
        if (strikeDetector.AddSample(frameTime, left, right, out var power))
        {
            Shot?.Invoke(new PawShot { playerIndex = playerIndex, direction = aim, power01 = power });
        }
    }

    static Vector2 ClampAimUpward(Vector2 dir, float minAngleDeg)
    {
        var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;             // (-180, 180]
        if (angle < 0f) angle = dir.x >= 0f ? minAngleDeg : 180f - minAngleDeg;
        return Vector2Utils.PolarDeg(Mathf.Clamp(angle, minAngleDeg, 180f - minAngleDeg)); // Nex.Utils
    }
    // TryGetBodyInches: §15.2
}
```

```csharp
// Pure C#; unit-testable. Times are camera frameTime seconds. Positions are ppi-inches rel. chest.
public sealed class PawStrikeDetector
{
    enum State { Disarmed, Armed, Striking, Cooldown }

    readonly PawInputConfig config;
    State state;
    bool enabled;
    bool hasPrev;
    double prevTime, stateStartTime;
    Vector2 prevRight;
    float prevDistance, strikeStartDistance, peakSpeed;

    public PawStrikeDetector(PawInputConfig config) { this.config = config; }
    public bool IsStriking => state == State.Striking;
    public void SetEnabled(bool value) { enabled = value; if (!value) Cancel(); }
    public void Cancel() { state = State.Disarmed; hasPrev = false; }

    public bool AddSample(double time, Vector2 left, Vector2 right, out float power01)
    {
        power01 = 0f;
        var toLeft = left - right;
        var distance = toLeft.magnitude;
        if (!hasPrev || time - prevTime <= 0 || time - prevTime > 0.25)   // first sample or tracking gap: resync only
        {
            Resync(time, right, distance);
            return false;
        }
        var dt = (float)(time - prevTime);
        var dir = distance > 1e-3f ? toLeft / distance : Vector2.up;
        var speedTowardLeft = Vector2.Dot((right - prevRight) / dt, dir);   // right-paw speed toward left paw, in/s
        var distanceBefore = prevDistance;
        Resync(time, right, distance);
        if (!enabled) return false;

        switch (state)
        {
            case State.Disarmed:
            case State.Cooldown:
                var cooledDown = state == State.Disarmed || time - stateStartTime >= config.Cooldown;
                if (cooledDown && distance >= config.ArmDistanceInches) SetState(State.Armed, time);
                return false;
            case State.Armed:
                if (speedTowardLeft >= config.StrikeSpeedInchesPerSec)
                {
                    strikeStartDistance = distanceBefore;            // distance at the last calm sample
                    peakSpeed = speedTowardLeft;
                    SetState(State.Striking, time);
                }
                return false;
            case State.Striking:
                peakSpeed = Mathf.Max(peakSpeed, speedTowardLeft);
                var travelled = strikeStartDistance - distance;
                var landed = distance <= config.ContactDistanceInches || speedTowardLeft < peakSpeed * 0.5f;
                if (travelled >= config.MinStrikeTravelInches && landed)
                {
                    power01 = Mathf.InverseLerp(config.StrikeSpeedInchesPerSec, config.FullPowerSpeedInchesPerSec, peakSpeed);
                    SetState(State.Cooldown, time);
                    return true;
                }
                if (time - stateStartTime > config.MaxStrikeDuration) SetState(State.Armed, time);   // too slow: abort
                return false;
        }
        return false;
    }

    void Resync(double time, Vector2 right, float distance) { prevTime = time; prevRight = right; prevDistance = distance; hasPrev = true; }
    void SetState(State s, double time) { state = s; stateStartTime = time; }
}
```
Implementation notes for the detector:
- **Starting distance.** `strikeStartDistance` is the distance between the paws at the last calm sample, taken just before speed crossed the threshold.
- **Namespaces.** `RemapUtils` and `Vector2Utils` live in `Nex.Utils`, so add `using Nex.Utils;`.
- **Testing.** Write EditMode tests that feed synthetic trajectories:
  - a fast approach should fire exactly once;
  - a slow approach should not fire;
  - leaning (both paws moving together) should not fire;
  - a gap longer than 0.25 s should resync.
- **Aim.** Aim is frozen while `IsStriking`, so the shot uses the pre-strike direction. The direction wobbles during a fast strike.
- **Whole-body motion.** Positions are relative to the chest, and closing speed is measured along the paw-to-paw vector. Leaning or stepping therefore does not trigger a shot.
- **Latency.** Expect about 150–250 ms from the physical strike to `Shot` (camera plus model plus about 30 Hz sampling) **[UNVERIFIED on device]**. That is acceptable for turn-based shots. Give instant visual feedback, such as a cue-line pulse, when the state enters `Striking`.
- **Analytics.** Log `pawShot` (power, angle, playerIndex) and tracking lost/regained through `AnalyticsManager.TrackEvent`. Show values with `DebugPrinter` when enabled.

### 15.4 Keyboard mock source (dev and Editor)
```csharp
public class KeyboardPawInputSource : MonoBehaviour, IPawInputSource
{
    // Left/Right arrows: ballX01 ±; A/D: rotate aim; Space: Shot(power 0.7). Use Nex.Dev.DebugInput (false in PRODUCTION).
}
```
Select the source per player from a `DebugSettings` flag (`ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR`). This lets the whole turn loop run with no camera. Pair it with the S key to skip setup.

### 15.5 Gameplay PiP with per-player indicators and active highlight (Q5)
Build the new prefab `Assets/Prefabs/Views/Gameplay/GameplayCameraPip.prefab` via an Editor script, following ARGameExample's structure:
```
GameplayCameraPip (anchor/pivot bottom-right, 384×216 (16:9), pos (-32, 32)) [GameplayCameraPip, CanvasGroup]
├─ Border [Image, pixel-art 9-slice]                              (optional)
├─ PreviewFrame (stretch, 6 px inset) [AreaPreviewFrame(rawImage, canvasGroup, enableSmoothing=false), CanvasGroup]
│   └─ RawImage (stretch) [RawImage (+ RoundedRectModifier optional)]
└─ PlayerIndicatorsManager [PlayerIndicatorsManager(playerIndicatorPrefab=PreviewFramePlayerIndicator.prefab, ratio≈0.18)]
```
- Parent it under a **Screen Space – Overlay** HUD canvas. A Screen Space – Camera canvas on the main camera would get HD-2D pixelation, DoF, and bloom on the camera feed.
- Don't animate its scale from 0. `AreaPreviewFrame` caches the aspect ratio on the first texture, and an aspect of 0 gives a zero-width `uvRect`. Animate `CanvasGroup.alpha` or position instead.
- A 16:9 PiP shows exactly the play area: one player at 1P, both at 2P.

```csharp
public class GameplayCameraPip : MonoBehaviour
{
    [SerializeField] AreaPreviewFrame previewFrame = null!;
    [SerializeField] PlayerIndicatorsManager playerIndicatorsManager = null!;

    public void Initialize(int numOfPlayers, BasePlayAreaController playAreaController, BodyPoseDetectionManager bodyPoseDetectionManager)
    {
        previewFrame.Initialize(playAreaController);
        playerIndicatorsManager.Initialize(numOfPlayers, previewFrame, bodyPoseDetectionManager);
    }

    public void SetActivePlayer(int playerIndex) => playerIndicatorsManager.SetActivePlayer(playerIndex);
}
```
Additive extensions to the starter scripts (they extend rather than duplicate, per the rules):
```csharp
// PlayerIndicatorsManager
readonly List<PreviewFramePlayerIndicator> indicators = new();
// in InitializePlayerIndicators(): indicators.Add(indicator);
public void SetActivePlayer(int activePlayerIndex)
{
    foreach (var indicator in indicators) indicator.SetHighlighted(indicator.PlayerIndex == activePlayerIndex);
}

// PreviewFramePlayerIndicator
public int PlayerIndex => playerIndex;
public void SetHighlighted(bool highlighted)
{
    indicator.localScale = Vector3.one * (highlighted ? 1.4f : 1f);            // Root: position is script-driven, scale is free
    image.color = highlighted ? Color.white : new Color(1f, 1f, 1f, 0.45f);    // Icon sizeDelta is overwritten each update; don't animate it
}
```
- Optionally add a DOTween or MMFeedbacks pulse on `Root` for the active player, a "P1/P2 turn" banner in the matching colour (red/blue sprite), and a photo from `PlayerPhotoManager` (RawImage + uvRect).
- Alternative: one `PlayerFocusPreviewFrame` per player, showing only the active one. Its `Initialize(playerIndex, numOfPlayers, bpdm)` follows that player's chest.

### 15.6 Lost-tracking feedback
Check three levels:
- **Paw level** (immediate): `PawFrame.isTracked == false`, meaning a paw node was hidden for more than 0.2 s. Dim the aim line, show a localized "Show both paws" hint near the PiP, and flash that player's indicator. The strike is cancelled automatically.
- **Player level**: subscribe to `SetupStateManager.PlayerTrackerUpdated`.
  - `setupSummary.setupStateType == SetupStateType.PlayingButNoPose` means no pose for more than 2 s. Pause the turn timer and show a setup-style overlay (the PiP enlarged, or a PlayerFocusPreviewFrame).
  - `currentSetupIssue` gives the position hint. Use localized strings, not `SetupWarningMessage`'s English.
- **Camera level**: `CherryIntegrationManager.Instance.PreferGameStopped` / `WaitForResumeIfNeeded(ct)` covers camera mute and the platform pause.
- Consider enabling override flags on the Gameplay entry of `SetupDetectorWarningConfig`, for example a looser `chestXToCenterMaxInches`. Players lean while shooting.

---

## 16. Gotchas and bugs (read before coding)
1. `SetupStateManager.Update` uses `Input.GetKeyDown(KeyCode.S)` with **no dev gate**, so it works in production builds. If that matters, wrap it with `Nex.Dev.DebugInput`.
2. **Bug:** `SetupStateManager.ClearTrackers()` doesn't clear `playerStates`. After `ResetSetupStates()` or `SetTrackingEnabled(false)` followed by `SetTrackingEnabled(true)`, the extra default (Preparing) entries make `WaitForGoodPlayerPosition` / `WaitForRaiseHand` **never resolve**. Fix it by adding `playerStates.Clear()`, or never re-create trackers.
3. `DetectionManager.Initialize` isn't idempotent (PlayAreaController subscribes twice). Call it once per scene.
4. `BaseOnePlayerDetectionEngine.LeftWrist/RightWrist/LeftEar/RightEar/LeftAnkle/RightAnkle` and their `Original*` versions **throw KeyNotFoundException**, because those nodes are never created. `GetNodePosition` returns `null` for them.
5. `GetNodePosition` returns a bogus non-null position before the first detection. The containers are inactive, but `node.activeSelf` is true.
6. Smoothed nodes use `CirclePoseNode`, a visible SpriteRenderer. Use a HiddenPoseNode variant in the 3D game.
7. The `processed` stream fires every `Update`, not per camera frame. The manager's own `captureAspectNormalizedDetection` has `original == processed`, unsmoothed. Know which one a component listens to (see §1).
8. Jazz and Nex share type names: `OneEuroFilter`, `ComposedFilter2D`, `IFilter`, `IFilter2D`, `SetupDetector`, `SetupIssueType`, `SetupIssueInfo`, `SetupDetection`.
   - Write code in `namespace Nex` / `Nex.*` with `using Jazz;` at the **file top**.
   - A `using Jazz;` inside the namespace block, or code outside `Nex`, makes these resolve to Jazz or become ambiguous (CS0104).
9. `DetectionUtils` hard-codes 16:9.
10. `AreaPreviewFrame` and `PlayerSetupPreviewFrame` cache the world rect and aspect once. Don't resize or scale the frame to 0 around the first texture.
11. Setup preview frames stay registered as preview handlers after `MoveOut` (they are only offscreen). They still receive textures and shape the native crop request. Destroy them after setup if that matters on device **[UNVERIFIED impact]**.
12. With no camera device, nothing runs, VideoClip included. `frameProviderType` must be set before `CvDetectionManager.Start`.
13. `PlayerPhotoSprite` and `PlayerPreviewSprite` call `Sprite.Create` per update, and `OnePlayerPhotoTracker.TakePhoto` never destroys the old `latestPhoto`. Both are costly on low-end GPUs.
14. Hard-coded English appears in `SetupWarningMessage` and in the example prompts.
15. Timing: engines, trackers, and indicators use `Time.fixedTime` (scaled). `Time.timeScale = 0` freezes the setup histories. The smoother uses scaled `Time.time`.
16. `PlayerPositionDefinition.GetXRatioForPlayer` throws for `numOfPlayers` outside 1..4.
17. `OnePlayerSetupStateTracker` never unsubscribes from `processed`. This is harmless after destroy (`isTracking=false`), but it is a leak.
18. `ChestTooLeft` and `ChestTooRight` are never produced by the tracker's issue list.
19. The Gameplay `SetupDetectorWarningConfig` entry ignores its own values (`overrideWarningConfig: 0`).

## 17. Unverified or not checked
- On-device mirroring of the Nex Playground camera. It relies on NexCamera's native front-facing handling. The L/R-by-x guarantee itself is code-verified.
- Real detection FPS and latency on the target Android box. `maxPoseDetectionFps = 30` is only the cap.
- The VideoClip provider path end-to-end, and the MDK replay tooling.
- The effect of preview-handler regions on native zoom/dewarp (`enableNativeZoom=true`) on device.
- All PawInput thresholds in §15.3 are initial guesses and must be tuned with real players (adults and kids, 1P and 2P).
- The exact `LocalizedString` / `NexLocalizedString` usage for prompts. Only `NexLocalizedString.StringReference.GetLocalizedString()` was seen, in `WelcomeScreenView`.
