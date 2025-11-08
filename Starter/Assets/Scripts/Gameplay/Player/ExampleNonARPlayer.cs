#nullable enable

using System.Collections.Generic;
using Jazz;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nex
{
    public class ExampleNonARPlayer : MonoBehaviour
    {
        [SerializeField] int baseSortingOrder = 20;
        [SerializeField] SortingGroup rootSortingGroup = null!;
        [SerializeField] List<PlayerAttachment> playerAttachmentPrefabs = null!;
        [SerializeField] PlayerStyleController playerStyleController = null!;
        [SerializeField] PlayerPreviewSpritesController playerPreviewSpritesController = null!;

        int playerIndex;
        OnePlayerDetectionEngine playerDetectionEngine = null!;

        #region Initialization

        public void Initialize(
            int aPlayerIndex,
            OnePlayerDetectionEngine aPlayerDetectionEngine,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager)
        {
            playerIndex = aPlayerIndex;
            playerDetectionEngine = aPlayerDetectionEngine;

            rootSortingGroup.sortingOrder = baseSortingOrder + playerIndex;
            foreach (var prefab in playerAttachmentPrefabs)
            {
                InitializeAttachment(prefab, rootSortingGroup.transform);
            }

            playerStyleController.Initialize(aPlayerIndex);
            playerPreviewSpritesController.Initialize(playerIndex, aCvDetectionManager, aBodyPoseDetectionManager);
        }

        #endregion

        #region Attachments

        void InitializeAttachment(
            PlayerAttachment attachmentPrefab,
            Transform parent)
        {
            var attachment = Instantiate(attachmentPrefab, parent.transform);
            attachment.Initialize(playerDetectionEngine);
        }

        #endregion
    }
}
