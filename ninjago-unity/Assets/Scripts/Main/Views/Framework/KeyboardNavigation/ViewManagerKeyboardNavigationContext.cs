#nullable enable

namespace Nex.KeyboardNavigation
{
    public class ViewManagerKeyboardNavigationContext : IKeyboardNavigationContext
    {
        readonly ViewManager viewManager;

        public ViewManagerKeyboardNavigationContext(ViewManager viewManager)
        {
            this.viewManager = viewManager;
        }

        internal KeyResponder? GetTopLevelControlKeyResponder(TopLevelControlPanel.ControlConfig control)
        {
            return viewManager.GetTopLevelControlKeyResponder(control);
        }
    }
}
