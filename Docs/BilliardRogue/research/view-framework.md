# Research Note — UI Flow & View Framework (ViewManager / View / Keyboard Navigation)

Scope: `Starter/Assets/Scripts/Main/Views/**`, package `team.nex.keyboard-navigation@1.2.4`, `team.nex.dual-blur@0.0.7`
(camera chain / blur), `team.nex.debug-settings@1.1.8`, and the prefabs/scene that boot the UI flow.
All paths are relative to `Starter/` unless absolute. Everything below was read from source or prefab YAML (read-only),
except the items in §14 (Unverified).

---

## 0. TL;DR — facts implementation agents must know

1. **Namespace `Nex`, assembly `Assembly-CSharp`** (no asmdef under `Assets/Scripts`). `View.Manager`, `View.IsActive`,
   `View.ViewCamera`, `View.ViewOrder` are **`internal`**. Put Billiard Rogue views in `Assets/Scripts/**` with no asmdef,
   or they lose access.
2. **The secret code is NOT wired.** `ViewManager.Awake` looks for a `SecretCodeSequenceDetector` on its own GameObject, but
   no prefab/scene in `Assets/` has one (GUID `b2206e09222247ee93e55f263db7d0ec` is unreferenced). Add it via an Editor script (§12.6).
3. **Blur is not functional.** `MainViewManager.prefab` has one `CameraChainItem` (`configIndex: -1`) and no URP renderer uses
   the `BlurredTextureBackground` feature. `RequiresAdditionalBackgroundBlur == true` changes nothing except that the view
   below goes through `EnterBackground` (its `toBackgroundAnimator` runs). `PerformBlur`/`PerformUnblur` are never called.
4. **ViewManager SFX is partial.** It plays SFX only for the top-level **Back** button (`GenericExit`) and for opening Debug
   Settings (`GenericEnter`). Push/Pop/Replace play **no** SFX. Play enter SFX yourself, or put `MMF_SfxManager` in the entry animator.
5. **Screen analytics fire on every activation**, including when a view comes back to the top after a Pop. They do not fire
   inside a transaction, and do not fire when `AnalyticsScreenName` is `""`. UI actions still need explicit `AnalyticsManager.Instance.TrackEvent`.
6. **A second transition while one is running throws** `InvalidOperationException`. The throw goes into the returned UniTask,
   so `.Forget()` only logs it. Guard with `if (!IsActive) return;` or `if (Manager.IsInTransition) return;`.
7. **Deadlock risk.** An exception during Push/Pop/Replace (from a view's Present/Dismiss/animator, or from popping the root
   `EmptyView`) leaves `isInTransition == true` for good, and every later transition then throws.
8. **Time scale.** WelcomeScreenView's MMF feedbacks use **Scaled** time (`Timing.TimescaleMode: 0`), while DebugSettingsView and
   the control panel use Unscaled. If pause sets `Time.timeScale = 0`, every animator in the Pause/overlay views must be Unscaled.
9. **Legacy Input Manager only** (`activeInputHandler: 0`). `KeyboardNavigationController` takes the `#else` (legacy `Input`) branch:
   arrows (repeat 0.5 s then every 0.2 s), Return/KeypadEnter/JoystickButton0/Mouse2 → Enter, Escape → Back, mouse wheel → Up/Down/Left/Right.
10. **MainCoordinator.prefab brings its own cameras and a full-screen green `UIBackground` canvas.** Both will cover or double-render
    a 3D arena. Billiard Rogue must remove or replace them (§12.1).

---

## 1. File map

| Path | Type | Role |
|---|---|---|
| `Assets/Scripts/Main/Views/Framework/View.cs` | `abstract class View : MonoBehaviour` | Base view: lifecycle, identifiers, controls, key responder |
| `.../Framework/SimpleView.cs` | `abstract class SimpleView : View` | MMFeedbacks-driven present/dismiss/background, serialized `KeyResponder`, Back = PopSelf |
| `.../Framework/SimpleCanvasView.cs` | `[RequireComponent(typeof(Canvas))] abstract class SimpleCanvasView : SimpleView` | Binds canvas to view camera; plane distance ordering |
| `.../Framework/ViewManager.cs` | `class ViewManager : MonoBehaviour` | View stack, transitions, transactions, controls, keyboard routing, pause events |
| `.../Framework/AbstractViewState.cs` | `abstract class AbstractViewState : IDisposable` | Linked list of per-view restore state |
| `.../Framework/KeyboardNavigation/AbstractCustomViewKeyResponder.cs` | `abstract class AbstractCustomKeyResponder<TView> : KeyResponder where TView : SimpleCanvasView` (ns `Nex.KeyboardNavigation`) | Code-built responder bound to a view |
| `.../Framework/KeyboardNavigation/TopLevelControlProxyKeyResponder.cs` | `class TopLevelControlProxyKeyResponder : KeyResponder` | Delegates to a top-level control button's responder |
| `.../Framework/KeyboardNavigation/ViewManagerKeyboardNavigationContext.cs` | `class ViewManagerKeyboardNavigationContext : IKeyboardNavigationContext` | Context passed to `Activate` |
| `Assets/Scripts/Main/Views/EmptyView.cs` | `class EmptyView : View` | Root anchor (Identifier `Empty`, Controls `None`) |
| `Assets/Scripts/Main/Views/WelcomeScreenView.cs` | `class WelcomeScreenView : SimpleCanvasView` | Reference view |
| `Assets/Scripts/Main/Views/Debug/DebugSettingsView.cs` | `class DebugSettingsView : SimpleCanvasView` | Debug panel view |
| `Assets/Scripts/Main/Views/Controls/TopLevelControlPanel.cs` | `abstract class TopLevelControlPanel : MonoBehaviour` | Flags and animation of Back/Exit/Help/Debug |
| `.../Controls/MainTopLevelControlPanel.cs` | `class MainTopLevelControlPanel : TopLevelControlPanel` | Concrete 4-button panel |
| `.../Controls/DismissableControl.cs` | `class DismissableControl : MonoBehaviour` | Show/hide one control with MMFeedbacks |
| `Assets/Scripts/Main/Coordinator/MainCoordinator.cs` | `class MainCoordinator` | Flow orchestrator (pushes Welcome) |
| `Assets/Scripts/Examples/MainInitializerExample.cs` | boot script | Splash, Addressables load of coordinator |
| `Assets/Scripts/Utils/MMFeedbacksExtension.cs` | `Nex.Utils.MMFeedbacksExtension.PlayAsUniTask` | Await an MMFeedbacks |
| `Assets/Scripts/Feedbacks/MMF_SfxManager.cs` | `Nex.MMF.MMF_SfxManager : MMF_Feedback` | Play `SfxManager.SoundEffect` from a feedback |
| `Assets/Scripts/Editor/MMFeedbacks/MMFeedbacksEditorUtils.cs` | menu `Nex/MMFeedbacks/Convert Feedbacks to Unscaled Time` | Bulk timescale fix for selected objects |
| `Library/PackageCache/team.nex.keyboard-navigation@85c1b57f3096/Runtime/*` | ns `Nex.KeyboardNavigation` | KeyResponder family, controller, secret code |
| `Library/PackageCache/team.nex.dual-blur@9a1424175a42/Runtime/CameraChainItem.cs` | ns `Nex` | Camera chain and blur hooks |
| Prefabs | `Assets/Prefabs/Views/{WelcomeScreenView,DebugSettingsView}.prefab`, `Assets/Prefabs/Coordinators/{MainCoordinator,MainViewManager,MainTopLevelControlPanel}.prefab` | §10 |

---

## 2. `View` (abstract) — API

```csharp
public abstract class View : MonoBehaviour
{
    public enum ViewIdentifier { Invalid = -1, Empty, WelcomeScreen, DebugSettings }   // add entries here
    public abstract ViewIdentifier Identifier { get; }
    public virtual string AnalyticsScreenName => "";            // "" = no TrackScreen
    protected virtual void Awake() {}

    internal ViewManager Manager { get; set; }                  // null after the view is popped/replaced
    internal bool IsActive { get; set; }                        // true only while top AND not transitioning
    internal virtual Camera ViewCamera { get; set; }
    internal virtual int ViewOrder { get; set; }
    protected static float GetPlaneDistance(int viewOrder);     // 300 - 10 * viewOrder

    protected UniTask PushViewPrefab(View view, bool animate = true);    // discards the instance
    protected UniTask PushView(View view, bool animate = true);
    protected UniTask PopSelf(bool animate = true, bool keepAlive = false);

    public abstract UniTask Present(bool animate = true);
    public async UniTask PresentWithDuration(float duration, bool animate = true);   // waits (duration - PresentDuration) unscaled, then Present
    public abstract UniTask Dismiss(bool animate = true);
    public virtual UniTask EnterBackground(ViewIdentifier childViewIdentifier, bool animate = true);
    public virtual UniTask EnterForeground(ViewIdentifier childViewIdentifier, bool animate = true);
    public async UniTask EnterForegroundWithDuration(float duration, ViewIdentifier child, bool animate = true);
    public virtual UniTask PerformBlur(ViewIdentifier child, bool animate = true);     // never called in the starter (§4.6)
    public virtual UniTask PerformUnblur(ViewIdentifier child, bool animate = true);
    public async UniTask PerformUnblurWithDuration(float duration, ViewIdentifier child, bool animate = true);

    public virtual float PresentDuration => 0.5f;  DismissDuration => 0.5f;
    public virtual float BackgroundDuration => 0.5f; ForegroundDuration => 0.5f;
    public virtual float BlurDuration => 0f; UnblurDuration => 0f;

    public virtual void ViewDidBecomeTopView(bool afterPush);   // base: Debug.Log
    public virtual void ViewDidLoseTopView(bool dismissing);    // base: Debug.Log

    public abstract TopLevelControlPanel.ControlConfig Controls { get; }
    public virtual bool RequiresAdditionalBackgroundBlur => false;
    public virtual void OnBackButton() {}
    public virtual bool OnControlButton(TopLevelControlPanel.ButtonKind buttonKind) => false;  // return value ignored
    public virtual KeyResponder KeyResponder => default;
}
```

