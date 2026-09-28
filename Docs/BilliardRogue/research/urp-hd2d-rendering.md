# URP 17.3 HD-2D Rendering Reference (Billiard Rogue)

Scope: Unity 6000.3.9f1, `com.unity.render-pipelines.universal@17.3.0` + `core@17.3.0`, Android GLES3, 1920x1080 TV, 60 fps.
Sources read: `Starter/Library/PackageCache/com.unity.render-pipelines.universal@7327e77c1cc2/` (= `URP/` below),
`com.unity.render-pipelines.core@04ab0eefa0c3/` (= `CORE/`), `team.nex.dual-blur@9a1424175a42`, starter prefabs/settings.
Items marked **[unverified]** were not confirmed in source or on device.
Full shader sources (ToonLit, LitPixelParticle, LightShaft) are in the appendix **`urp-hd2d-shaders.md`** (same folder).

---

## 0. TL;DR decisions

| Topic | Decision |
|---|---|
| Render Graph | **Mandatory.** Compatibility Mode is compiled out (no `URP_COMPATIBILITY_MODE` define). Only `RecordRenderGraph` passes run. |
| Low-res world | **World camera → low-res point-filtered RenderTexture (e.g. 640x360 = 3x) with post-processing ON; show it full-screen via a RawImage (or tiny composite pass) under the starter's full-res view canvases.** Do NOT use `renderScale` (it pixelates the starter UI). |
| Post FX | Built-in Bloom (Dual filter, Half, HDR on), Tonemapping, Color grading (all baked into LUT), Vignette. Tilt-shift = custom separable RG pass (below). No built-in DoF. |
| Lighting path | Forward (not Forward+), per-pixel additional lights, per-object limit 4, 1 shadow cascade, hard shadows, shadowmap 1024. |
| Shaders | Custom HLSL toon (N bands) + lit alpha-clip billboard particles; `_CLUSTER_LIGHT_LOOP` (not `_FORWARD_PLUS`) and `LIGHT_LOOP_BEGIN/END`. |
| Blocker found | `team.nex.dual-blur` `BlurredTextureBackground` uses `Configure/Execute` → **no-op under 17.3** (only logs a warning). Background blur for overlay views does not work; do not add a second `CameraChainItem`. |

---

## 1. Render Graph status (verified)

