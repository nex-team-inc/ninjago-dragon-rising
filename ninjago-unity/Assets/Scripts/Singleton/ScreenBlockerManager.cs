#nullable enable

using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using Nex.Utils;
using UnityEngine;

namespace Nex
{
    public class ScreenBlockerManager : Singleton<ScreenBlockerManager>
    {
        [SerializeField] MMF_Player showAnimator = null!;
        [SerializeField] MMF_Player hideAnimator = null!;

        protected override ScreenBlockerManager GetThis() => this;

        public UniTask Show(bool animate = true)
        {
            gameObject.SetActive(true);
            return showAnimator.PlayAsUniTask(animate: animate);
        }

        public async UniTask Hide(bool animate = true)
        {
            await hideAnimator.PlayAsUniTask(animate: animate);
            gameObject.SetActive(false);
        }
    }
}