Notes
- `ViewIdentifier` is not serialized in any prefab, so adding entries is safe. Append new entries at the end anyway.
- `PushViewPrefab` (the protected wrapper) returns a plain `UniTask`, so you cannot configure the new instance. Prefer
  `var v = Instantiate(prefab); v.Initialize(...); await Manager.PushView(v);`.
- A duration only lengthens the delay. It never shortens an animation.

## 3. `SimpleView`, `SimpleCanvasView`, `EmptyView`

`SimpleView` serialized fields: `protected MMFeedbacks entryAnimator`, `protected MMFeedbacks toBackgroundAnimator` (may be null),
`[Header("Keyboard Navigation")] KeyResponder keyResponder` (private).
- `Present` → `entryAnimator.PlayAsUniTask(animate)`. `Dismiss` plays the same animator reversed.
- `EnterBackground` → `toBackgroundAnimator.PlayAsUniTask(animate)`. `EnterForeground` plays it reversed. Nothing happens if it is null.
- `KeyResponder => keyResponder` (the serialized field). You can override it again in a subclass.
- `OnBackButton()` → `if (!IsActive) return; PopSelf().Forget();`. **Views that must not pop on Back (Title, Gameplay) must override it.**
- `PlayAsUniTask(animate:false)` runs the feedbacks at `DurationMultiplier = 0.001` (near instant, but still async).

`SimpleCanvasView` has field `protected Canvas canvas` (cached in `Awake`, so overrides must call `base.Awake()`) and
`[SerializeField] protected bool disableCanvasOnBackground`.
- `ViewCamera` setter forces `canvas.renderMode = ScreenSpaceCamera` and `canvas.worldCamera = camera`. The prefab's render mode is overwritten.
- `ViewOrder` setter sets `canvas.planeDistance = 300 - 10*order` when a camera is set. It sets `canvas.sortingOrder = order` only with no camera, which never happens here.
  **Ordering between views comes from plane distance only.** Keep `sortingOrder = 0` in view prefabs. DebugSettingsView uses 1000, so it always wins.
  At a stack depth of 30 the plane distance reaches 0 (near clip 0.3).
- `EnterBackground`: plays the base animation, then disables the canvas if `disableCanvasOnBackground`. `EnterForeground` enables it first.
  Use `disableCanvasOnBackground = true` on full-screen views that sit under Gameplay (e.g. Title) to save fill rate.

`EmptyView : View`: Identifier `Empty`, Controls `None`, instant Present/Dismiss. ViewManager adds one to its own GameObject as the
permanent stack root, and you can use it as a no-UI anchor.

---

## 4. `ViewManager`

### 4.1 Serialized fields and public API
```csharp
[SerializeField] CameraChainItem[] cameraChains;         // MainViewManager: [RootCamera]
[SerializeField] int rootViewOrder;                      // 0
[SerializeField] TopLevelControlPanel controlPanel;      // nested MainTopLevelControlPanel
[SerializeField] DebugSettingsView debugSettingsPrefab;  // Assets/Prefabs/Views/DebugSettingsView.prefab

public event UnityAction? PauseViewHomeClicked, PauseViewResumeClicked, Paused;
public View TopView { get; }                       // viewStack.Peek().view
public View.ViewIdentifier TopViewIdentifier { get; }
public TopLevelControlPanel ControlPanel { get; }
public bool IsInTransition { get; }

public async UniTask<T> PushViewPrefab<T>(T viewPrefab, bool animate = true) where T : View; // Instantiate + PushView, returns instance
public async UniTask PushView(View view, bool animate = true, bool skipScreenAnalytics = false);
public async UniTask PopView(bool animate = true, bool keepPoppedViewAlive = false);
public async UniTask<View> ReplaceView(View replacementView, bool animate = true, bool keepPoppedViewAlive = false); // returns the replaced view
public Transaction CreateTransaction();            // IDisposable
public static void SuspendKeyboardNavigation();    // counter ++ (static, global)
public static void ResumeKeyboardNavigation();     // counter --
public void AnnouncePauseViewHomeClicked(); public void AnnouncePauseViewResumeClicked(); public void AnnouncePaused();
```
ViewManager is **not** a singleton. Reach it from a coordinator's serialized ref, or from inside a view via `Manager`.

### 4.2 `Awake`
1. Subscribes `controlPanel.OnButton += HandleTopLevelButton`.
2. `AddComponent<KeyboardNavigationController>()` on its own GameObject, with `OnKey += HandleKey`.
3. `AddComponent<EmptyView>()` and pushes it as the root (`hierarchyLevel 0`). The stack is never empty.
4. In debug builds (`ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR`): `TryGetComponent<SecretCodeSequenceDetector>` → `detector.AddListener(0, OpenDebugSettings)`.

### 4.3 Transition semantics (exact order)

**PushView(view)**
1. Throws if `isInTransition`. Sets `view.Manager = this`, `controlPanel.Interactable = false`, `isInTransition = true`.
2. `DeactivateView(oldTop, dismissing:false)` runs **immediately**: `oldTop.ViewDidLoseTopView(false)`, deactivates its key responder, `IsActive = false`.
3. Hierarchy level = previous level + 1 only if `RequiresAdditionalBackgroundBlur` **and** another chain exists. It never increases here.
4. `view.ViewCamera = cameraChains[level].GetCamera()` (RootCamera). `view.ViewOrder = rootViewOrder + stack.Count` (first real view = 1, plane 290). The view is pushed.
5. `duration = animate ? max(view.PresentDuration, oldTop.BackgroundDuration) : 0`.
6. `await WhenAll(oldTop.EnterBackground(view.Identifier), view.PresentWithDuration(duration), controlPanel.ConfigureControls(Compute(view), duration))`.
7. `isInTransition = false`, `Interactable = true`, then (outside a transaction) `ActivateView(view, afterPush:true, skipScreenAnalytics)`.

**PopView()**
1. Throws if in transition. Pops the top. `DeactivateView(prev, dismissing:true)` sets `prev.Manager = null`.
2. `duration = max(prev.DismissDuration, newTop.ForegroundDuration)`, then `await WhenAll(prev.Dismiss(), newTop.EnterForegroundWithDuration(...), ConfigureControls(newTop))`.
3. `ActivateView(newTop, afterPush:false)` fires **TrackScreen again**, then `Destroy(prev.gameObject)` unless `keepPoppedViewAlive`.
4. **Popping when only `EmptyView` is left** throws inside `Peek()` after `isInTransition = true`. That is a permanent deadlock.

**ReplaceView(v)**
1. Pops the top, runs `DeactivateView(prev, true)`, and gives `v` the same order and camera level.
2. The parent view below is **not** touched at the same level (it stays in background).
3. `await WhenAll(prev.Dismiss(), v.PresentWithDuration(max(prev.DismissDuration, v.PresentDuration)), ConfigureControls(v))`.
4. `ActivateView(v, afterPush:false)`. **`afterPush` is false after a replace.** Don't use it to detect first activation.
5. Destroys `prev` (end of frame) unless `keepPoppedViewAlive`, and returns `prev`.

**ActivateView(view)**: `IsActive = true`. If `view.KeyResponder != null`, it sets `EnableHighlighting = PlayerDataManager.Instance.appViewState.enableHighlighting`,
calls `Activate(context, isFocused:true)`, and remembers the responder as active. It calls `AnalyticsManager.Instance.TrackScreen(AnalyticsScreenName)`
when the name is non-empty and not skipped, then `view.ViewDidBecomeTopView(afterPush)`.
ViewManager needs **`PlayerDataManager` and `AnalyticsManager` singletons** (in CommonSingletons / StartUpSingletons).

### 4.4 Transactions
```csharp
using (viewManager.CreateTransaction())
{
    await viewManager.PopView(animate: false);            // intermediate views are NOT activated
    await viewManager.ReplaceView(CreateGameplayView());  // no TrackScreen / ViewDidBecomeTopView / key activation inside
}   // Dispose → count 0 → ActivateView(TopView, lastViewChangeWasPush) exactly once
```
- **Await every call inside the `using`.** Push and Replace put the new view on the stack before their first await. If the
  transaction is disposed before a call completes, the view is activated mid-transition and then activated a second time.
- `DeactivateView` still runs for every view that loses the top inside a transaction. `ViewDidLoseTopView` can therefore arrive without a matching
  `ViewDidBecomeTopView`, so make it idempotent.
