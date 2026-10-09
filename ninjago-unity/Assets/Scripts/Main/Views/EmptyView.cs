using Cysharp.Threading.Tasks;

namespace Nex
{
    public class EmptyView : View
    {
        public override ViewIdentifier Identifier => ViewIdentifier.Empty;

        public override UniTask Present(bool animate = true)
        {
            return UniTask.CompletedTask;
        }

        public override UniTask Dismiss(bool animate = true)
        {
            return UniTask.CompletedTask;
        }

        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.None;
    }
}
