#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// "MOVE!" prompt near the arena bottom (GDD v2 §3): shown while balls fly and the players stand still. A dancing
    /// cat (the shooter's portrait swaying to a beat, both paws waving in turn) beside a pulsing MOVE! label. Fades in
    /// and out on unscaled time; the content is deactivated while hidden so it costs nothing.
    /// </summary>
    public sealed class MovePromptWidget : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [SerializeField] CanvasGroup group = null!;
        [SerializeField] GameObject content = null!;
        [SerializeField] RectTransform cat = null!;
        [SerializeField] Image catImage = null!;
        [SerializeField] RectTransform pawLeft = null!;
        [SerializeField] RectTransform pawRight = null!;
        [SerializeField] Image pawLeftImage = null!;
        [SerializeField] Image pawRightImage = null!;
        [Tooltip("Pulsed with the beat.")]
        [SerializeField] RectTransform label = null!;
        [SerializeField, Range(0.05f, 1f)] float fadeDuration = 0.18f;
        [SerializeField, Range(0f, 64f)] float pawWave = 28f;

        bool visible;
        float alpha;
        int player = -1;
        Vector2 catHome;
        Vector2 pawLeftHome;
        Vector2 pawRightHome;

        void Awake()
        {
            catHome = cat.anchoredPosition;
            pawLeftHome = pawLeft.anchoredPosition;
            pawRightHome = pawRight.anchoredPosition;
            group.alpha = 0f;
            content.SetActive(false);
            SetPlayer(0);
        }

        public void SetVisible(bool show)
        {
            if (show == visible) return;
            visible = show;
            if (show) content.SetActive(true);
        }

        /// <summary>Cat portrait and paw colours of the active shooter.</summary>
        public void SetPlayer(int playerIndex)
        {
            if (playerIndex == player) return;
            player = playerIndex;
            catImage.sprite = theme.Portrait(playerIndex);
            pawLeftImage.sprite = pawRightImage.sprite = theme.PawOpen(playerIndex);
        }

        void Update()
        {
            if (!content.activeSelf) return;
            var dt = Time.unscaledDeltaTime;
            alpha = Mathf.MoveTowards(alpha, visible ? 1f : 0f, dt / fadeDuration);
            group.alpha = alpha;
            if (!visible && alpha <= 0f)
            {
                content.SetActive(false);
                return;
            }

            var beat = Time.unscaledTime / theme.MoveDanceBeat;
            var sway = Mathf.Sin(beat * Mathf.PI);
            var hop = Mathf.Abs(sway);
            cat.localRotation = Quaternion.Euler(0f, 0f, sway * theme.MoveDanceAngle);
            cat.anchoredPosition = catHome + new Vector2(0f, hop * 14f);
            pawLeft.anchoredPosition = pawLeftHome + new Vector2(0f, Mathf.Max(0f, sway) * pawWave);
            pawRight.anchoredPosition = pawRightHome + new Vector2(0f, Mathf.Max(0f, -sway) * pawWave);
            pawLeft.localRotation = Quaternion.Euler(0f, 0f, 12f + sway * 18f);
            pawRight.localRotation = Quaternion.Euler(0f, 0f, -12f + sway * 18f);
            var pulse = 1f + hop * 0.08f;
            label.localScale = new Vector3(pulse, pulse, 1f);
        }
    }
}