- Transactions nest (counter).

### 4.5 Keyboard routing (`HandleKey`)
Keys are ignored while `isInTransition`, with no active responder, when `CherryIntegrationManager.Instance.IsKeyboardControlDisabled()`,
or when the suspend counter is above 0.
- `Escape` → `activeKeyResponder.HandleBack()`.
- `Enter` → `EnableHighlightingIfNeeded()` then `HandleEnter()`.
- Arrows → `EnableHighlightingIfNeeded()` then `HandleNavigation(Up/Down/Left/Right)`. The result is ignored.
- `Update()`: raw `Input.GetKeyDown(KeyCode.Period)` toggles `appViewState.enableHighlighting` (a debug toggle that also ships in production).
- `appViewState` is `PlayerDataManager.AppViewState : AbstractViewState` with `bool enableHighlighting = true`. It lives in memory only and is not saved with ES3.

### 4.6 Cameras, plane distance, blur
- `CameraChainItem` (`Nex`, dual-blur) has fields `Camera[] baseCameras` and `BackgroundConfig{configIndex, screenWidth, screenHeight, colorFormat, depthStencilFormat}`.
  API: `Camera GetCamera()`, `UniTask Activate(CameraChainItem parent, float duration = 0.5f, CancellationToken)`, `UniTask Deactivate(float, CancellationToken)`.
  Activation renders the parent chain (its camera plus its `baseCameras`) into a RenderTexture that a `BlurredTextureBackground`
  (`ScriptableRendererFeature`, settings `configIndex, numPasses 2..5, blur`) draws blurred.
- Starter: a single chain item, `RootCamera` (Overlay, orthographic size 5, at world position y = 10000, culling mask **Everything**, `configIndex: -1`, 1920x1080).
  `baseCameras = [Main Camera]` is set by the MainCoordinator override. No renderer has the blur feature, so **blur is off**.
- Recommendation: **do not enable blur** on the low-end GPU (a full-screen RT copy plus two down/up passes on top of HD-2D post).
  Give overlays a full-screen dim `Image`, as DebugSettingsView does with `Background` (black, alpha 0.333).

### 4.7 Top-level buttons (`HandleTopLevelButton`, ignored during transition)
- `DebugSettings` → `OpenDebugSettings()`: plays `SfxManager.SoundEffect.GenericEnter`, then `PushViewPrefab(debugSettingsPrefab).Forget()`.
  It has **no guard** against being in a transition or already showing Debug Settings.
- `Back` → plays `GenericExit`, then `TopView.OnBackButton()`.
- `Exit` / `Help` → `TopView.OnControlButton(kind)`.
- `ComputeControlConfig(view)` = `view.Controls`, plus `| DebugSettings` in debug builds unless the view is DebugSettings.

### 4.8 Pause announcements
Pause announcements are plain relays with no built-in pause view, no time-scale change and no analytics.
Gameplay subscribes to `Paused` / `PauseViewResumeClicked` / `PauseViewHomeClicked`, and a PauseView calls `Announce*()` (§12.4).

---

## 5. `AbstractViewState`
State you want to restore across scene reloads, chained like a navigation stack. Members:
`abstract View.ViewIdentifier ViewIdentifier`; `protected CancellationToken CancellationToken` (cancelled on Dispose);
`AbstractViewState NextViewState {get; set;}` (setting it disposes the old one); `bool HasValidNextViewStateOrClear(id)`, plus 2- and 3-id
overloads that return the matching id or `Invalid` and clear the link on mismatch; `virtual void Dispose()`.
The only use is `PlayerDataManager.appViewState` (identifier `Empty`). Billiard Rogue persists runs through PlayerDataManager, so it doesn't need this.

---

## 6. Top-level controls

```csharp
public abstract class TopLevelControlPanel : MonoBehaviour {
    [Flags] public enum ControlConfig { None = 0, Back = 1<<0, Exit = 1<<1, Help = 1<<2, DebugSettings = 1<<31 }
    public enum ButtonKind { Back = 0, Exit = 1, Help = 2, DebugSettings = 31 }
    public event UnityAction<ButtonKind>? OnButton;
    protected struct DismissableButton { public DismissableControl control; public Button button; public KeyResponder keyResponder; }
    public virtual bool Interactable { get; set; }            // false during transitions
    [SerializeField] ControlConfig supportedControls;         // prefab: -1 (all)
    public async UniTask ConfigureControls(ControlConfig newConfig, float duration, bool animate = true);
    protected abstract DismissableControl? GetDismissableControl(ControlConfig id);
    protected abstract KeyResponder? GetKeyResponder(ControlConfig control);
    public KeyResponder? GetActiveKeyResponder(ControlConfig control);   // null unless that control is currently shown
    protected void InvokeButtonHandler(ButtonKind kind);
}
```
- `ConfigureControls` masks the config with `supportedControls`, XORs it against the current one, and calls `Present(extraDelay)` or `Dismiss()` on each changed control.
  `currControlConfig` is updated only after the animations finish.
- `MainTopLevelControlPanel`: fields `backButton, exitButton, helpButton, debugSettingsButton` (`DismissableButton`). `Awake` wires `button.onClick`,
  and each handler returns early when `!Interactable`. In debug builds it re-activates the debug button GameObject. `GetKeyResponder(DebugSettings)`
  returns null, so the debug button can't be reached by keyboard (use the secret code).
- `DismissableControl` (`[SerializeField] MMFeedbacks entryAnimator`): `Awake` → `SetActive(false)`. `Present(delay)` → SetActive(true), then
  `UniTask.Delay(delay)` (**scaled time**, only when delay > 0, i.e. when a transition duration is over 0.5 s), then play. `Dismiss` plays reversed, then SetActive(false).
- **The starter's buttons are invisible**. Each is a `Button` with an npuikit `RaycastTarget` graphic, 150x75, no child visuals, sliding 150 px in from off-screen:
  Back at top-left, Help at bottom-left, Exit at top-right, Debug at top-right under Exit. Billiard Rogue needs visible Back/Exit icons as children of these buttons.
- WelcomeScreenView declares `Controls = Exit` but does **not** override `OnControlButton`, so its top-level Exit does nothing.
  Its exit runs through a hidden `exitButton` that Escape triggers (§10.1). Handle every control you declare.

---

## 7. Keyboard navigation (`Nex.KeyboardNavigation`)

### 7.1 `KeyboardNavigationController : UIBehaviour`
`enum Key { Escape, Enter, Up, Down, Left, Right }`, `event UnityAction<Key>? OnKey`,
`[SerializeField, Min(0)] float firstRepeatDelay = 0.5f, repeatInterval = 0.2f` (properties `FirstRepeatDelay`/`RepeatInterval`), `bool IsInteractable`
(false when an ancestor CanvasGroup is non-interactable). Arrow keys go through `KeyStateMachine`, which uses unscaled time and auto-repeats.
With the legacy input handler: Escape → Escape; Return/KeypadEnter/JoystickButton0/Mouse2 → Enter; mouse wheel y/x → Up/Down/Right/Left.
The ViewManager's controller is added at runtime, so its tunables are the defaults and are only reachable through `GetComponent`.

### 7.2 `KeyResponder : UIBehaviour`
`[RequireComponent(typeof(RectTransform))] [DisallowMultipleComponent]`, so there is **one responder per GameObject**.
```csharp
public enum NavigationKey { None = 0, Left = 0b010, Right = 0b011, Up = 0b100, Down = 0b101 }
public readonly struct NavigationResult { bool IsDone /*TargetArea != null*/; RectTransform? TargetArea; NavigationKey Key;
    NavigationResult(RectTransform? targetArea) /*consumed*/; NavigationResult(NavigationKey key) /*not handled, bubble*/ }
public abstract NavigationResult HandleNavigation(NavigationKey key);
public abstract bool HandleEnter();  public abstract bool HandleBack();
public RectTransform? GainFocus(NavigationKey key = None);  public virtual void LoseFocus();
protected virtual RectTransform? OnFocusRequested(NavigationKey key = None);   // base applies gain-focus tweets, returns RectTransform
public virtual bool Activate(IKeyboardNavigationContext? context, bool isFocused); public virtual void Deactivate(IKeyboardNavigationContext? context);
public virtual bool EnableHighlighting { get; set; }        // default true
public bool Focused, Activated /*activated && isActiveAndEnabled*/, IsActivatedAndFocused; public RectTransform? LastFocusedTransform;
public event UnityAction<KeyResponder, RectTransform?> FocusChanged;   public event UnityAction<KeyResponder, bool> ActivationChanged;
protected RectTransform RectTransform { get; }
// Serialized "Tweets" (focus highlight, unscaled, crossFadeDuration = 0.25):
protected GraphicTweet[] graphicTweets;          // {Graphic graphic; Color gainFocusColor; Color loseFocusColor}
protected ImageFillTweet[] imageFillTweets;      // image fill amount
protected CanvasGroupTweet[] canvasGroupTweets;  // {CanvasGroup canvasGroup; float gainFocusAlpha; float loseFocusAlpha}
protected TransformScaleTweet[] transformScaleTweets; // {Transform transform; Vector3 gainFocusScale, loseFocusScale; AnimationCurve animationCurve} (curve must have keys)
[SerializeField] FocusChangedEvent? focusChanged;    // UnityEvent<KeyResponder, RectTransform?>
```
Tweets back up the original values on `Activate` and restore them on `Deactivate`. They only apply while `EnableHighlighting` is true.