- `URP/Runtime/Settings/RenderGraphSettings.cs`: without `URP_COMPATIBILITY_MODE`, `enableRenderCompatibilityMode` getter returns `false`; setter is `[Obsolete(..., true)]` "Compatibility Mode is being removed ... #from(6000.3)".
- Project defines (`ProjectSettings.asset` `scriptingDefineSymbols.Android`) do **not** contain `URP_COMPATIBILITY_MODE`. Global settings asset has `m_EnableRenderCompatibilityMode: 0`.
- `ScriptableRenderPass.Execute/Configure/OnCameraSetup` exist only as empty `[Obsolete]` stubs (`URP/Runtime/Deprecated.cs` region `#if !URP_COMPATIBILITY_MODE`). A pass without `RecordRenderGraph` logs: *"The render pass ... does not have an implementation of the RecordRenderGraph method"* and draws nothing.
- `ScriptableRendererFeature.SetupRenderPasses` (empty stub) and `IntermediateTextureUsage`/`useIntermediateTextures` (obsolete `#from(6000.3)`) do nothing; `ScriptableRenderer.cameraColorTargetHandle` is a stub returning `null` (`Deprecated.cs:946`). Old-style code compiles (with obsolete warnings) but silently renders nothing.
- Native Render Pass compiler is always on in RG (`RenderGraph.nativeRenderPassesEnabled` defaults `true`; `UniversalRendererRenderGraph.cs:597` overrides the renderer's `m_UseNativeRenderPass`).
- Built-in `FullScreenPassRendererFeature` (fields `injectionPoint`, `fetchColorBuffer`, `requirements`, `passMaterial`, `passIndex`, `bindDepthStencilAttachment`) works in RG and is fine for trivial one-material effects.
- `OnTilePostProcessFeature` exists (Vignette/ColorLookup/ColorAdjustments/Tonemapping/FilmGrain only, requires Vulkan/Metal framebuffer fetch, conflicts with built-in PP). **Not usable** (we run GLES3 and need Bloom).

---

## 2. Current project configuration and required changes

### 2.1 Values found

| File / field | Current | Meaning |
|---|---|---|
| `GraphicsSettings.asset m_CustomRenderPipeline` | guid `ff5c3ee2…` = `Assets/URP/URPAsset.asset` | Default pipeline; no quality level overrides it (`customRenderPipeline: {fileID: 0}` everywhere). |
| `QualitySettings m_PerPlatformDefaultQuality.Android` | `2` ("Medium", `vSyncCount: 1`, `antiAliasing: 0`) | Frame rate is capped by starter `ApplicationManager` (`Application.targetFrameRate = min(60, refresh)`), NexCamera sets `vSyncCount = 0`. |
| `ProjectSettings m_ActiveColorSpace` | `0` = **Gamma** | See 2.3. |
| `m_BuildTargetGraphicsAPIs AndroidPlayer` | `m_APIs: 0b000000` (=11 = **OpenGLES3** only), `m_Automatic: 0` | GLES3 → `MAX_VISIBLE_LIGHTS` = 16 (`Input.hlsl`, low-end mobile path). |
| `scriptingBackend.Android` / `AndroidTargetArchitectures` | `1` IL2CPP / `2` ARM64 | OK. |
| `m_MTRendering`, `mobileMTRendering.Android` | 1 / 1 | OK. Graphics jobs off. |
| `m_BuildTargetDefaultTextureCompressionFormat Android` | `02000000` = ETC2 | Pixel-art sprite sheets: override per texture to RGBA32 (ETC2 smears pixel edges). |
| `m_BuildTargetBatching Android` | static 1, dynamic 0 | OK. |
| `androidUseSwappy` | 1 | Frame pacing on. |
| `URPAsset.asset` | `m_SupportsHDR 0`, `m_MSAA 1`(off), `m_RenderScale 1`, `m_UpscalingFilter 0`(Auto), `m_RequireDepthTexture 0`, `m_RequireOpaqueTexture 0`, `m_MainLightShadowmapResolution 2048`, `m_ShadowCascadeCount 1`, `m_ShadowDistance 50`, `m_SoftShadowsSupported 0`, `m_AdditionalLightsRenderingMode 1`(PerPixel), `m_AdditionalLightsPerObjectLimit 4`, `m_AdditionalLightShadowsSupported 0`, `m_UseSRPBatcher 1`, `m_ColorGradingMode 0`(LDR), `m_ColorGradingLutSize 32`, `m_HDRColorBufferPrecision 0`(32-bit), `m_SupportsLightCookies 1`, `m_VolumeProfile none`, `m_VolumeFrameworkUpdateMode 0`(EveryFrame), `m_UseAdaptivePerformance 1` | |
| `URPAsset_Renderer.asset` (UniversalRendererData) | `m_RenderingMode 0`(Forward), `m_IntermediateTextureMode 1`(**Always**), `m_CopyDepthMode 1`(AfterTransparents), `m_DepthPrimingMode 0`, `m_RendererFeatures: []`, `m_UseNativeRenderPass 0`(ignored in RG), `postProcessData` set, `m_ShadowTransparentReceive 1` | |
| Global default volume profile | `Assets/DefaultVolumeProfile.asset` (all overrides at defaults, i.e. inactive) | |

### 2.2 Camera setup found in starter

`Assets/Prefabs/Coordinators/MainCoordinator.prefab`:
```
MainCoordinator
├─ Main Camera   (Camera ortho, depth -1, clear Skybox, cull Everything, m_HDR 1, tag MainCamera, AudioListener;
│                 UniversalAdditionalCameraData: Base, m_Cameras=[RootCamera], renderPostProcessing 0)
├─ MainViewManager (prefab instance, MainViewManager.prefab)
│    └─ CameraChain/RootCamera (Camera ortho, clear Nothing; URP: **Overlay**, PP off;
│                                CameraChainItem baseCameras=[Main Camera], backgroundConfig.configIndex -1)
├─ UIBackground  (Canvas ScreenSpaceCamera, worldCamera=RootCamera, planeDistance 300, CanvasScaler 1920x1080) └─ Image (full stretch)
└─ SingletonSpawner
```
- `ViewManager.cameraChains` has exactly one item → `SimpleCanvasView.ViewCamera` setter forces `RenderMode.ScreenSpaceCamera` on RootCamera; `ViewOrder` sets `planeDistance = 300 - 10*order` (views start at 290).
- `CameraChainItem.SetTargetTexture` overwrites `targetTexture` of its own camera **and every camera in `baseCameras`** when a higher chain activates. Never put the low-res World camera in `baseCameras`.

### 2.3 Changes to make (via Editor script, see §8)

| Setting | Target | Why |
|---|---|---|
| `supportsHDR` | **true** (32-bit `_32Bits`) | Bloom threshold >1 isolates emissive; world RT is small so cost is tiny. Also set **Main Camera `allowHDR = false`** (else the 1080p UI stack becomes HDR). GLES3 needs `EXT_color_buffer_float` for B10G11R11; URP falls back to LDR otherwise **[unverified on target device]**. |
| `colorGradingMode` | HighDynamicRange | Tonemapping baked into 32³ LUT (LDR mode runs tonemap per pixel in Uber). |
| `mainLightShadowmapResolution` | 1024 (512 if arena is small) | Output is 640x360. |
| `shadowCascadeCount` / `shadowDistance` | 1 / just past the far arena edge (~25–35) | Keyword `_MAIN_LIGHT_SHADOWS` (1 cascade) vs `_MAIN_LIGHT_SHADOWS_CASCADE` (>1) — `MainLightShadowCasterPass.cs:365`. |
| `m_SoftShadowsSupported` | false (already) | Hard pixel shadows; lets shaders omit `_SHADOWS_SOFT*` variants. |
| `maxAdditionalLightsCount` | 4 (max 8, `UniversalRenderPipeline.maxPerObjectLights`) | Forward per-object list; split big floor meshes into tiles so lights don't pop. |
| `m_SupportsLightCookies` | false | Drops `_LIGHT_COOKIES` variants / work. |
| `supportsCameraDepthTexture` (asset) | false; enable per camera (`requiresDepthOption = On`) on World camera only if god-ray depth fade / soft particles are used | Built-in DoF would auto-request depth (`CheckPostProcessForDepth`). |
| `supportsCameraOpaqueTexture` | false | |
| `msaaSampleCount` | 1 | Pixel look; MSAA wasted. |
| `renderScale` | 1.0 (keep) | See §3. |
| `useSRPBatcher` | true (already) | |
| Renderer `intermediateTextureMode` | Auto | "Always" forces an extra copy for cameras that don't need it. (The Main+Root stack always uses an intermediate anyway: `RequiresIntermediateColorTexture` returns true for a Base camera with a stack.) |
| Renderer `renderingMode` | Forward | ≤16 visible lights, few per object. Forward+ only if many overlapping lights are required. |
| Color space | **Keep Gamma** unless art insists | Linear changes all starter UI/TMP blending; bloom/grading support gamma (`UNITY_COLORSPACE_GAMMA` paths in `Bloom.shader`). World RT format must be `R8G8B8A8_UNorm` in Gamma, `_SRGB` in Linear. |
| Graphics API | Keep GLES3 | Vulkan untested on Playground hardware **[unverified]**. |

---

## 3. Low-res world, full-res UI

### 3.1 Option analysis (source-verified)

**(b) `renderScale` + `upscalingFilter = Point` — rejected.**
- `renderScale` lives on the pipeline asset and applies to every Game camera (`InitializeStackedCameraData`: `cameraData.renderScale = settings.renderScale`, only SceneView/Preview/Reflection are exempt; `scaledWidth = pixelWidth * renderScale`).
- Overlay cameras are created from the **base camera's** data (`CreateCameraData(overlayFrameData, baseCamera, ...)`) and render into the same scaled intermediate; the upscale happens once in the final pass → the starter's view canvases (ScreenSpaceCamera on the RootCamera overlay) get pixelated too.
- A separate base camera for UI is also scaled (global setting). Hack: flip `asset.renderScale` in `RenderPipelineManager.beginCameraRendering` (it fires before `CreateCameraData`) — mutates the asset (dirties it in Editor), fragile. Don't.
- Only **Screen Space – Overlay** canvases stay crisp (drawn by `DrawScreenSpaceUIPass.RenderOverlay` onto the backbuffer after the final blit when `isLastBaseCamera` and HDR output off, `UniversalRendererRenderGraph.cs:1545`), but `SimpleCanvasView` forces ScreenSpaceCamera.
- `UpscalingFilterSelection.Auto` picks Point only for exact integer ratios; `Point` = NN via `_POINT_SAMPLING` keyword in the final pass.

**(a) World camera → low-res RenderTexture — recommended.**
- A camera with `targetTexture` is an offscreen camera; post-processing runs at RT size (bloom starts at RT/2), shadows/lighting unaffected, UI untouched.
- The display is one full-screen textured quad at 1080p (RawImage, point filtered). Cost ≈ one texture fetch per screen pixel.
- `RequiresIntermediateColorTexture`: offscreen camera needs an intermediate only when PP/opaque texture/MSAA-resolve/non-default viewport → with PP on it renders into an intermediate at RT size then final-blits into the RT.

**(c) Custom renderer feature** — same result as (a) without a canvas: import the RT and blit it with nearest filtering into the UI stack's color before UI draws. Use only if a RawImage is awkward:
```csharp
// In a pass at RenderPassEvent.BeforeRenderingOpaques on the Main Camera's renderer
var src = renderGraph.ImportTexture(worldRtHandle);            // RTHandle from RTHandles.Alloc(worldRT) — allocate once, Release on dispose
renderGraph.AddBlitPass(src, resourceData.activeColorTexture, Vector2.one, Vector2.zero,
    filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest, passName: "PixelWorldComposite");
```

### 3.2 Recommended rig

```
GameplayRoot (in the gameplay scene / prefab)
├─ WorldCamera   Camera: perspective, low FOV (~20–30°) tilted down (HD-2D), or ortho; depth -2; cull = World layers only;
│                clear SolidColor; targetTexture assigned at runtime; allowHDR true; allowMSAA false
│                URP: Base, renderPostProcessing TRUE, antialiasing None, volumeLayerMask = WorldVolume layer,
│                requiresDepthOption On only if needed; NOT in any CameraChainItem.baseCameras
├─ WorldVolume   Volume (isGlobal, sharedProfile = WorldVolumeProfile) on a layer only WorldCamera listens to
└─ lights, arena, etc. (all on "World" layer)
Main Camera (starter): cull mask must exclude World layers; clear SolidColor (skybox is wasted fill); allowHDR false.
Display: RawImage (full-stretch, raycastTarget off) as the **first child of the gameplay SimpleCanvasView prefab**,
         or its own ScreenSpaceCamera canvas on RootCamera with planeDistance 295 (behind views at ≤290, in front of UIBackground at 300).
```
Layers must be added through an Editor script (SerializedObject on `ProjectSettings/TagManager.asset` → `layers` array), not by hand.

### 3.3 `PixelWorldDisplay` (owner of the RT, snapping, world→UI mapping)

```csharp
#nullable enable
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace Nex.Rendering
{
    // Owns the low-res world render target and presents it through a RawImage with integer upscaling.
    public sealed class PixelWorldDisplay : MonoBehaviour
    {
        [SerializeField] Camera worldCamera = null!;
        [SerializeField] RawImage display = null!;
        [Tooltip("Visible vertical texels. 1080 / 360 = 3x integer scale.")]
        [SerializeField] int visibleHeight = 360;
        [Tooltip("Extra texels per side so sub-texel camera motion can be shown by shifting the uvRect.")]
        [SerializeField] int marginTexels = 1;

        RenderTexture? worldTexture;

        public void Initialize()
        {
            var scale = Mathf.Max(1, Mathf.RoundToInt(Screen.height / (float)visibleHeight));
            var visibleW = Screen.width / scale;
            var visibleH = Screen.height / scale;
            var format = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
            worldTexture = new RenderTexture(visibleW + 2 * marginTexels, visibleH + 2 * marginTexels,
                format, GraphicsFormat.D24_UNorm_S8_UInt)
            {
                name = "PixelWorldRT", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                useMipMap = false, antiAliasing = 1,
            };
            worldTexture.Create();
            worldCamera.targetTexture = worldTexture;
            display.texture = worldTexture;
            if (worldCamera.orthographic)   // cover the margin so the visible part keeps the designed framing
            {
                worldCamera.orthographicSize = worldCamera.orthographicSize * worldTexture.height / visibleH;
            }
            // Perspective: widen fieldOfView the same way, e.g. 2*atan(tan(fov/2) * rtHeight / visibleH).
            ApplyUvRect(Vector2.zero);
        }

        void OnDestroy()
        {
            if (worldTexture == null) return;
            worldCamera.targetTexture = null;
            worldTexture.Release();
            Destroy(worldTexture);
        }

        // Call instead of moving the camera directly (pans, shakes). Snaps to the texel grid to kill pixel crawl.
        public void SetCameraPosition(Vector3 position)
        {
            var rotation = worldCamera.transform.rotation;
            var local = Quaternion.Inverse(rotation) * position;
            var texel = WorldUnitsPerTexel();
            var snapped = new Vector3(Mathf.Round(local.x / texel) * texel, Mathf.Round(local.y / texel) * texel, local.z);
            worldCamera.transform.position = rotation * snapped;
            ApplyUvRect(new Vector2((local.x - snapped.x) / texel, (local.y - snapped.y) / texel));
        }

        // 0..1 over the screen for a world position; use as anchorMin/anchorMax of a label under a full-screen rect.
        public Vector2 WorldToScreenNormalized(Vector3 worldPosition)
        {
            var vp = worldCamera.WorldToViewportPoint(worldPosition);   // 0..1 over the whole RT (incl. margin)
            var uv = display.uvRect;
            return new Vector2((vp.x - uv.x) / uv.width, (vp.y - uv.y) / uv.height);
        }

        float WorldUnitsPerTexel()
        {
            var t = worldCamera.transform;
            if (worldCamera.orthographic) return 2f * worldCamera.orthographicSize / worldTexture!.height;
            // Perspective: exact only at the focus plane (distance along view axis to the arena centre).
            var focusDistance = Vector3.Dot(-t.position, t.forward);   // arena centre at origin; adjust to your pivot
            return 2f * focusDistance * Mathf.Tan(0.5f * worldCamera.fieldOfView * Mathf.Deg2Rad) / worldTexture!.height;
        }

        void ApplyUvRect(Vector2 subTexelError)
        {
            var w = (float)worldTexture!.width;
            var h = (float)worldTexture.height;
            display.uvRect = new Rect((marginTexels + subTexelError.x) / w, (marginTexels + subTexelError.y) / h,
                (w - 2 * marginTexels) / w, (h - 2 * marginTexels) / h);
        }
    }
}
```
HP label usage (label anchored inside a full-screen stretched RectTransform of a view):
```csharp
var n = pixelWorldDisplay.WorldToScreenNormalized(enemy.LabelAnchor.position);
label.anchorMin = label.anchorMax = n;          // no camera math, independent of CanvasScaler
label.anchoredPosition = labelOffset;           // offset in canvas units (1920x1080 reference)
```

### 3.4 Pitfalls
- **Pixel crawl/shimmer**: static camera = no crawl. For pans/shakes use `SetCameraPosition` (snap + uvRect remainder); or shake the RawImage `anchoredPosition` in multiples of the pixel scale (3 px) instead of moving the camera. Rotation/zoom always crawls — avoid or accept during transitions.
- Perspective snapping is exact only at the focus plane; tilted HD-2D cameras still show minor parallax crawl on near/far props.
- Keep RT dimensions an integer divisor of 1920x1080 (3x → 640x360, 4x → 480x270). Editor Game view at odd sizes → non-integer; recreate RT on `Screen` size change if needed.
- Textures on 3D props: point filter, mipmaps on, texel density ≈ 1 texel per RT texel at arena distance.
- `CameraChainItem` rewrites `targetTexture` on `baseCameras` — keep WorldCamera out.
- If `supportsHDR` is enabled, the Main Camera (`m_HDR: 1` in prefab) turns its 1080p stack HDR: set `allowHDR = false` on it.
- UI World-to-screen: never use `Camera.main` (Main Camera is the ortho UI base camera, not the world).
- Screen shake of HUD vs. world: shake only the world display to keep UI readable.

---

## 4. Render Graph API reference (URP 17.3)

Namespaces: `UnityEngine.Rendering` (Blitter, CoreUtils, VolumeManager), `UnityEngine.Rendering.Universal` (features, frame data), `UnityEngine.Rendering.RenderGraphModule` (RenderGraph, TextureHandle, TextureDesc, builders, AccessFlags), `UnityEngine.Rendering.RenderGraphModule.Util` (RenderGraphUtils).

### 4.1 Feature / pass
```csharp
public abstract partial class ScriptableRendererFeature : ScriptableObject, IDisposable
    public abstract void Create();                                   // called on (de)serialization/OnValidate
    public abstract void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData); // per camera
    public virtual void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData);
    protected virtual void Dispose(bool disposing);
    public bool isActive; public void SetActive(bool active);

public abstract partial class ScriptableRenderPass : IRenderGraphRecorder
    public RenderPassEvent renderPassEvent { get; set; }
    public bool requiresIntermediateTexture { get; set; }            // forces cameraColor != backbuffer
    public void ConfigureInput(ScriptableRenderPassInput passInput); // Depth | Normal | Color | Motion
    public virtual void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData);
renderer.EnqueuePass(pass);
```
`RenderingData` in `AddRenderPasses`: `renderingData.cameraData.cameraType`, `.postProcessEnabled`, `.camera`, `.cameraTargetDescriptor` (ref accessors over `UniversalCameraData`).

`RenderPassEvent` values: BeforeRendering 0, BeforeRenderingShadows 50, AfterRenderingShadows 100, BeforeRenderingPrePasses 150, AfterRenderingPrePasses 200, BeforeRenderingOpaques 250, AfterRenderingOpaques 300, BeforeRenderingSkybox 350, AfterRenderingSkybox 400, BeforeRenderingTransparents 450, AfterRenderingTransparents 500, **BeforeRenderingPostProcessing 550** (before Bloom/DoF/Uber — tilt-shift goes here), **AfterRenderingPostProcessing 600** (after Uber, before final blit/FXAA), AfterRendering 1000 (backbuffer only). `event + n` offsets allowed.

### 4.2 Frame data (`frameData.Get<T>()`)
- `UniversalResourceData`: `activeColorTexture`, `activeDepthTexture`, `isActiveTargetBackBuffer`, `cameraColor { get; set; }`, `cameraDepth { get; set; }`, `backBufferColor`, `cameraDepthTexture`, `cameraNormalsTexture`, `cameraOpaqueTexture`, `mainShadowsTexture`, `afterPostProcessColor`, `overlayUITexture`, `SwitchActiveTexturesToBackbuffer()`. Only valid during recording, not inside render funcs.
- `UniversalCameraData`: `camera`, `cameraType`, `renderType`, `cameraTargetDescriptor`, `scaledWidth/scaledHeight`, `isHdrEnabled`, `postProcessEnabled`, `isSceneViewCamera`, `GetViewMatrix()`, `GetProjectionMatrix()`.
- Also `UniversalRenderingData`, `UniversalLightData`, `UniversalShadowData`, `UniversalPostProcessingData`.

### 4.3 Textures
```csharp
TextureDesc desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture); // explicit size (imported RTHandle → new TextureDesc(rt))
desc.name = "X"; desc.clearBuffer = false; desc.width/height; desc.msaaSamples = MSAASamples.None;
desc.filterMode; desc.format (GraphicsFormat); desc.depthBufferBits; desc.sizeMode = TextureSizeMode.Explicit;
TextureHandle t = renderGraph.CreateTexture(desc);                       // transient, pooled, frame-scoped
TextureHandle t2 = renderGraph.CreateTexture(existingHandle, "Name", clear: false);
TextureHandle imported = renderGraph.ImportTexture(rtHandle);            // RTHandle from RTHandles.Alloc(RenderTexture)
// URP helper (point filter default):
UniversalRenderer.CreateRenderGraphTexture(RenderGraph rg, RenderTextureDescriptor desc, string name, bool clear,
    FilterMode filterMode = FilterMode.Point, TextureWrapMode wrapMode = TextureWrapMode.Clamp);
```
`TextureHandle` converts implicitly to `RTHandle`/`RenderTexture`/`Texture`/`RenderTargetIdentifier` — **only inside render funcs**.

### 4.4 Passes
```csharp
using (var builder = renderGraph.AddRasterRenderPass<MyData>("Name", out var data))
{
    builder.UseTexture(src);                                   // AccessFlags.Read default
    builder.SetRenderAttachment(dst, 0, AccessFlags.WriteAll); // WriteAll = Write|Discard (no load)
    builder.SetRenderAttachmentDepth(depth, AccessFlags.Read);
    builder.SetGlobalTextureAfterPass(dst, propertyId);        // publish as global for later passes
    builder.UseGlobalTexture(propertyId);                      // consume a global
    builder.AllowPassCulling(false);                           // only if output is not consumed by the graph
    builder.AllowGlobalStateModification(true);                // required for cmd.SetGlobal*/SetKeyword
    builder.SetRenderFunc(static (MyData d, RasterGraphContext ctx) => { /* ctx.cmd is RasterCommandBuffer */ });
}
// Utilities (RenderGraphUtils, extension methods on RenderGraph):
renderGraph.AddBlitPass(src, dst, Vector2.one, Vector2.zero, filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest, passName: "Copy");
renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(src, dst, material, shaderPass), passName: "FX");
// BlitMaterialParameters fields: source, destination, scale, offset, material, shaderPass, propertyBlock,
// sourceTexturePropertyID (_BlitTexture), scaleBiasPropertyID (_BlitScaleBias), geometry (ProceduralTriangle default).
```
- Both `AddBlitPass` overloads are implemented with **`AddUnsafePass`** (not mergeable into native sub-passes). Prefer raster passes for chains on tile GPUs.
- `Blitter.BlitTexture(RasterCommandBuffer cmd, RTHandle source, Vector4 scaleBias, Material material, int pass)` binds `_BlitTexture`/`_BlitScaleBias` through a **static MaterialPropertyBlock** and draws a procedural triangle.
- **Material state gotcha**: all render funcs record into one command buffer executed later; `material.SetX()` in two passes using the same material → both draws see the last value. Put per-draw values in a **MaterialPropertyBlock passed to the draw** (MPB is copied at record time — Blitter relies on this) or use separate materials.
- Swap pattern: create `dst`, write it, then `resourceData.cameraColor = dst;` — later passes (incl. URP post-processing) use it; saves a copy back.
- `isActiveTargetBackBuffer == true` → cannot sample color; return (set `requiresIntermediateTexture = true` to avoid).
- Skip non-game cameras in `AddRenderPasses` (`cameraType != CameraType.Game`) to keep Scene view cheap/clean.

---

## 5. Built-in post-processing (URP 17.3) and mobile cost

All are `VolumeComponent, IPostProcessComponent` in `UnityEngine.Rendering.Universal`. Cost column = relative estimate for a GLES3 mid/low GPU when the world RT is 640x360 **[estimates, not profiled]**.

| Override | Key params (defaults) | Where it runs | Cost @640x360 |
|---|---|---|---|
| `Bloom` | `threshold` 0.9 (gamma-space), `intensity` 0 (=off), `scatter` 0.7, `clamp` 65472, `tint`, `highQualityFiltering` false (bicubic upsample), **`filter` `BloomFilterMode.Gaussian/Dual/Kawase`** (new), `downscale` `Half/Quarter`, `maxIterations` 6 (2–8), `dirtTexture`, `dirtIntensity` | Separate mip chain (unsafe passes) + composite in Uber. Mips = clamp(floor(log2(max(w,h)/2^(downscale+1))) - 1, 1, maxIterations) | Low. Use **Dual**, Half, maxIterations 4–5, HQ off |
| `Tonemapping` | `mode` None/Neutral/ACES | Baked into LUT when `colorGradingMode = HighDynamicRange`; per-pixel in Uber when LDR | ~0 (HDR grading) |
| `ColorAdjustments` (`postExposure`, `contrast`, `colorFilter`, `hueShift`, `saturation`), `WhiteBalance` (`temperature`, `tint`), `SplitToning` (`shadows`, `highlights`, `balance`), `LiftGammaGain` (`lift`, `gamma`, `gain` Vector4), `ShadowsMidtonesHighlights`, `ChannelMixer`, `ColorCurves` | | All baked into the per-frame LUT (`ColorGradingLutPass`, 32³) | ~0 per pixel |
| `ColorLookup` | `texture` (strip LUT), `contribution` | Extra LUT sample in Uber | Very low |
| `Vignette` | `color`, `center`, `intensity`, `smoothness`, `rounded` | Uber ALU | Very low |
| `DepthOfField` | `mode` Off/Gaussian/Bokeh; Gaussian: `gaussianStart` 10, `gaussianEnd` 30, `gaussianMaxRadius` 1 (0.5–1.5), `highQualitySampling`; Bokeh: `focusDistance`, `aperture`, `focalLength`, `bladeCount`… | Gaussian = far-field blur only, needs depth texture (auto-requested), requires MRT; Bokeh = many taps | Gaussian medium; Bokeh high. **Don't use** — tilt-shift needs near+far blur; use §6 |
| `ChromaticAberration`, `FilmGrain`, `LensDistortion`, `PaniniProjection`, `MotionBlur`, `ScreenSpaceLensFlare` | | Uber / extra passes | Avoid (except maybe tiny FilmGrain) |

Starting values for the HD-2D look (WorldVolumeProfile): Bloom threshold 1.0 (HDR), intensity 1.0–2.0, scatter 0.75–0.85, filter Dual, downscale Half, maxIterations 5; Tonemapping Neutral (ACES darkens toon palettes); ColorAdjustments postExposure 0, contrast 10, saturation 10; SplitToning warm highlights / cool shadows; Vignette 0.25 smoothness 0.4.

Creating a profile by Editor script:
```csharp
var profile = ScriptableObject.CreateInstance<VolumeProfile>();
AssetDatabase.CreateAsset(profile, "Assets/Configs/Rendering/WorldVolumeProfile.asset");
var bloom = profile.Add<Bloom>(overrides: true);                 // sets all overrideState = true
bloom.intensity.value = 1.5f; bloom.threshold.value = 1f; bloom.scatter.value = 0.8f;
bloom.filter.value = BloomFilterMode.Dual; bloom.maxIterations.value = 5;
AssetDatabase.AddObjectToAsset(bloom, profile);                   // Add() does NOT create the sub-asset
var tone = profile.Add<Tonemapping>(true); tone.mode.value = TonemappingMode.Neutral; AssetDatabase.AddObjectToAsset(tone, profile);
EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
```
Runtime tweaks (boss mood): prefer a second Volume with higher `priority` and animate `weight`; or `profile.TryGet(out Bloom b)` and set `b.intensity.value` (overrideState must be true). `Volume` fields: `isGlobal`, `priority`, `weight`, `sharedProfile`, `profile` (instantiates a copy).

---

## 6. Custom full-screen effect: tilt-shift (VolumeComponent + RG feature + HLSL)

Separable blur at half RT resolution, blended by a screen-space vertical focus band. Injected at `BeforeRenderingPostProcessing` so bloom/grading apply after it. Files: `Assets/Scripts/Rendering/TiltShift*.cs`, `Assets/Shaders/Hidden/TiltShift.shader`.

```csharp
#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nex.Rendering
{
    [Serializable, VolumeComponentMenu("Billiard Rogue/Tilt Shift")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class TiltShift : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("0 disables the effect.")]
        public ClampedFloatParameter intensity = new(0f, 0f, 1f);
        [Tooltip("Focus band centre, 0 = screen bottom, 1 = top.")]
        public ClampedFloatParameter focusCenter = new(0.45f, 0f, 1f);
        public ClampedFloatParameter focusHalfHeight = new(0.15f, 0f, 0.5f);
        public ClampedFloatParameter falloff = new(0.25f, 0.01f, 1f);
        [Tooltip("Tap spacing multiplier, in texels of each blur pass's input.")]
        public ClampedFloatParameter blurRadius = new(1.5f, 0.5f, 4f);

        public bool IsActive() => intensity.value > 0f;
    }
}
```

```csharp
#nullable enable
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Nex.Rendering
{
    public sealed class TiltShiftRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader shader = null!;   // "Hidden/BilliardRogue/TiltShift"; serialized ref keeps it in builds
        Material? material;
        TiltShiftPass? pass;

        public override void Create() => pass = new TiltShiftPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            if (!renderingData.cameraData.postProcessEnabled) return;   // UI cameras have PP off
            var volume = VolumeManager.instance.stack.GetComponent<TiltShift>();
            if (!volume.IsActive()) return;
            if (material == null) material = CoreUtils.CreateEngineMaterial(shader);
            pass!.Setup(material, volume);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }
    }

    sealed class TiltShiftPass : ScriptableRenderPass
    {
        const int blurShaderPass = 0;
        const int compositeShaderPass = 1;
        static readonly int blitTextureId = Shader.PropertyToID("_BlitTexture");
        static readonly int blitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        static readonly int blurParamsId = Shader.PropertyToID("_TiltShiftBlur");
        static readonly int bandParamsId = Shader.PropertyToID("_TiltShiftParams");
        static readonly int blurTexId = Shader.PropertyToID("_TiltShiftBlurTex");

        sealed class DrawData
        {
            public TextureHandle source, blurred;   // blurred: composite only
            public Material material = null!;
            public MaterialPropertyBlock block = null!;
            public int shaderPass;
            public Vector4 parameters;
        }

        // One block per draw: values are copied when the draw is recorded, so reuse across frames is safe.
        readonly MaterialPropertyBlock blurHBlock = new();
        readonly MaterialPropertyBlock blurVBlock = new();
        readonly MaterialPropertyBlock compositeBlock = new();
        Material material = null!;
        TiltShift settings = null!;

        public TiltShiftPass() => requiresIntermediateTexture = true;

        public void Setup(Material mat, TiltShift volume) => (material, settings) = (mat, volume);

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            var source = resourceData.activeColorTexture;
            var srcDesc = renderGraph.GetTextureDesc(source);

            var halfDesc = srcDesc;
            halfDesc.sizeMode = TextureSizeMode.Explicit;
            (halfDesc.width, halfDesc.height) = (Mathf.Max(1, srcDesc.width / 2), Mathf.Max(1, srcDesc.height / 2));
            (halfDesc.msaaSamples, halfDesc.filterMode, halfDesc.clearBuffer) = (MSAASamples.None, FilterMode.Bilinear, false);
            halfDesc.name = "_TiltShiftBlurH";
            var blurH = renderGraph.CreateTexture(halfDesc);
            halfDesc.name = "_TiltShiftBlurV";
            var blurV = renderGraph.CreateTexture(halfDesc);

            var dstDesc = srcDesc;
            dstDesc.name = "_TiltShiftColor";
            dstDesc.clearBuffer = false;
            var destination = renderGraph.CreateTexture(dstDesc);

            var radius = settings.blurRadius.value;
            AddDraw(renderGraph, "TiltShift BlurH", source, TextureHandle.nullHandle, blurH, blurShaderPass, blurHBlock,
                new Vector4(1f / srcDesc.width * radius, 0f, 0f, 0f));
            AddDraw(renderGraph, "TiltShift BlurV", blurH, TextureHandle.nullHandle, blurV, blurShaderPass, blurVBlock,
                new Vector4(0f, 1f / halfDesc.height * radius, 0f, 0f));
            AddDraw(renderGraph, "TiltShift Composite", source, blurV, destination, compositeShaderPass, compositeBlock,
                new Vector4(settings.focusCenter.value, settings.focusHalfHeight.value, settings.falloff.value, settings.intensity.value));

            resourceData.cameraColor = destination;   // URP post-processing now reads our output
        }

        void AddDraw(RenderGraph renderGraph, string name, TextureHandle source, TextureHandle blurred,
            TextureHandle target, int shaderPass, MaterialPropertyBlock block, Vector4 parameters)
        {
            using var builder = renderGraph.AddRasterRenderPass<DrawData>(name, out var data);
            (data.source, data.blurred, data.material, data.block, data.shaderPass, data.parameters) =
                (source, blurred, material, block, shaderPass, parameters);
            builder.UseTexture(source);
            if (blurred.IsValid()) builder.UseTexture(blurred);
            builder.SetRenderAttachment(target, 0, AccessFlags.WriteAll);
            builder.SetRenderFunc(static (DrawData d, RasterGraphContext ctx) =>
            {
                d.block.SetTexture(blitTextureId, d.source);
                d.block.SetVector(blitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                if (d.blurred.IsValid())
                {
                    d.block.SetTexture(blurTexId, d.blurred);
                    d.block.SetVector(bandParamsId, d.parameters);
                }
                else
                {
                    d.block.SetVector(blurParamsId, d.parameters);
                }
                ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.shaderPass, MeshTopology.Triangles, 3, 1, d.block);
            });
        }
    }
}
```

```hlsl
Shader "Hidden/BilliardRogue/TiltShift"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl" // Vert, Varyings, _BlitTexture, samplers

        float4 _TiltShiftBlur;     // xy = tap step in source UV (texel size * radius)
        float4 _TiltShiftParams;   // x = focus centre (0 bottom..1 top), y = half band height, z = falloff, w = intensity
        TEXTURE2D_X(_TiltShiftBlurTex);

        // 9-tap Gaussian folded into 5 bilinear fetches (weights for sigma ~2 texels).
        half4 Blur9(float2 uv, float2 stepUV)
        {
            float2 o1 = stepUV * 1.3846153846;
            float2 o2 = stepUV * 3.2307692308;
            half4 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0) * 0.2270270270h;
            c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + o1, 0) * 0.3162162162h;
            c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - o1, 0) * 0.3162162162h;
            c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + o2, 0) * 0.0702702703h;
            c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - o2, 0) * 0.0702702703h;
            return c;
        }

        half TiltMask(float2 uv)
        {
            // Blit.hlsl's Vert compensates UNITY_UV_STARTS_AT_TOP, so uv.y = 0 is the image bottom.
            float d = abs(uv.y - _TiltShiftParams.x) - _TiltShiftParams.y;
            half m = saturate(d / max(_TiltShiftParams.z, 1e-4));
            return m * m * (3.0h - 2.0h * m) * _TiltShiftParams.w;   // smoothstep ramp
        }
        ENDHLSL

        Pass
        {
            Name "TiltShiftBlur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlur
            half4 FragBlur(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return Blur9(input.texcoord, _TiltShiftBlur.xy);
            }
            ENDHLSL
        }

        Pass
        {
            Name "TiltShiftComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            half4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 sharp = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                half4 blurred = SAMPLE_TEXTURE2D_X_LOD(_TiltShiftBlurTex, sampler_LinearClamp, uv, 0);
                return lerp(sharp, blurred, TiltMask(uv));
            }
            ENDHLSL
        }
    }
}
```
Notes: cost at 640x360 ≈ 2 half-res passes (5 fetches) + 1 full-res pass (2 fetches). For stronger blur add a quarter-res level or a second H/V pair. Pass `requiresIntermediateTexture` is inherent (offscreen PP camera already has one). Tilt-shift reads `_BlitTexture` point-sampled for the sharp band to keep pixels crisp.

---

## 7. Custom HLSL lit shaders (URP 17.3)

### 7.1 Rules and keywords (verified in `URP/ShaderLibrary`, `URP/Shaders/Lit.shader`)
- Includes: `Core.hlsl` (transforms, `GetVertexPositionInputs`, `GetVertexNormalInputs`, `ComputeFogFactor`, `MixFog`, `GetNormalizedScreenSpaceUV`, `GetWorldSpaceNormalizeViewDir`) → `Lighting.hlsl` (pulls `RealtimeLights.hlsl`, `Shadows.hlsl`, `GlobalIllumination.hlsl` incl. `SampleSH`, `Clustering.hlsl`). `SurfaceInput.hlsl` declares `_BaseMap/_BumpMap/_EmissionMap` + samplers, `Alpha()`, `SampleAlbedoAlpha()`, `SampleNormal()`.
- Light API: `Light GetMainLight()`, `GetMainLight(float4 shadowCoord)`, `GetMainLight(float4 shadowCoord, float3 positionWS, half4 shadowMask)` (adds shadow distance fade + cookies); `int GetAdditionalLightsCount()` (returns 0 in cluster mode); `Light GetAdditionalLight(uint i, float3 positionWS)` / `(uint i, float3 positionWS, half4 shadowMask)`; `float4 TransformWorldToShadowCoord(float3 positionWS)`. `Light { half3 direction; half3 color; float distanceAttenuation; half shadowAttenuation; uint layerMask; }`.
- Shadow keywords set by URP: `_MAIN_LIGHT_SHADOWS` (1 cascade), `_MAIN_LIGHT_SHADOWS_CASCADE` (>1), `_MAIN_LIGHT_SHADOWS_SCREEN` (ScreenSpaceShadows feature); soft: `_SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH`; `_ADDITIONAL_LIGHT_SHADOWS`. `MAIN_LIGHT_CALCULATE_SHADOWS` is derived unless `_RECEIVE_SHADOWS_OFF`.
- Additional lights: `_ADDITIONAL_LIGHTS_VERTEX` / `_ADDITIONAL_LIGHTS`. **Forward+ keyword is `_CLUSTER_LIGHT_LOOP`** (`_FORWARD_PLUS` is deprecated since 6.1 and emits a `#warning`; `Core.hlsl` maps it to `USE_CLUSTER_LIGHT_LOOP`).
- `LIGHT_LOOP_BEGIN(count)` / `LIGHT_LOOP_END` (`RealtimeLights.hlsl`): non-cluster = `for (uint lightIndex = 0u; lightIndex < count; ++lightIndex)`; cluster = iterator that **requires a local `InputData inputData` with `normalizedScreenSpaceUV` and `positionWS`**. In cluster mode also loop `lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS)` first for extra directional lights (guard with `#if USE_CLUSTER_LIGHT_LOOP`).
- Fog: `#include_with_pragmas ".../ShaderLibrary/Fog.hlsl"` (emits `multi_compile_fog`).
- SRP Batcher: every non-texture material property in **one** `CBUFFER_START(UnityPerMaterial)` with identical layout in **all passes** (put it in the SubShader `HLSLINCLUDE`); textures/samplers outside; never redeclare `UnityPerDraw` built-ins. Renderers with a `MaterialPropertyBlock` drop out of the SRP batch — prefer material instances for hit-flash tints (same variant still batches).
- GPU instancing: `#pragma multi_compile_instancing` + `UNITY_VERTEX_INPUT_INSTANCE_ID` / `UNITY_SETUP_INSTANCE_ID` / `UNITY_TRANSFER_INSTANCE_ID`. SRP Batcher wins for GameObjects; instancing matters for `Graphics.RenderMeshInstanced`.
- Aux passes to reuse from `Packages/com.unity.render-pipelines.universal/Shaders/`: `ShadowCasterPass.hlsl` (`ShadowPassVertex/ShadowPassFragment`, needs `_BaseMap_ST`, `_BaseColor`, `_Cutoff`, `_BaseMap`, `Alpha()` + `multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW`), `DepthOnlyPass.hlsl` (`DepthOnlyVertex/DepthOnlyFragment`), `DepthNormalsPass.hlsl` (`DepthNormalsVertex/DepthNormalsFragment`). They define their own `Attributes/Varyings`, so keep your structs inside the ForwardLit `HLSLPROGRAM`.
- GLES3 limits: `MAX_VISIBLE_LIGHTS` 16, per-object ≤ 8, `USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA 0`.

### 7.2–7.4 Shader skeletons → `urp-hd2d-shaders.md` (same folder)

Full, copy-ready sources live in the appendix `Docs/BilliardRogue/research/urp-hd2d-shaders.md`:

| Shader | Passes | Key features |
|---|---|---|
| `BilliardRogue/ToonLit` | ForwardLit (`UniversalForward`), ShadowCaster (reuses `Shaders/ShadowCasterPass.hlsl`), notes for DepthOnly/DepthNormals | N-band ramp with soft steps, shadow-side colour, SH ambient, normal map (`_NORMALMAP`), cavity map (`_CAVITYMAP`, 0.5 neutral), rim, HDR emission, fog, alpha clip, instancing, Forward + Forward+ light loops |
| `BilliardRogue/LitPixelParticle` | ForwardLit (Queue AlphaTest, Cull Off, ZWrite On) | Alpha-clip pixel sprite, half-lambert banded main + additional lights, receives main-light shadows, flipbook from Texture Sheet Animation or `Custom1.x` (`_FLIPBOOK_CUSTOMDATA`), emission, fog |
| `BilliardRogue/LightShaft` | UniversalForward, Transparent, `Blend One One`, ZWrite Off | Two-octave scrolling noise, view-angle edge fade, along-shaft fade, optional depth fade (`_DEPTH_FADE`, needs World camera depth texture, perspective only) |

Core pattern every lit shader must follow (from the ToonLit fragment):
```hlsl
InputData inputData = (InputData)0;                       // this exact name is used inside LIGHT_LOOP_BEGIN (cluster path)
inputData.positionWS = input.positionWS;
inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);  // per pixel: correct for 1 or N cascades
Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, half4(1, 1, 1, 1));
half ramp = ToonRamp(saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation);
half3 lighting = lerp(_ShadowColor.rgb, mainLight.color, ramp) + input.sh * _AmbientStrength;
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();       // 0 in cluster mode (count not needed)
  #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
        lighting += AdditionalToon(GetAdditionalLight(dirIndex, inputData.positionWS, half4(1, 1, 1, 1)), normalWS);
  #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        lighting += AdditionalToon(GetAdditionalLight(lightIndex, inputData.positionWS, half4(1, 1, 1, 1)), normalWS);
    LIGHT_LOOP_END
#endif
```
Pragmas used: `multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE`, `multi_compile _ _ADDITIONAL_LIGHTS`, `multi_compile _ _CLUSTER_LIGHT_LOOP`,
`multi_compile_instancing`, `#include_with_pragmas ".../ShaderLibrary/Fog.hlsl"`; soft-shadow, cookie, lightmap, decal and vertex-light keywords are
deliberately omitted (they are off per §2.3). `UnityPerMaterial` CBUFFER lives in the SubShader `HLSLINCLUDE` so all passes match (SRP Batcher).
Particles: AlphaTest queue renders in the opaque pass (shadows received, depth written, no sorting); no ShadowCaster pass. God rays: rendered by the
World camera in the transparent queue, so bloom picks them up when `_Color` exceeds the threshold.

## 8. Editor-script configuration (never hand-edit the .asset files)

Place under `Assets/Scripts/Editor/`, namespace `Nex.Editor`, run through the Unity CLI. Public setters exist for: `supportsHDR`, `hdrColorBufferPrecision`, `msaaSampleCount`, `renderScale`, `upscalingFilter`, `supportsCameraDepthTexture`, `supportsCameraOpaqueTexture`, `mainLightShadowmapResolution`, `shadowDistance`, `shadowCascadeCount`, `cascade2Split…`, `shadowDepthBias`, `shadowNormalBias`, `maxAdditionalLightsCount`, `additionalLightsShadowmapResolution`, `useSRPBatcher`, `supportsDynamicBatching`, `colorGradingMode`, `colorGradingLutSize`, `volumeProfile`, `useAdaptivePerformance`, `gpuResidentDrawerMode`, `storeActionsOptimization`. **Internal/read-only** (use `SerializedObject`): `m_MainLightRenderingMode`, `m_MainLightShadowsSupported`, `m_AdditionalLightsRenderingMode`, `m_AdditionalLightShadowsSupported`, `m_SoftShadowsSupported`, `m_SupportsLightCookies`, `m_SupportsLightLayers`, `m_UseFastSRGBLinearConversion`, `m_VolumeFrameworkUpdateMode`, `m_SupportDataDrivenLensFlare`, `m_SupportScreenSpaceLensFlare`, `m_EnableLODCrossFade`.
```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nex.Editor
{
    static class Hd2dRenderSettingsTool
    {
        const string assetPath = "Assets/URP/URPAsset.asset";
        const string rendererPath = "Assets/URP/URPAsset_Renderer.asset";

        [MenuItem("Nex/Billiard Rogue/Apply HD-2D URP Settings")]
        static void Apply()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            Undo.RecordObject(asset, "HD-2D URP settings");
            asset.supportsHDR = true;
            asset.hdrColorBufferPrecision = HDRColorBufferPrecision._32Bits;
            asset.msaaSampleCount = 1;
            asset.renderScale = 1f;
            asset.supportsCameraDepthTexture = false;
            asset.supportsCameraOpaqueTexture = false;
            asset.mainLightShadowmapResolution = 1024;
            asset.shadowCascadeCount = 1;
            asset.shadowDistance = 30f;
            asset.maxAdditionalLightsCount = 4;
            asset.useSRPBatcher = true;
            asset.colorGradingMode = ColorGradingMode.HighDynamicRange;

            var so = new SerializedObject(asset);
            so.FindProperty("m_SoftShadowsSupported").boolValue = false;
            so.FindProperty("m_SupportsLightCookies").boolValue = false;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            Undo.RecordObject(renderer, "HD-2D renderer settings");
            renderer.renderingMode = RenderingMode.Forward;
            renderer.intermediateTextureMode = IntermediateTextureMode.Auto;
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        // Mirrors ScriptableRendererDataEditor.AddComponent: sub-asset + m_RendererFeatures + m_RendererFeatureMap.
        public static T AddRendererFeature<T>(UniversalRendererData renderer) where T : ScriptableRendererFeature
        {
            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var so = new SerializedObject(renderer);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedProperties();
            renderer.SetDirty();                     // ScriptableRendererData.SetDirty(): rebuilds the renderer
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            return feature;
        }
    }
}
// Feature serialized refs afterwards: new SerializedObject(feature).FindProperty("shader").objectReferenceValue = Shader.Find("Hidden/BilliardRogue/TiltShift");
```
Camera components (World camera in a prefab built by script): `var data = camera.GetUniversalAdditionalCameraData(); data.renderType = CameraRenderType.Base; data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.None; data.requiresDepthOption = CameraOverrideOption.Off; data.volumeLayerMask = LayerMask.GetMask("WorldVolume"); data.renderShadows = true; data.stopNaN = false; data.dithering = false;` and `camera.allowHDR = true; camera.allowMSAA = false;`. For the starter Main Camera: `allowHDR = false`, `clearFlags = CameraClearFlags.SolidColor`, cull mask without World layers (edit `MainCoordinator.prefab` via `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`).

---

## 9. Risks, gotchas, checklist

1. **dual-blur package is dead under RG**: `BlurredTextureBackground.CustomRenderPass` overrides only `Configure/Execute` → no drawing + console warning each camera if the feature is ever added. `View.RequiresAdditionalBackgroundBlur` is currently a no-op because `cameraChains` has one entry. A blurred pause background needs an RG port (e.g. a tilt-shift-like pass with intensity 1) or a pre-blurred snapshot.
2. `CameraChainItem.SetTargetTexture` would clobber a World camera placed in `baseCameras`.
3. HDR on the asset also flips the UI stack to HDR unless Main Camera `allowHDR = false`.
4. Main+Root camera stack always renders through a 1080p intermediate + final blit (stack rule). Budget ~1 full-screen copy; optional optimisation later: make RootCamera a lone Base camera (changes starter structure — measure first).
5. GLES3: 16 visible lights total, ≤8 per object in Forward, no framebuffer fetch assumptions, B10G11R11 render support device-dependent.
6. Material properties set inside render funcs race (last write wins) — use per-draw MPBs (§4.4, §6).
7. `AddBlitPass` = unsafe pass; long chains of them prevent native pass merging.
8. Shader `#pragma` lists: omitting a keyword the pipeline enables means that feature is silently ignored by the shader (e.g. soft shadows, cookies) — keep §2.3 settings consistent with shader pragmas.
9. `PixelPerfectCamera` (URP 2D) only works with the 2D Renderer — not applicable to our 3D `UniversalRenderer`.
10. Volume stack: `VolumeManager.instance.stack` is the current camera's stack only during that camera's render; read custom volume components in `AddRenderPasses/RecordRenderGraph`, not in `Update`.
11. `m_VolumeFrameworkUpdateMode` EveryFrame costs CPU per camera; fine with 2–3 cameras.
12. Add DebugSettings toggles (guarded by `ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR`) for pixel scale (2/3/4), bloom, tilt-shift, shadows, to profile on device.

Unverified / to confirm on device: HDR buffer format actually chosen on the Playground GPU; exact GPU cost numbers in §5; custom-vertex-stream packing of `Custom1.x` into `TEXCOORD0.z`; shaders in §6–7 compile (written against 17.3 source, not yet compiled).
