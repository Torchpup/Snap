namespace Snap.Engine.Enums;

/// <summary>
/// Specifies which layers of debug shapes should be rendered.
/// </summary>
public enum DebugDrawMode
{
    /// <summary>
    /// Disables debug shape rendering entirely.
    /// </summary>
    None,

    /// <summary>
    /// Renders debug shapes only on the topmost visual layer, above all other content.
    /// </summary>
    TopMost,

    /// <summary>
    /// Renders debug shapes only on the bottommost visual layer, behind all other content.
    /// </summary>
    BottomMost,

    /// <summary>
    /// Renders debug shapes on all layers.
    /// </summary>
    All,
}