### 7.3 Responder catalog

| Class | Key serialized fields | Behaviour |
|---|---|---|
| `ButtonKeyResponder` | `Button button` | Enter → `button.onClick.Invoke()` (**ignores `interactable`**). `Activate` fails if `!button.isActiveAndEnabled`. Nav/back not handled |
| `ToggleKeyResponder` | `Toggle toggle` | Enter toggles |
| `GroupKeyResponder : AbstractGroupKeyResponder` | `Axis axis` (private enum `{Horizontal=0, Vertical=1}`) | Linear list, clamps at the ends (no wrap). Arrows off-axis bubble |
| `AbstractGroupKeyResponder` (base) | `int initialActiveIndex; bool resetOnActivate; bool resetOnFocus = true; bool keepLastAcrossScene; bool fetchChildResponders; List<KeyResponder> responders; SelectionFallbackStrategy selectionFallback = Next` | `fetchChildResponders` collects **direct, active children** at Awake. Public API: `SelectedIndex`, `SetInitialActiveIndex(int)`, `ReinitializeResponders(IEnumerable<KeyResponder>)`, `ReinitializeWithChildResponders()`, `NavigateTo(KeyResponder?, key)`, IList `Add/Insert/Remove/Clear` |
| `GridKeyResponder` | `int numColumns; WrapBehavior edgeBehavior` | 2D grid over the active responders |
| `GraphKeyResponder : HierarchicalKeyResponder` | `KeyResponder? backButtonResponder; KeyResponderGraphNode? initialGraphNode; bool resetOnActivate, resetOnFocus, keepLastAcrossScene, fetchChildNodes; List<KeyResponderGraphNode> graphNodes` | Explicit neighbour graph. **Back → `backButtonResponder.HandleEnter()`**, otherwise the selection's HandleBack. `fetchChildNodes` collects direct active children that have `KeyResponderGraphNode`. API: `NavigateTo(KeyResponderGraphNode?)`, `SetInitialGraphNode(...)` |
| `KeyResponderGraphNode : MonoBehaviour` | `KeyResponder keyResponder; up/down/left/rightNeighbour; fallbackNode` | A node can point at a responder anywhere in the hierarchy. An inactive target falls back to `fallbackNode` |
| `TableKeyResponder`, `ScrollRectResponder`, `SimpleScrollRectResponder`, `SwitchKeyResponder`, `CollapsibleKeyResponder`, `PrevNextButtonKeyResponder`, `FirstDelegateKeyResponder`, `ProxyKeyResponder`, `TMPInputFieldKeyResponder`, `TMPDropdownKeyResponder` | — | Specialised helpers (not used by the starter views) |
| `TopLevelControlProxyKeyResponder` (starter) | `TopLevelControlPanel.ControlConfig control` | On `Activate` it looks up `controlPanel.GetActiveKeyResponder(control)` and delegates to it. **It fails to activate when that control is not currently shown** |
| `AbstractCustomKeyResponder<TView>` (starter) | — (`protected TView host`) | `static Create<TConcrete>(TView host)` → `host.gameObject.AddComponent<TConcrete>()`, then sets `host` |

### 7.4 How a Button becomes navigable (checklist)
1. The Button has `navigation.mode = None` and `transition = None` (as in WelcomeScreenView). That keeps EventSystem navigation
   (`StandaloneInputModule`, `sendNavigationEvents: 1`) from double-handling arrows and Submit after a mouse click selects it.
2. Add a `ButtonKeyResponder` whose `button` is that Button, usually on the same GameObject, with tweets for the focus look.
3. Register it in a container responder. Either make it a direct child of a `GroupKeyResponder` with `fetchChildResponders`, or list it in `responders`,
   or point a `KeyResponderGraphNode` (a direct child of a `GraphKeyResponder` with `fetchChildNodes`) at it.
4. Assign the root container to the view's `keyResponder` field, or override `KeyResponder`.
5. The handler starts with `if (!IsActive) return;`. Enter reaches it through `onClick.Invoke()` even when the Button is non-interactable, so check for disabled states yourself.
6. Responders added after Awake need `ReinitializeWithChildResponders()` or `ReinitializeResponders(list)` before the view is activated.

### 7.5 Back via the top-level panel (canonical pattern, from DebugSettingsView)
Root `GraphKeyResponder.backButtonResponder` = a `TopLevelControlProxyKeyResponder{control = Back}` that is **also a graph node child**, so it gets activated.
Escape then runs proxy → panel Back `ButtonKeyResponder` → `MainTopLevelControlPanel.HandleBackButton` → ViewManager (plays `GenericExit`) → `TopView.OnBackButton()`.
Requirement: the view's `Controls` includes `Back`. A plain `ButtonKeyResponder` also works as `backButtonResponder` without being a node, because its `HandleEnter` doesn't check activation. That is what WelcomeScreenView does.

### 7.6 Suspending
`ViewManager.SuspendKeyboardNavigation()` / `ResumeKeyboardNavigation()` use a static counter, so keep them balanced (for example with try/finally).
`CherryIntegrationManager` drives them from the platform's keyboard-disabled property.

---

## 8. `SecretCodeSequenceDetector` (package, `[AddComponentMenu("Nex/Keyboard Navigation/Secret Code Sequence Detector")]`)
```csharp
[Serializable] class SequenceConfig { [SerializeField] KeyboardNavigationController.Key[] sequence; public UnityEvent onTriggered; } // private nested
[SerializeField] SequenceConfig[] configs;
[SerializeField] KeyboardNavigationController? keyboardController;   // null → GetComponent<KeyboardNavigationController>() in Start
public void AddListener(int configIndex, UnityAction action);
```
- It uses KMP-style prefix matching, so overlapping input works, and it fires every time the sequence completes. It subscribes in `Start`,
  which works because ViewManager adds its controller in `Awake`.
- It receives **every** key: during transitions, while suspended, and at the same moment the active view navigates. So "Up Up Down Down Left Right Left Right"
  also moves focus in the current menu.
- **Current state: not present on any prefab.** If it is added with an empty `configs` array, `ViewManager.Awake` throws `IndexOutOfRangeException` on `configs[0]`.
- ViewManager binds `configs[0]` → `OpenDebugSettings` (debug builds only). Recommended hardening (a small starter edit):
  `if (isInTransition || TopViewIdentifier == View.ViewIdentifier.DebugSettings) return;` at the top of `OpenDebugSettings`.

## 9. Debug Settings
- `DebugSettingsView : SimpleCanvasView`: Identifier `DebugSettings`, Controls `Back`, screen name `"debug-settings"`, `[SerializeField] DebugSettingsPanel debugSettingsPanel`.
  In `Awake` it calls `debugSettingsPanel.Initialize(save: SaveDebugSettings + SavePlayerPreference, close: () => PopSelf())` and
  `PopulateRows(PlayerDataManager.Instance.DebugSettings, 0)`.
- `Nex.Dev.DebugSettingsPanel` (package) builds rows by reflection over the **public fields, readable properties and methods** of `Nex.DebugSettings`
  (`Assets/Scripts/PlayerData/DebugSettings.cs`). Supported types: bool, enum, numeric types, string, DateTime.
  Attributes in `Nex.Dev.Attributes`: `VisibilityLevel(int)`, `Description`, `IntChoices`, `StringChoices`, `NumericSteps{Steps,Min,Max}`,
  `SaveBeforeInvoking`, `DebugOrder(int)`, `CopyableTextArea(rows)`, `DebugContentWidth`, `DateTimeTicks`.
  **Save** writes the rows back and pops. **Back pops without saving.** Method rows close first (saving if marked `SaveBeforeInvoking`), then invoke.
- Opening it: in debug builds every view gets the (invisible) top-right Debug button, plus the secret code once it is wired.
  In release both the flag and the button are compiled out. The prefab is still referenced, and so still built.
- Billiard Rogue tunables (strike threshold, auto-aim bot, and so on) are added as public fields on `DebugSettings`. The view picks them up automatically.

---

## 10. Prefab hierarchies (read from YAML)

