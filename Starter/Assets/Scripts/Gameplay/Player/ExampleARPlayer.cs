#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nex
{
    public class ExampleARPlayer : MonoBehaviour
    {
        [SerializeField] int baseSortingOrder = 20;
        [SerializeField] SortingGroup rootSortingGroup = null!;
        [SerializeField] List<PlayerAttachment> playerAttachmentPrefabs = null!;

        int playerIndex;
        OnePlayerPreviewPoseEngine playerPreviewPoseEngine = null!;

        #region Initialization

        public void Initialize(
            int aPlayerIndex,
            OnePlayerPreviewPoseEngine aPlayerPreviewPoseEngine)
        {
            playerIndex = aPlayerIndex;
            playerPreviewPoseEngine = aPlayerPreviewPoseEngine;

            rootSortingGroup.sortingOrder = baseSortingOrder + playerIndex;
            foreach (var prefab in playerAttachmentPrefabs)
            {
                InitializeAttachment(prefab, rootSortingGroup.transform);
            }
        }

        #endregion

        #region Attachments

        void InitializeAttachment(
            PlayerAttachment attachmentPrefab,
            Transform parent)
        {
            var attachment = Instantiate(attachmentPrefab, parent.transform);
            attachment.Initialize(playerPreviewPoseEngine);
        }

        #endregion
    }
}
