#nullable enable

using System.Collections.Generic;
using Nex.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>One player's half of the fight HUD: tag, hearts, sweep count, safe-side arrow, hints, out banner.</summary>
    public class FightHud : MonoBehaviour
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
        [Header("Sweep Label")]
        [SerializeField] NexLocalizedString sweepLabel = null!;
        [Header("Left Safe Arrow")]
        [SerializeField] GameObject leftArrow = null!;
        [Header("Right Safe Arrow")]
        [SerializeField] GameObject rightArrow = null!;
        [Header("Left Slip Hint")]
        [SerializeField] GameObject leftSlipHint = null!;
        [Header("Right Slip Hint")]
        [SerializeField] GameObject rightSlipHint = null!;
        [Header("Spin Hint")]
        [SerializeField] GameObject spinHint = null!;
        [Header("Out Banner")]
        [SerializeField] GameObject outBanner = null!;
        [Header("Finished Banner")]
        [SerializeField] GameObject finishedBanner = null!;

        readonly List<Image> hearts = new();

        #region Initialization

        public void Initialize(int playerIndex, Color color, int maxHearts)
        {
            playerTag.Initialize(playerIndex, color);
            for (var i = 0; i < maxHearts; i++)
            {
                hearts.Add(Instantiate(heartPrefab, heartsRow));
            }

            SetHearts(maxHearts);
            HideSafeSide();
            ShowSpinHint(false);
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

        public void SetSweep(int current, int total)
        {
            sweepLabel.SetSmartStringArgument("current", current);
            sweepLabel.SetSmartStringArgument("total", total);
        }

        public void ShowSafeSide(SweepSide safeSide, bool withSlipHint)
        {
            leftArrow.SetActive(safeSide == SweepSide.Left);
            rightArrow.SetActive(safeSide == SweepSide.Right);
            leftSlipHint.SetActive(withSlipHint && safeSide == SweepSide.Left);
            rightSlipHint.SetActive(withSlipHint && safeSide == SweepSide.Right);
        }

        public void HideSafeSide()
        {
            leftArrow.SetActive(false);
            rightArrow.SetActive(false);
        }

        public void ShowSpinHint(bool visible)
        {
            spinHint.SetActive(visible);
        }

        public void ShowOut()
        {
            HideSafeSide();
            outBanner.SetActive(true);
        }

        public void ShowFinished()
        {
            finishedBanner.SetActive(true);
        }

        #endregion
    }
}
