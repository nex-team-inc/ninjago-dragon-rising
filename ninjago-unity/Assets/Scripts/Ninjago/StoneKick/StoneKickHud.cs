#nullable enable

using System.Collections.Generic;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>One player's half of the Stone Kick HUD: tag, hearts, throw count, KICK prompt with its count, hints.</summary>
    public class StoneKickHud : MonoBehaviour
    {
        [Header("Player Tag")]
        [SerializeField] PlayerTagLabel playerTag = null!;
        [Header("Hearts Row")]
        [SerializeField] RectTransform heartsRow = null!;
        [Header("Heart Prefab")]
        [SerializeField] Image heartPrefab = null!;
        [Header("Full Heart Color")]
        [SerializeField] Color fullHeartColor = new(1f, 0.25f, 0.3f);
        [Header("Empty Heart Color")]
        [SerializeField] Color emptyHeartColor = new(0.15f, 0.15f, 0.15f, 0.5f);
        [Header("Throw Label")]
        [SerializeField] NexLocalizedString throwLabel = null!;
        [Header("Throw Text")]
        [Tooltip("Smart string with {current} {total}.")]
        [SerializeField] LocalizedString throwText = new();
        [Header("Kick Prompt")]
        [Tooltip("The big KICK word and its count, near the hanging pieces.")]
        [SerializeField] GameObject kickPrompt = null!;
        [Header("Kick Count Label")]
        [SerializeField] NexLocalizedString kickCountLabel = null!;
        [Header("Kick Count Text")]
        [Tooltip("Smart string with {count} {total}.")]
        [SerializeField] LocalizedString kickCountText = new();
        [Header("Slash Hint")]
        [SerializeField] GameObject slashHint = null!;
        [Header("Hit Flash")]
        [SerializeField] Image hitFlash = null!;
        [Header("Hit Flash Seconds")]
        [SerializeField] float hitFlashSeconds = 0.45f;
        [Header("Out Banner")]
        [SerializeField] GameObject outBanner = null!;
        [Header("Finished Banner")]
        [SerializeField] GameObject finishedBanner = null!;

        readonly List<Image> hearts = new();
        float hitFlashStart = float.NegativeInfinity;

        #region Initialization

        public void Initialize(int playerIndex, Color color, int maxHearts, int throws)
        {
            playerTag.Initialize(playerIndex, color);
            for (var i = 0; i < maxHearts; i++)
            {
                hearts.Add(Instantiate(heartPrefab, heartsRow));
            }

            SetHearts(maxHearts);
            SetThrow(1, throws);
            HideKick();
            ShowSlashHint(false);
            hitFlash.enabled = false;
            outBanner.SetActive(false);
            finishedBanner.SetActive(false);
        }

        #endregion

        #region Public API

        public void SetHearts(int remaining)
        {
            for (var i = 0; i < hearts.Count; i++)
            {
                hearts[i].color = i < remaining ? fullHeartColor : emptyHeartColor;
            }
        }

        public void SetThrow(int current, int total)
        {
            throwLabel.StringReference = LocalizedStrings.WithArguments(throwText, ("current", current), ("total", total));
        }

        public void ShowKick(int count, int total)
        {
            kickPrompt.SetActive(true);
            kickCountLabel.StringReference = LocalizedStrings.WithArguments(kickCountText, ("count", count), ("total", total));
        }

        public void HideKick()
        {
            kickPrompt.SetActive(false);
        }

        public void ShowSlashHint(bool visible)
        {
            slashHint.SetActive(visible);
        }

        public void FlashHit()
        {
            hitFlashStart = Time.unscaledTime;
            hitFlash.enabled = true;
        }

        public void ShowOut()
        {
            HideKick();
            ShowSlashHint(false);
            outBanner.SetActive(true);
        }

        public void ShowFinished()
        {
            finishedBanner.SetActive(true);
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            if (!hitFlash.enabled) return;
            var t = (Time.unscaledTime - hitFlashStart) / hitFlashSeconds;
            if (t >= 1f)
            {
                hitFlash.enabled = false;
                return;
            }

            var color = hitFlash.color;
            color.a = 0.55f * (1f - t);
            hitFlash.color = color;
        }

        #endregion
    }
}
