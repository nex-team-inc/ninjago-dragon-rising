namespace Nex.BilliardRogue
{
    /// <summary>
    /// World post-processing cost: Full everywhere except weak mobile GPUs (HD2DVisualConfig.DetectTier), where the
    /// rig enables Volume_LowTier. DebugSettings.renderTier forces either one.
    /// </summary>
    public enum RenderQualityTier
    {
        Full,
        Low,
    }
}