### 10.1 `Assets/Prefabs/Views/WelcomeScreenView.prefab` (layer UI=5 throughout)
```
WelcomeScreenView  [RectTransform, Canvas(renderMode=ScreenSpaceCamera, planeDistance 100 (overridden), sortingOrder 0,
                    additionalShaderChannels 27), CanvasScaler(ScaleWithScreenSize 1920x1080, match 0 = width),
                    GraphicRaycaster, WelcomeScreenView{entryAnimator→EntryAnimator, toBackgroundAnimator→BackgroundAnimator,
                    keyResponder→KeyResponder(Graph), disableCanvasOnBackground 0, startARGameButton, startNonARGameButton,
                    exitButton→KeyResponder/ExitButtonResponder.Button, versionLabel}]
├─ Animators
│  ├─ EntryAnimator       [MMF_Player(PlayerTimescaleMode Unscaled) + MMF_CanvasGroup(Target=UI.CanvasGroup, OverTime 0.5 s,
│  │                        AlphaCurve linear 0→1, Remap 0→1, Timing.TimescaleMode = Scaled(!))]
│  └─ BackgroundAnimator  [MMF_Player + MMF_CanvasGroup(same target, Remap 1→0 → fades fully out)]
├─ KeyResponder           [GraphKeyResponder{backButtonResponder→ExitButtonResponder.ButtonKeyResponder,
│  │                        initialGraphNode→PlayARButtonResponder, fetchChildNodes 1, resetOnActivate/Focus 0}]
│  ├─ ExitButtonResponder [Button(nav Automatic, no graphic, invisible) + ButtonKeyResponder]  ← not a node, back target only
│  ├─ PlayARButtonResponder    [KeyResponderGraphNode{keyResponder→UI/StartARGameButton.ButtonKeyResponder, down→PlayNonAR}]
│  └─ PlayNonARButtonResponder [KeyResponderGraphNode{keyResponder→UI/StartNonARGameButton.ButtonKeyResponder, up→PlayAR}]
└─ UI                     [stretch, CanvasRenderer, CanvasGroup(alpha 1)]
   ├─ Title               [TextMeshProUGUI "Welcome!" (font Assets/Fonts/BrainParty/Play Chickens SDF), NexLocalizedString]
   ├─ StartARGameButton   [Image + RoundedRectModifier, Button(nav None, transition None, target Image),
   │  │                    ButtonKeyResponder{graphicTweets: Image white→(1,0.995,0.193) yellow; transformScaleTweets: self 1→1.2, EaseInOut},
   │  │                    HorizontalLayoutGroup, ContentSizeFitter(preferred), LayoutElement(min 200x100)]  pos (0,-160)
   │  └─ Text             [TMP 50pt + NexLocalizedString]
   ├─ StartNonARGameButton  (same as above, pos (0,-320))
   └─ VersionLabel        [TMP "Version: {0}" + NexLocalizedString (smart arg "0" = AppInfo version)]
```
Escape → `GraphKeyResponder.HandleBack` → hidden exit `ButtonKeyResponder.HandleEnter` → `exitButton.onClick` → `OnExitButton` → quits.

### 10.2 `Assets/Prefabs/Views/DebugSettingsView.prefab` (layer Default=0)
```
DebugSettingsView [Canvas(ScreenSpaceCamera, sortingOrder 1000), CanvasScaler(ref 812x375!), GraphicRaycaster,
                   DebugSettingsView{entryAnimator→EntryAnimator, toBackgroundAnimator none, keyResponder→KeyResponder,
                   debugSettingsPanel→DebugSettingsPanel}, CanvasGroup]
├─ Animators/EntryAnimator [MMF_Player + MMF_CanvasGroup (Timing Unscaled)]
├─ KeyResponder   [GraphKeyResponder{backButtonResponder→BackKeyResponder proxy, initialGraphNode→PanelGraphNode, fetchChildNodes 1}]
│  ├─ BackKeyResponder [TopLevelControlProxyKeyResponder{control = Back(1)} + KeyResponderGraphNode(no neighbours)]
│  └─ PanelGraphNode   [KeyResponderGraphNode{keyResponder→DebugSettingsPanel's DynamicGroupKeyResponder}]
├─ Background     [Image black a=0.333, stretch]   ← dim-overlay pattern
└─ DebugSettingsPanel  (nested prefab Packages/team.nex.debug-settings/Prefabs/DebugSettingsPanel.prefab, inset 80/50 px)
```

### 10.3 `Assets/Prefabs/Coordinators/MainViewManager.prefab`
```
MainViewManager [Transform, ViewManager{cameraChains=[RootCamera.CameraChainItem], rootViewOrder 0,
                 controlPanel→MainTopLevelControlPanel, debugSettingsPrefab→DebugSettingsView.prefab}]
                 (no SecretCodeSequenceDetector; KeyboardNavigationController + EmptyView added at runtime)
├─ CameraChain  (pos y = 10000)
│  └─ RootCamera [Camera(orthographic, size 5, depth 0, clear Nothing, cullingMask Everything), URP AdditionalCameraData(renderType Overlay,
│                 postProcessing off), CameraChainItem{baseCameras [] (overridden to [Main Camera] in MainCoordinator), configIndex -1, 1920x1080}]
└─ MainTopLevelControlPanel (nested prefab)
```

### 10.4 `Assets/Prefabs/Coordinators/MainTopLevelControlPanel.prefab` (layer 0)
```
MainTopLevelControlPanel [Canvas(ScreenSpaceOverlay (renders above all camera canvases), sortingOrder 0), CanvasScaler(1920x1080, match 0),
                          GraphicRaycaster, MainTopLevelControlPanel{supportedControls -1, back/exit/help/debugSettingsButton}]
├─ BackButton          anchor TL, 150x75 [DismissableControl{entryAnimator}, MMF_Player(MMF_Position, anchored slide ±150, 0.5 s, Unscaled),
│                                        Button(nav None, target RaycastTarget), ButtonKeyResponder, CanvasRenderer, RaycastTarget]
├─ HelpButton          anchor BL (same components)
├─ ExitButton          anchor TR
└─ DebugSettingsButton anchor TR, y -75
```

### 10.5 `Assets/Prefabs/Coordinators/MainCoordinator.prefab` (Addressable, GUID `7d131650af90f4b52a0de40ce3d7b71f`, Default Local Group)
```
MainCoordinator [MainCoordinator{viewManager→MainViewManager.ViewManager, welcomeScreenViewPrefab→WelcomeScreenView.prefab}]
├─ Main Camera     [Camera(orthographic 5, depth -1, clear Skybox, cullingMask Everything), AudioListener,
│                   URP AdditionalCameraData(Base, cameraStack=[RootCamera])]
├─ SingletonSpawner [configs=[Common → CommonSingletons.prefab], activatePostSpawn []]   (no-op when already spawned; self-destroys)
├─ UIBackground    [Canvas(ScreenSpaceCamera, camera=RootCamera, planeDistance 300 → behind all views), CanvasScaler 1920x1080]
│  └─ Image        [full-screen opaque green (0, 0.42, 0.18)]
└─ MainViewManager (nested prefab; override CameraChainItem.baseCameras=[Main Camera])
```

---

## 11. `Assets/Scenes/Examples/GameUIExample.unity` boot

Scene roots: **`SingletonSpawner`** (configs StartUp → `StartUpSingletons.prefab`, Common → `CommonSingletons.prefab`;
activatePostSpawn = [MainInitializerExample]), **`MainInitializerExample`** (inactive; `nexSplashScreenReference`, `mainCoordinatorReference` = the
MainCoordinator Addressable, `debugShowSplash` false), and **`EventSystem`** (EventSystem + `StandaloneInputModule`). **There are no cameras in the scene**;
they come from the MainCoordinator prefab. The scene is Build Settings index 0, and `GameConfigsManager.mainScene = "GameUIExample"`.

Sequence:
1. `SingletonSpawner.Awake` spawns missing singletons through `AssetReferenceGameObject.InstantiateAsync` into DontDestroyOnLoad
   (StartUp: Analytics, Application, ScreenBlocker; Common: Sfx, Bgm, Volume, Vfx, PlayerData, CherryIntegration, GameConfigs, DebugPrinter).
   It then activates `MainInitializerExample` and destroys itself.
2. `MainInitializerExample.Start`: on the first app start in a player build it plays the splash. In the editor it skips it.
3. `LoadMainCoordinator()` → Addressables `InstantiateAsync`. That instantiates the coordinator, and `ViewManager.Awake` runs.
4. `coordinator.Initialize()` resolves at once, because `OnEnable` already set `prepared`.
5. Wait 0.1 s, then `ScreenBlockerManager.Instance.Hide()`.
6. `coordinator.StartMain()` → `viewManager.PushView(CreateWelcomeScreenView())`. `CreateWelcomeScreenView` calls `Instantiate`, `Initialize()`, and subscribes the events.
7. Welcome's buttons call `SceneManager.LoadScene(GameConfigsManager.Instance.ARGameScene / NonARGameScene)`. That is a single-mode load, so it **destroys the coordinator and ViewManager**.
   The example game scenes have no ViewManager and use raw `Input.GetKeyDown(Escape)` to return.

---

## 12. How Billiard Rogue should use this

### 12.1 Architecture decisions
- **Single main scene, every step a View** (GDD §9). Don't `SceneManager.LoadScene` into gameplay: that destroys the ViewManager.
  Gameplay is `GameplayView : SimpleCanvasView` (HUD). It instantiates the arena prefab (3D diorama, balls, enemies) in `Initialize`,
  owns it, and destroys it in `OnDestroy`.
