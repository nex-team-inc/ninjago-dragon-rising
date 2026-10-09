#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Nex.BilliardRogue.Simulation;
using Nex.KeyboardNavigation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Motion reward pick (GDD v2 §4): three big balls float across the upper middle; two cat arms rise from the bottom
    /// corners and follow the chooser's paws (IPawPointer); both paws on one ball fill its ring for rewardHoldSeconds and
    /// pick it (grab, SFX, VFX). Remote arrows + Enter, DebugHooks.ChooseReward (TryChoose) and, in the Editor without a
    /// tracked body, the mouse (both paws) still work. ChooseAsync fills the balls, waits until the view is on top,
    /// reveals them, waits for a pick, plays the grab and pops itself once it is on top again.
    /// Usage: Instantiate → Initialize → SetPawPointer → PushView (don't await first) → await ChooseAsync.
    /// </summary>
    public sealed class RewardView : RogueView
    {
        enum PickMethod
        {
            Paws,
            Remote,
            Debug,
        }

        static readonly string[] methodButtons = { "ball_paws", "ball_remote", "ball_debug" };
        static readonly string[] methodInputs = { "motion", "remote", "debug" };

        [Header("Balls")]
        [Tooltip("Exactly three slots, direct children of optionsGroup.")]
        [SerializeField] RewardBallOption[] options = null!;
        [SerializeField] GroupKeyResponder optionsGroup = null!;

        [Header("Arms (left, right)")]
        [SerializeField] PawArm[] arms = null!;
        [Tooltip("Full-screen layer the arms and paw positions live in (origin at its centre).")]
        [SerializeField] RectTransform armsLayer = null!;

        [Header("2P")]
        [SerializeField] GameObject chooserBanner = null!;
        [SerializeField] TextLabel chooserLabel = null!;

        readonly RewardPawPicker picker = new();
        readonly float[] hoverAmounts = new float[RewardPawPicker.MaxBalls];
        PacingConfig pacing = null!;
        Camera? vfxCamera;
        IPawPointer? pointer;
        UniTaskCompletionSource<int>? choice;
        int shownCount;
        bool interactable;
        bool revealed;
        bool picking;
        float rise;
        int lastHovered = -1;
#if UNITY_EDITOR
        Vector3 mouseAtOpen;
        bool mouseArmed;
#endif

        public override ViewIdentifier Identifier => ViewIdentifier.Reward;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.None;
        public override string AnalyticsScreenName => "reward";

        #region Life Cycle

        protected override void Awake()
        {
            base.Awake();
            for (var i = 0; i < options.Length; i++)
            {
                var index = i;
                options[i].Button.onClick.AddListener(() => HandleChoose(index, PickMethod.Remote));
                // Hidden until ChooseAsync reveals them, even when the view is pushed before ChooseAsync fills them.
                options[i].BodyGroup.alpha = 0f;
            }

            SetLook(0, 1);
        }

        void Update()
        {
            if (!revealed) return;
            var dt = Time.unscaledDeltaTime;
            AnimateBalls(dt, Time.unscaledTime);
            if (picking) return;
            var tracked = TryGetPaws(out var left, out var right);
            var sway = Mathf.Sin(Time.unscaledTime * 2.1f) * 14f;
            arms[0].Follow(tracked ? left : arms[0].RestPosition + new Vector2(0f, sway), theme.RewardPawSharpness, dt, rise);
            arms[1].Follow(tracked ? right : arms[1].RestPosition - new Vector2(0f, sway), theme.RewardPawSharpness, dt, rise);
            if (!interactable || !IsActive) return;
            if (tracked) picker.Observe(left, right);
            StepPicker(tracked, dt);
        }

        #endregion

        #region Public Methods

        /// <summary>vfxCamera: the world camera (pick VFX play in the world under the ball); null = UI flash only.</summary>
        public void Initialize(PacingConfig pacing, Camera? vfxCamera = null)
        {
            this.pacing = pacing;
            this.vfxCamera = vfxCamera;
        }

        /// <summary>
        /// Motion pick (GDD v2 §4): the chooser's paws drive the two cat arms (P1 orange / P2 charcoal); holding both
        /// on a ball picks it. Call before ChooseAsync; null keeps remote/keyboard (and the Editor mouse) only.
        /// </summary>
        public void SetPawPointer(IPawPointer? pointer, int chooserIndex, int numPlayers)
        {
            this.pointer = pointer;
            SetLook(Mathf.Max(0, chooserIndex), numPlayers);
        }

        public async UniTask<int> ChooseAsync(IReadOnlyList<RewardOption> rewards, BallCatalog balls, RunState run,
            CancellationToken ct = default)
        {
            shownCount = Mathf.Min(rewards.Count, options.Length);
            for (var i = 0; i < options.Length; i++)
            {
                var option = options[i];
                option.gameObject.SetActive(i < shownCount);
                if (i >= shownCount) continue;
                option.Show(rewards[i], balls, run, theme);
                option.BodyGroup.alpha = 0f;
                hoverAmounts[i] = 0f;
            }

            picker.Reset(shownCount, theme.RewardPawArmDistance);
            optionsGroup.SetInitialActiveIndex(shownCount == 3 ? 1 : 0);
            choice = new UniTaskCompletionSource<int>();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken);
            await UniTask.WaitUntil(() => IsActive, cancellationToken: linked.Token);
#if UNITY_EDITOR
            mouseAtOpen = UnityEngine.Input.mousePosition;
            mouseArmed = false;
#endif
            await RevealAsync(linked.Token);
            interactable = true;
            var index = await choice.Task.AttachExternalCancellation(linked.Token);
            await PickAsync(index, linked.Token);
            // A pause pushed during the pick animation: pop once we are the top view again (PopSelf pops the top).
            await UniTask.WaitUntil(() => IsActive, cancellationToken: linked.Token);
            await PopSelf();
            return index;
        }

        /// <summary>Motion/CLI hover: moves the keyboard highlight to a ball.</summary>
        public void Hover(int index)
        {
            if (!IsActive) return;
            if (index < 0) return;
            if (index >= shownCount) return;
            optionsGroup.NavigateTo(options[index].Responder);
        }

        /// <summary>Debug/CLI pick (DebugHooks.ChooseReward). False while the balls are not selectable yet.</summary>
        public bool TryChoose(int index)
        {
            if (!IsActive || !interactable || index < 0 || index >= shownCount)
            {
                return false;
            }

            HandleChoose(index, PickMethod.Debug);
            return true;
        }

        #endregion

        #region Paws

        void SetLook(int chooserIndex, int numPlayers)
        {
            var sleeve = theme.Arm(chooserIndex);
            var open = theme.PawOpen(chooserIndex);
            var grab = theme.PawGrab(chooserIndex);
            for (var i = 0; i < arms.Length; i++)
            {
                arms[i].SetLook(sleeve, open, grab);
            }

            var twoPlayers = numPlayers > 1;
            chooserBanner.SetActive(twoPlayers);
            if (!twoPlayers) return;
            chooserLabel.SetKey(LocKeys.Reward.ChooserBanner, chooserIndex + 1);
            chooserLabel.Color = theme.PlayerColor(chooserIndex);
        }

        /// <summary>Paws in the arms layer's space: the pointer, else (Editor only) the mouse once it moved.</summary>
        bool TryGetPaws(out Vector2 left, out Vector2 right)
        {
            var size = armsLayer.rect.size;
            if (pointer != null && pointer.TryGetPaws(out var left01, out var right01))
            {
                left = (left01 - new Vector2(0.5f, 0.5f)) * size;
                right = (right01 - new Vector2(0.5f, 0.5f)) * size;
                return true;
            }

#if UNITY_EDITOR
            var mouse = UnityEngine.Input.mousePosition;
            mouseArmed |= (mouse - mouseAtOpen).sqrMagnitude > 16f;
            var inside = mouse.x >= 0f && mouse.y >= 0f && mouse.x <= Screen.width && mouse.y <= Screen.height;
            if (mouseArmed && inside && Screen.width > 0 && Screen.height > 0)
            {
                var at = (new Vector2(mouse.x / Screen.width, mouse.y / Screen.height) - new Vector2(0.5f, 0.5f)) * size;
                left = at - new Vector2(36f, 0f);
                right = at + new Vector2(36f, 0f);
                return true;
            }
#endif
            left = right = default;
            return false;
        }

        void StepPicker(bool tracked, float dt)
        {
            for (var i = 0; i < shownCount; i++)
            {
                picker.SetBall(i, BallCenter(i), options[i].HitRadius(theme.RewardPawHitScale));
            }

            var picked = picker.Step(tracked, arms[0].PawPosition, arms[1].PawPosition, dt, theme.RewardHoldSeconds, theme.RewardHoldDrain);
            for (var i = 0; i < shownCount; i++)
            {
                options[i].SetFill(picker.Fill(i), picker.LeftHover == i || picker.RightHover == i);
            }

            var hovered = picker.Hovered;
            if (hovered != lastHovered)
            {
                lastHovered = hovered;
                if (hovered >= 0)
                {
                    optionsGroup.NavigateTo(options[hovered].Responder);
                    UiTheme.PlaySfx(theme.RewardHoverSfx);
                }
            }

            if (picked >= 0) HandleChoose(picked, PickMethod.Paws);
        }

        Vector2 BallCenter(int index) => armsLayer.InverseTransformPoint(options[index].Visual.position);

        void AnimateBalls(float dt, float time)
        {
            var hovered = picking ? -1 : picker.Hovered;
            var phaseStep = 1f / Mathf.Max(1, shownCount);
            for (var i = 0; i < shownCount; i++)
            {
                var target = i == hovered ? 1f : 0f;
                hoverAmounts[i] = Mathf.MoveTowards(hoverAmounts[i], target, dt * 7f);
                var bob = Mathf.Sin((time / theme.RewardBobPeriod + i * phaseStep) * Mathf.PI * 2f) * theme.RewardBobDistance;
                options[i].SetHover(Mathf.SmoothStep(0f, 1f, hoverAmounts[i]), theme.RewardHoverScale, bob);
            }
        }

        #endregion

        #region Helpers

        void HandleChoose(int index, PickMethod method)
        {
            if (!IsActive) return;
            if (!interactable) return;
            if (index >= shownCount) return;
            interactable = false;
            TrackButton(methodButtons[(int)method], index, methodInputs[(int)method]);
            choice?.TrySetResult(index);
        }

        async UniTask RevealAsync(CancellationToken ct)
        {
            rise = 0f;
            for (var i = 0; i < arms.Length; i++)
            {
                arms[i].Snap(arms[i].RestPosition, 0f);
            }

            revealed = true;
            await RevealSequence().ToUniTask(TweenCancelBehaviour.Complete, ct);
        }

        UniTask PickAsync(int index, CancellationToken ct)
        {
            picking = true;
            for (var i = 0; i < shownCount; i++)
            {
                options[i].SetFill(i == index ? 1f : 0f, i == index);
            }

            return PickSequence(index).ToUniTask(TweenCancelBehaviour.Complete, ct);
        }

        Sequence RevealSequence()
        {
            var duration = theme.RewardRevealDuration;
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            for (var i = 0; i < shownCount; i++)
            {
                var body = options[i].Body;
                var group = options[i].BodyGroup;
                body.localScale = Vector3.zero;
                var at = i * pacing.RewardRevealStagger;
                sequence.InsertCallback(at, () => UiTheme.PlaySfx(theme.RewardRevealSfx));
                sequence.Insert(at, body.DOScale(1f, duration).SetEase(Ease.OutBack));
                sequence.Insert(at, group.DOFade(1f, duration * 0.5f));
            }

            sequence.Insert(shownCount * pacing.RewardRevealStagger * 0.5f,
                DOTween.To(() => rise, value => rise = value, 1f, theme.RewardArmRiseDuration).SetEase(Ease.OutBack));
            return sequence;
        }

        Sequence PickSequence(int index)
        {
            var duration = pacing.RewardPickDuration;
            var grab = theme.RewardGrabDuration;
            var chosen = options[index];
            var center = BallCenter(index);
            var fromLeft = arms[0].PawPosition;
            var fromRight = arms[1].PawPosition;
            var reach = chosen.HitRadius(0.35f);
            var t = 0f;
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            sequence.Append(DOTween.To(() => t, value =>
            {
                t = value;
                arms[0].Snap(Vector2.Lerp(fromLeft, center - new Vector2(reach, 0f), t), rise);
                arms[1].Snap(Vector2.Lerp(fromRight, center + new Vector2(reach, 0f), t), rise);
            }, 1f, grab).SetEase(Ease.OutQuad));
            sequence.AppendCallback(() => OnGrab(index));
            sequence.Append(chosen.Body.DOPunchScale(Vector3.one * theme.RewardPickPunch, duration, 6, 0.6f));
            var glow = chosen.Glow.rectTransform;
            sequence.Join(glow.DOScale(2.2f, duration).SetEase(Ease.OutCubic));
            sequence.Join(chosen.Glow.DOFade(0f, duration).SetEase(Ease.InQuad));
            for (var i = 0; i < shownCount; i++)
            {
                if (i == index) continue;
                sequence.Join(options[i].BodyGroup.DOFade(0.25f, duration * 0.5f));
                sequence.Join(options[i].Body.DOScale(0.85f, duration * 0.5f).SetEase(Ease.InQuad));
            }

            // The paws pull the ball down with them as the arms sink below the screen edge.
            var at = grab + duration * 0.5f;
            var retract = duration * 0.6f;
            var root = (RectTransform)chosen.transform;
            sequence.Insert(at, DOTween.To(() => rise, value =>
            {
                rise = value;
                arms[0].Snap(arms[0].PawPosition, rise);
                arms[1].Snap(arms[1].PawPosition, rise);
            }, 0f, retract).SetEase(Ease.InBack));
            sequence.Insert(at, root.DOAnchorPosY(root.anchoredPosition.y - arms[0].DropDistance, retract).SetEase(Ease.InBack));
            sequence.Insert(at, chosen.Floater.DOScale(0.7f, retract).SetEase(Ease.InQuad));
            return sequence;
        }

        void OnGrab(int index)
        {
            for (var i = 0; i < arms.Length; i++)
            {
                arms[i].SetGrab(true);
            }

            UiTheme.PlaySfx(theme.RewardPickSfx);
            PlayPickVfx(options[index].Visual.position);
        }

        /// <summary>VfxManager effect in the world under the ball's screen point (the world shows once the view pops).</summary>
        void PlayPickVfx(Vector3 uiWorld)
        {
            if (vfxCamera == null) return;
            var screen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, uiWorld);
            var ray = vfxCamera.ScreenPointToRay(screen);
            var floor = new Plane(Vector3.up, Vector3.zero);
            var point = floor.Raycast(ray, out var enter) ? ray.GetPoint(enter) : ray.GetPoint(10f);
            VfxManager.Instance.PlayVisualEffect(theme.RewardPickVfx, point, Quaternion.identity, 1.5f);
        }

        #endregion
    }
}
