#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>P1 / P2 tags in the player colours (2-player runs only): the active shooter is full size and bouncing.</summary>
    public sealed class PlayerTagsWidget : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [SerializeField] RectTransform[] tags = null!;
        [SerializeField] CanvasGroup[] tagGroups = null!;
        [Tooltip("Graphics tinted with each player's colour (the tag labels).")]
        [SerializeField] Graphic[] tagTints = null!;
        [SerializeField] TextLabel[] tagLabels = null!;
        [Tooltip("Scale of the waiting player's tag.")]
        [SerializeField, Range(0.5f, 1f)] float inactiveScale = 0.8f;
        [SerializeField, Range(0f, 1f)] float inactiveAlpha = 0.45f;

        int active = -1;
        int players = -1;
        Tween? bounce;

        // Authored inactive: Awake runs on the first 2-player Set.
        void Awake()
        {
            for (var i = 0; i < tags.Length; i++)
            {
                tagTints[i].color = theme.PlayerColor(i);
                tagLabels[i].SetKey(LocKeys.Hud.PlayerTag, i + 1);
            }
        }

        public void Set(int playerIndex, int numPlayers)
        {
            if (playerIndex == active && numPlayers == players) return;
            active = playerIndex;
            players = numPlayers;
            gameObject.SetActive(numPlayers > 1);
            bounce?.Kill();
            if (numPlayers <= 1) return;
            for (var i = 0; i < tags.Length; i++)
            {
                var on = i == playerIndex;
                tags[i].localScale = Vector3.one * (on ? 1f : inactiveScale);
                tagGroups[i].alpha = on ? 1f : inactiveAlpha;
            }

            bounce = tags[playerIndex].DOScale(theme.ChipPulseScale, theme.ChipPulseDuration).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }
    }
}
