namespace Nex
{
    public class WelcomeScreenView : SimpleCanvasView
    {
        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.WelcomeScreen;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Exit;

        #endregion
    }
}
