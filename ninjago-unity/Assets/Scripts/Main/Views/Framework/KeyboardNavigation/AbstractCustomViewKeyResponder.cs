namespace Nex.KeyboardNavigation
{
    public abstract class AbstractCustomKeyResponder<TView> : KeyResponder where TView : SimpleCanvasView
    {
        protected TView host;

        public static AbstractCustomKeyResponder<TView> Create<TConcreteView>(TView host)
            where TConcreteView : AbstractCustomKeyResponder<TView>
        {
            var responder = host.gameObject.AddComponent<TConcreteView>();
            responder.host = host;
            return responder;
        }
    }
}