- Create `BilliardRogueCoordinator` (copy the `MainCoordinator` pattern, don't reuse the Example class) plus a coordinator prefab containing
  a nested `MainViewManager`, or a variant of it. Copy the `MainInitializerExample` boot into a `BilliardRogueInitializer`. The coordinator
  reference can be a direct prefab ref, or an Addressable. Addressable entries must be created through `AddressableAssetSettings` in an Editor script.
- **Cameras**: the gameplay camera (HD-2D, perspective) must be the single **Base** camera, with `RootCamera` (overlay, UI) in its
  `cameraStack`. Either repurpose `Main Camera`, or remove it and add RootCamera to the arena camera's stack. Also:
  - Remove `UIBackground`. It is an opaque full-screen canvas on the overlay camera and would hide the arena.
  - Set the RootCamera culling mask to `UI` only.
  - Two base cameras rendering the world would double the GPU cost.
  - URP camera stacking shares the base camera's render target and render scale. If pixelation is done with a low render scale, the UI is pixelated too. See the rendering note.
- **Views raise C# events; the coordinator owns the flow**, as in WelcomeScreenView → MainCoordinator. Views may still `PopSelf()` for local overlays (Pause/Reward/StageIntro).

Proposed identifiers (append to `View.ViewIdentifier`):
`Title, PlayerMode, Calibration, Gameplay, StageIntro, Reward, Pause, TrackingLost, Summary, Settings`.

| GDD step | Class | Transition | Controls | Screen name |
|---|---|---|---|---|
| Title | `TitleView` (vertical list) | root Push | `Exit` (handle it!) | `title` |
| Player mode | `PlayerModeView` (vertical/horizontal list) | Push | `Back` | `player-mode` |
| Calibration | `CalibrationView` | Push | `Back` | `calibration` |
| Gameplay | `GameplayView` | transaction: Pop + Replace (§12.7) | `None` or `Back` (override → pause) | `gameplay` |
| Stage intro | `StageIntroView` (auto-pop) | Push | `None` | `stage-intro` |
| Reward | `RewardView` (3 cards) | Push overlay | `None` | `reward` |
| Pause | `PauseView` | Push overlay | `Back` (= resume) | `pause` |
| Tracking lost | `TrackingLostView` (auto-pop) | Push overlay | `None` | `tracking-lost` |
| Summary | `SummaryView` | Replace | `None` | `summary` |
| Settings | `SettingsView` | Push (from Title or Pause) | `Back` | `settings` |

### 12.2 Vertical button list view (Title / PlayerMode / Pause / Summary)
Runtime snippets in §12 assume `#nullable enable`, `namespace Nex`, and the usings `System`, `Cysharp.Threading.Tasks`, `Nex.KeyboardNavigation`, `UnityEngine`.
Prefab structure (build it with §12.6):
```
TitleView [Canvas SSC, CanvasScaler 1920x1080, GraphicRaycaster, TitleView{entryAnimator, toBackgroundAnimator, keyResponder→KeyResponder, disableCanvasOnBackground ✓}]
├─ Animators/EntryAnimator, Animators/BackgroundAnimator   (MMF_CanvasGroup on UI, Unscaled)
├─ KeyResponder [GraphKeyResponder{fetchChildNodes ✓, initialGraphNode→MenuNode, backButtonResponder→ExitProxy}]
│  ├─ ExitProxy [TopLevelControlProxyKeyResponder{control=Exit} + KeyResponderGraphNode]   (Title: Escape = Exit; other views use control=Back)
│  └─ MenuNode  [KeyResponderGraphNode{keyResponder→UI/Menu.GroupKeyResponder}]
└─ UI [CanvasGroup, stretch]
   └─ Menu [VerticalLayoutGroup, GroupKeyResponder{axis=Vertical, fetchChildResponders ✓, initialActiveIndex 0}]
      ├─ NewRunButton  [Image, Button(nav None, transition None), ButtonKeyResponder(tweets), child TMP + NexLocalizedString]
      ├─ ContinueButton  (SetActive(false) when there is no save → skipped automatically; do it before push)
      └─ SettingsButton
```
```csharp
public class TitleView : SimpleCanvasView
{
    [Header("Buttons")]
    [SerializeField] UnityEngine.UI.Button newRunButton = null!;
    [SerializeField] UnityEngine.UI.Button continueButton = null!;
    [SerializeField] UnityEngine.UI.Button settingsButton = null!;

    public event Action? NewRunClicked;
    public event Action? ContinueClicked;
    public event Action? SettingsClicked;
    public event Action? ExitClicked;

    public override ViewIdentifier Identifier => ViewIdentifier.Title;
    public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Exit;
    public override string AnalyticsScreenName => "title";

    public void Initialize(bool hasSave)
    {
        continueButton.gameObject.SetActive(hasSave);   // before PushView so the group skips it on Activate
        newRunButton.onClick.AddListener(() => HandleButton("new_run", NewRunClicked));
        continueButton.onClick.AddListener(() => HandleButton("continue", ContinueClicked));
        settingsButton.onClick.AddListener(() => HandleButton("settings", SettingsClicked));
    }

    void HandleButton(string action, Action? callback)
    {
        if (!IsActive) return;
        AnalyticsManager.Instance.TrackEvent($"ui_title_{action}");
        SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.GenericEnter);
        callback?.Invoke();
    }

    public override void OnBackButton()
    {
        // Title is the first real view: the default PopSelf would expose the EmptyView root.
    }

    public override bool OnControlButton(TopLevelControlPanel.ButtonKind buttonKind)
    {
        if (!IsActive) return false;
        if (buttonKind != TopLevelControlPanel.ButtonKind.Exit) return false;
        AnalyticsManager.Instance.TrackEvent("ui_title_exit");
        ExitClicked?.Invoke();
        return true;
    }
}
```
Without the Graph wrapper you can assign the `GroupKeyResponder` straight to `keyResponder`. Escape is then a no-op, because `ButtonKeyResponder.HandleBack` returns false.

### 12.3 Three-card horizontal choice (`RewardView`)
Author **3 fixed card slots** in the prefab (asset rule: fixed UI uses serialized refs). Each slot root has `Button` + `ButtonKeyResponder`
(tweets: frame `GraphicTweet` plus `TransformScaleTweet` 1→1.1) + a `RewardCard` component. The slots are **direct children** of
`UI/Cards [HorizontalLayoutGroup, GroupKeyResponder{axis=Horizontal(0), fetchChildResponders ✓, initialActiveIndex 1}]`.
The view's `keyResponder` → the Cards `GroupKeyResponder`. With Controls `None` the reward can't be skipped.
```csharp
public class RewardView : SimpleCanvasView
{
    [SerializeField] RewardCard[] cards = null!;              // exactly 3, authored in prefab
    [SerializeField] GroupKeyResponder cardsResponder = null!;

    public event Action<int>? CardChosen;

    public override ViewIdentifier Identifier => ViewIdentifier.Reward;
    public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.None;
    public override string AnalyticsScreenName => "reward";

    public void Initialize(RewardOffer[] offers)               // RewardOffer = gameplay data type
    {
        for (var i = 0; i < cards.Length; i++)
        {
            var index = i;
            cards[i].Initialize(offers[i]);
            cards[i].Button.onClick.AddListener(() => HandleChoose(index));
        }
        cardsResponder.SetInitialActiveIndex(1);                // start on the middle card
    }

    // Motion-control hover (called by gameplay input): keeps keyboard highlight and body cursor in sync.
    public void Hover(int index)
    {
        if (!IsActive) return;
        cardsResponder.NavigateTo(cards[index].KeyResponder);
    }

    void HandleChoose(int index)
    {
        if (!IsActive) return;
        AnalyticsManager.Instance.TrackEvent("ui_reward_chosen");   // add props (offer id, rarity) per analytics note
        SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.GenericEnter);
        CardChosen?.Invoke(index);                                 // handler must NOT start a transition itself
        PopSelf().Forget();                                        // overlay: gameplay view becomes top again
    }
}
```
`RewardCard : MonoBehaviour` exposes `Button Button`, `KeyResponder KeyResponder` (its ButtonKeyResponder) and `Initialize(RewardOffer)`.
It fills a localized name through `NexLocalizedString.StringReference` / `SetSmartStringArgument`.

### 12.4 Pause overlay (and auto-popping overlays)
```csharp
public class PauseView : SimpleCanvasView
{
    [SerializeField] UnityEngine.UI.Button resumeButton = null!;
    [SerializeField] UnityEngine.UI.Button settingsButton = null!;
    [SerializeField] UnityEngine.UI.Button saveQuitButton = null!;
    [SerializeField] SettingsView settingsViewPrefab = null!;

    public override ViewIdentifier Identifier => ViewIdentifier.Pause;
    public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
    public override string AnalyticsScreenName => "pause";
    public override bool RequiresAdditionalBackgroundBlur => true;   // no-op today (§4.6); dim Image provides the look

    protected override void Awake()
    {
        base.Awake();
        resumeButton.onClick.AddListener(HandleResume);
        settingsButton.onClick.AddListener(HandleSettings);
        saveQuitButton.onClick.AddListener(HandleSaveQuit);
    }

    public override void OnBackButton() => HandleResume();   // Escape / top-level Back = resume

    void HandleResume()
    {
        if (!IsActive) return;
        AnalyticsManager.Instance.TrackEvent("ui_pause_resume");
        Manager.AnnouncePauseViewResumeClicked();
        PopSelf().Forget();
    }

    void HandleSettings()
    {
        if (!IsActive) return;
        AnalyticsManager.Instance.TrackEvent("ui_pause_settings");
        PushView(Instantiate(settingsViewPrefab)).Forget();
    }

    void HandleSaveQuit()
    {
        if (!IsActive) return;
        AnalyticsManager.Instance.TrackEvent("ui_pause_save_quit");
        Manager.AnnouncePauseViewHomeClicked();   // coordinator unwinds (§12.7)
    }
}
```
Opening it from `GameplayView` (single entry point for Escape, `OnBackButton`, `CherryIntegrationManager.PreferGameStopped` and app pause):
```csharp
public void RequestPause()
{
    if (!IsActive) return;                       // also false while any transition runs
    turnController.SetPaused(true);              // gameplay-owned pause (preferred over Time.timeScale = 0)
    AnalyticsManager.Instance.TrackPause();
    Manager.AnnouncePaused();
    Manager.PushView(Instantiate(pauseViewPrefab)).Forget();
}
public override void OnBackButton() => RequestPause();   // never PopSelf the gameplay view

public override void ViewDidBecomeTopView(bool afterPush)
{
    base.ViewDidBecomeTopView(afterPush);
    if (!turnController.IsPaused) return;        // resumed from Pause/Reward/TrackingLost/StageIntro
    turnController.SetPaused(false);
    AnalyticsManager.Instance.TrackResume();
}
// In Initialize: CherryIntegrationManager.Instance.PreferGameStopped.Subscribe(stopped => { if (stopped) RequestPause(); }, destroyCancellationToken);  (using Cysharp.Threading.Tasks.Linq)
```
- **Leave `toBackgroundAnimator` empty on GameplayView.** Otherwise the HUD fades out under every overlay, because blur never engages and `EnterBackground` runs instead.
- If `Time.timeScale = 0` is used anyway, every MMF feedback in overlay views needs Unscaled `PlayerTimescaleMode` and `Timing.TimescaleMode`
  (menu: `Nex/MMFeedbacks/Convert Feedbacks to Unscaled Time`). Keep `PresentDuration` at 0.5 s or below, because of the DismissableControl scaled delay.
- **Auto-popping overlays** (StageIntro, TrackingLost) must wait until they are on top before popping. A pop during their own present animation throws.
```csharp
async UniTaskVoid AutoPopWhen(Func<bool> condition)
{
    await UniTask.WaitUntil(() => IsActive && condition(), cancellationToken: destroyCancellationToken);
    PopSelf().Forget();
}
```

### 12.5 Non-UI View step
Use it for a flow step without a canvas (for example run bootstrap or a camera fly-through). Instantiate it from a prefab (runtime rule: no `new GameObject`).
```csharp
public class RunBootstrapStepView : View
{
    public event Action? Completed;
    public override ViewIdentifier Identifier => ViewIdentifier.Empty;   // or a dedicated identifier
    public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.None;
    public override UniTask Present(bool animate = true) => UniTask.CompletedTask;
    public override UniTask Dismiss(bool animate = true) => UniTask.CompletedTask;

    public override void ViewDidBecomeTopView(bool afterPush)
    {
        base.ViewDidBecomeTopView(afterPush);
        RunAsync().Forget();
    }

    async UniTaskVoid RunAsync()
    {
        await UniTask.Yield(destroyCancellationToken);   // leave the PushView continuation before the next transition
        // ... awaited work ...
        Completed?.Invoke();                              // coordinator: viewManager.ReplaceView(next)
    }
}
```
Note: `ViewCamera` and `ViewOrder` are just stored on a plain `View`. An `EmptyView` identifier suits a pure anchor.

### 12.6 Editor prefab builder (reproduces the WelcomeScreenView setup)
Put it under `Assets/Scripts/Editor/BilliardRogue/` (ns `Nex.BilliardRogue.Editor`, Assembly-CSharp-Editor) and run it via `unity command`, a menu item, or `-executeMethod`.
Private `[SerializeField]` fields are set through `SerializedObject` using the field names in this note.
```csharp
#nullable enable
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Nex.KeyboardNavigation;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    public static class ViewPrefabBuilder
    {
        const string parentFolder = "Assets/Prefabs/Views";
        const string folder = "Assets/Prefabs/Views/BilliardRogue";

        [MenuItem("Nex/Billiard Rogue/Build Pause View")]
        public static void BuildPauseView()
        {
            var root = CreateViewRoot<PauseView>("PauseView", out var view);
            var ui = CreateUIObject("UI", root.transform);
            Stretch(ui);
            var uiGroup = ui.AddComponent<CanvasGroup>();
            var dim = CreateUIObject("Dim", ui.transform);
            Stretch(dim);
            dim.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.5f);

            var animators = CreateUIObject("Animators", root.transform);
            var entry = CreateFadeAnimator("EntryAnimator", animators.transform, uiGroup, 0f, 1f);

            var menu = CreateUIObject("Menu", ui.transform);
            ((RectTransform)menu.transform).sizeDelta = new Vector2(600f, 480f);
            var layout = menu.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;       // buttons keep their authored size
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var menuGroup = menu.AddComponent<GroupKeyResponder>();
            var resume = CreateMenuButton("ResumeButton", menu.transform);
            var settings = CreateMenuButton("SettingsButton", menu.transform);
            var saveQuit = CreateMenuButton("SaveQuitButton", menu.transform);

            var groupSo = new SerializedObject(menuGroup);
            groupSo.FindProperty("axis").enumValueIndex = 1;               // Vertical
            groupSo.FindProperty("fetchChildResponders").boolValue = true;
            groupSo.ApplyModifiedPropertiesWithoutUndo();
            // Escape → top-level Back → PauseView.OnBackButton (resume). Requires Controls to include Back.
            var graph = CreateGraphWithBackProxy(root.transform, menuGroup, TopLevelControlPanel.ControlConfig.Back);

            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("entryAnimator").objectReferenceValue = entry;
            viewSo.FindProperty("keyResponder").objectReferenceValue = graph;
            viewSo.FindProperty("resumeButton").objectReferenceValue = resume;
            viewSo.FindProperty("settingsButton").objectReferenceValue = settings;
            viewSo.FindProperty("saveQuitButton").objectReferenceValue = saveQuit;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            Save(root, "PauseView");
        }

        static GameObject CreateUIObject(string name, Transform? parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static void Stretch(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }

        static GameObject CreateViewRoot<TView>(string name, out TView view) where TView : SimpleCanvasView
        {
            var go = CreateUIObject(name, null);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;               // ViewManager re-applies it; worldCamera set at push
            canvas.planeDistance = 100f;
            canvas.sortingOrder = 0;
            canvas.additionalShaderChannels = (AdditionalCanvasShaderChannels)27; // same as WelcomeScreenView (TMP)
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            view = go.AddComponent<TView>();
            return go;
        }

        static MMF_Player CreateFadeAnimator(string name, Transform parent, CanvasGroup target, float from, float to)
        {
            var go = CreateUIObject(name, parent);
            var player = go.AddComponent<MMF_Player>();
            player.PlayerTimescaleMode = TimescaleModes.Unscaled;
            var fade = (MMF_CanvasGroup)player.AddFeedback(typeof(MMF_CanvasGroup));
            fade.TargetCanvasGroup = target;
            fade.Mode = MMF_FeedbackBase.Modes.OverTime;
            fade.Duration = 0.5f;
            fade.AlphaCurve = new MMTweenType(AnimationCurve.Linear(0f, 0f, 1f, 1f)); // default curve is a 0→1→0 bump!
            fade.RemapZero = from;
            fade.RemapOne = to;
            fade.Timing.TimescaleMode = TimescaleModes.Unscaled;
            return player;
        }

        static UnityEngine.UI.Button CreateMenuButton(string name, Transform parent)
        {
            var go = CreateUIObject(name, parent);
            ((RectTransform)go.transform).sizeDelta = new Vector2(520f, 110f);
            var image = go.AddComponent<UnityEngine.UI.Image>();
            var button = go.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var responder = go.AddComponent<ButtonKeyResponder>();
            var so = new SerializedObject(responder);
            so.FindProperty("button").objectReferenceValue = button;
            var colors = so.FindProperty("graphicTweets");
            colors.arraySize = 1;
            var color = colors.GetArrayElementAtIndex(0);
            color.FindPropertyRelative("graphic").objectReferenceValue = image;
            color.FindPropertyRelative("gainFocusColor").colorValue = new Color(1f, 0.995f, 0.193f, 1f);
            color.FindPropertyRelative("loseFocusColor").colorValue = Color.white;
            var scales = so.FindProperty("transformScaleTweets");
            scales.arraySize = 1;
            var scale = scales.GetArrayElementAtIndex(0);
            scale.FindPropertyRelative("transform").objectReferenceValue = go.transform;
            scale.FindPropertyRelative("gainFocusScale").vector3Value = Vector3.one * 1.1f;
            scale.FindPropertyRelative("loseFocusScale").vector3Value = Vector3.one;
            scale.FindPropertyRelative("animationCurve").animationCurveValue = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            // Label: child TextMeshProUGUI + Nex.Localization.NexLocalizedString (StringReference.SetReference(table, key)).
            return button;
        }

        // Mirrors DebugSettingsView: GraphKeyResponder{fetchChildNodes} with a proxy node (activated → Escape works)
        // and a node pointing at the real content responder.
        static GraphKeyResponder CreateGraphWithBackProxy(Transform root, KeyResponder content,
            TopLevelControlPanel.ControlConfig backControl)
        {
            var graphGo = CreateUIObject("KeyResponder", root);
            var graph = graphGo.AddComponent<GraphKeyResponder>();
            var proxyGo = CreateUIObject("BackProxy", graphGo.transform);
            var proxy = proxyGo.AddComponent<TopLevelControlProxyKeyResponder>();
            var proxyNode = proxyGo.AddComponent<KeyResponderGraphNode>();
            var contentNode = CreateUIObject("ContentNode", graphGo.transform).AddComponent<KeyResponderGraphNode>();

            var proxySo = new SerializedObject(proxy);
            proxySo.FindProperty("control").intValue = (int)backControl;  // [Flags] enum → intValue
            proxySo.ApplyModifiedPropertiesWithoutUndo();
            SetReference(proxyNode, "keyResponder", proxy);
            SetReference(contentNode, "keyResponder", content);

            var graphSo = new SerializedObject(graph);
            graphSo.FindProperty("fetchChildNodes").boolValue = true;
            graphSo.FindProperty("initialGraphNode").objectReferenceValue = contentNode;
            graphSo.FindProperty("backButtonResponder").objectReferenceValue = proxy;
            graphSo.ApplyModifiedPropertiesWithoutUndo();
            return graph;
        }

        static void SetReference(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Save(GameObject root, string name)
        {
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parentFolder, "BilliardRogue");
            PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{name}.prefab", out var success);
            Object.DestroyImmediate(root);
            if (!success) throw new System.InvalidOperationException($"Saving {name} failed");
        }
    }
}
```
For views with no Escape behaviour, assign the content responder (e.g. `menuGroup`) directly to `keyResponder` and skip the graph.
For a Title view, use `ControlConfig.Exit` as the proxy control.
For a background fade, add `CreateFadeAnimator("BackgroundAnimator", ..., 1f, 0f)` → `toBackgroundAnimator`.
For the 3-card view, use `axis` enumValueIndex 0 (Horizontal) and `initialActiveIndex` 1.
For an enter SFX: `var sfx = (Nex.MMF.MMF_SfxManager)player.AddFeedback(typeof(Nex.MMF.MMF_SfxManager)); sfx.Timing.MMFeedbacksDirectionCondition = MMFeedbackTiming.MMFeedbacksDirectionConditions.OnlyWhenForwards;`
(it is skipped automatically on `animate:false`).

**Wire the secret code** (on the Billiard Rogue ViewManager prefab, or on `MainViewManager.prefab`):
```csharp
// using Key = Nex.KeyboardNavigation.KeyboardNavigationController.Key;
[MenuItem("Nex/Billiard Rogue/Wire Debug Secret Code")]
public static void WireSecretCode()
{
    const string path = "Assets/Prefabs/Coordinators/MainViewManager.prefab";   // root GO holds ViewManager
    var root = PrefabUtility.LoadPrefabContents(path);
    try
    {
        if (!root.TryGetComponent<SecretCodeSequenceDetector>(out var detector))
        {
            detector = root.AddComponent<SecretCodeSequenceDetector>();
        }
        var so = new SerializedObject(detector);
        var configs = so.FindProperty("configs");
        configs.arraySize = 1;                                   // ViewManager uses index 0
        var sequence = configs.GetArrayElementAtIndex(0).FindPropertyRelative("sequence");
        Key[] keys = { Key.Up, Key.Up, Key.Down, Key.Down, Key.Left, Key.Right, Key.Left, Key.Right };
        sequence.arraySize = keys.Length;
        for (var i = 0; i < keys.Length; i++) sequence.GetArrayElementAtIndex(i).enumValueIndex = (int)keys[i];
        so.ApplyModifiedPropertiesWithoutUndo();                 // keyboardController left null → GetComponent at Start
        PrefabUtility.SaveAsPrefabAsset(root, path);
    }
    finally
    {
        PrefabUtility.UnloadPrefabContents(root);
    }
}
```

### 12.7 Coordinator transitions (GDD flow)
```csharp
// Calibration done → Gameplay with Title directly below (stack: Empty, Title, PlayerMode, Calibration → Empty, Title, Gameplay)
async UniTask EnterGameplay()
{
    using (viewManager.CreateTransaction())
    {
        await viewManager.PopView(animate: false);                  // Calibration
        await viewManager.ReplaceView(CreateGameplayView());        // PlayerMode → Gameplay
    }
}

// Pause "Save & Quit" (subscribe once: viewManager.PauseViewHomeClicked += ...)
async UniTask QuitToTitle()
{
    using (viewManager.CreateTransaction())
    {
        while (viewManager.TopViewIdentifier is not (View.ViewIdentifier.Gameplay or View.ViewIdentifier.Empty))
        {
            await viewManager.PopView(animate: false);              // Settings?/Pause
        }
        if (viewManager.TopViewIdentifier != View.ViewIdentifier.Gameplay) return;   // never pop the EmptyView root
        await viewManager.PopView();                                // Gameplay → Title (Title never re-activated mid-way)
    }
}

// Run end: Gameplay → Summary (Back must not return to gameplay)
UniTask ShowSummary(RunResult result) => viewManager.ReplaceView(CreateSummaryView(result));
```
Always check `viewManager.IsInTransition` (or the calling view's `IsActive`) before starting a transition from gameplay callbacks such as run end, tracking lost, or platform pause.

### 12.8 Custom key responder (remote/keyboard fallback on GameplayView)
```csharp
public class GameplayKeyResponder : AbstractCustomKeyResponder<GameplayView>
{
    public override NavigationResult HandleNavigation(NavigationKey key)
    {
        host.HandleRemoteNavigation(key);                // internal method on the view
        return new NavigationResult(RectTransform);      // consumed
    }
    public override bool HandleEnter() { host.HandleRemoteConfirm(); return true; }
    public override bool HandleBack() { host.RequestPause(); return true; }
}
// GameplayView: KeyResponder gameplayResponder = null!;
// protected override void Awake() { base.Awake(); gameplayResponder = AbstractCustomKeyResponder<GameplayView>.Create<GameplayKeyResponder>(this); }
// public override KeyResponder KeyResponder => gameplayResponder;
```
The view root must not already carry a `KeyResponder` (`DisallowMultipleComponent`). Dev-only aim/fire keys belong in `Nex.Dev.DebugInput` (GDD §3.3), not here.

### 12.9 Motion-control ("paw") UI selection
The framework has no pointer or body navigation. Drive the same responders so keyboard and body stay consistent:
highlight with `group.NavigateTo(buttonKeyResponder)` (or `GraphKeyResponder.NavigateTo(node)`), and confirm with `button.onClick.Invoke()`.
Handlers already guard `IsActive`. The highlight shows only while `appViewState.enableHighlighting` is true. It defaults to true and the Period key toggles it.

---

## 13. Gotcha checklist
- [ ] Every transition call guarded (`IsActive` / `IsInTransition`). Never Pop the root. Never throw from Present/Dismiss (deadlock).
- [ ] Overrode `OnBackButton` on Title and Gameplay (the default pops). Handled every declared control in `OnControlButton`.
- [ ] `ViewDidBecomeTopView` fires again after every pop and every transaction end. Make it idempotent and don't rely on `afterPush` after a Replace.
- [ ] Screen names are set (they re-fire on return). UI actions are tracked explicitly. Enter SFX is played explicitly or through `MMF_SfxManager`.
- [ ] View canvases keep `sortingOrder 0`. Full-screen views under Gameplay use `disableCanvasOnBackground`.
- [ ] Feedbacks are Unscaled in overlay views. `MMF_CanvasGroup.AlphaCurve` is set explicitly. `TransformScaleTweet.animationCurve` has keys.
- [ ] Buttons: navigation None, transition None, `ButtonKeyResponder`, registered in a group/graph. Disabled options are checked in the handler.
- [ ] Top-level control buttons get visible children (the prefab has none).
- [ ] Secret code detector added with `configs[0]`, and OpenDebugSettings hardened.
- [ ] Coordinator prefab: no `UIBackground`, a single base camera, RootCamera in its stack, UI culling mask.
- [ ] Each scene has an `EventSystem` (the scene provides it, not a singleton). Keyboard navigation works without one; mouse clicks don't.
- [ ] Use fully qualified `UnityEngine.UI.*` types in code (ui-ugui skill rule).

## 14. Unverified / to confirm in the Editor
- Keyboard navigation of the DebugSettings rows: `DebugSettingsView.Awake` does not call `rowsKeyResponder.ReinitializeWithChildResponders()`
  (the generic `DebugSettingsPanel.Initialize<T>` does). Test whether arrow keys reach the rows.
- `MMF_Player.AddFeedback(Type)` in edit mode, before the player is initialized: confirm the prefab serializes the `[SerializeReference]` list correctly (inspect it after building).
- Order of `Awake` between `MainTopLevelControlPanel` (which re-activates the debug button) and `DismissableControl` (which deactivates itself).
  The first frame's visibility of the debug hotspot was not traced.
- Whether a CanvasGroup at alpha 0 stops uGUI from submitting draws for background views (hence the `disableCanvasOnBackground` recommendation).
- Nex Playground remote key mapping to legacy `KeyCode`s (assumed: DPAD → arrows, OK → Return/JoystickButton0, Back → Escape).
