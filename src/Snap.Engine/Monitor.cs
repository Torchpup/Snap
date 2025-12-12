namespace Snap.Engine;

/// <summary>
/// Represents a display monitor's resolution and aspect ratio.
/// </summary>
/// <remarks>
/// This struct is typically used to describe the desktop monitor or a supported
/// fullscreen resolution when enumerating available display modes.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="Monitor"/> struct.
/// </remarks>
/// <param name="width">The width of the monitor in pixels.</param>
/// <param name="height">The height of the monitor in pixels.</param>
public readonly struct Monitor(int width, int height)
{
	/// <summary>
	/// Gets the width of the monitor, in pixels.
	/// </summary>
	public int Width { get; } = width;

	/// <summary>
	/// Gets the height of the monitor, in pixels.
	/// </summary>
	public int Height { get; } = height;

	/// <summary>
	/// Gets the aspect ratio of the monitor as a floating-point value (<c>width / height</c>).
	/// </summary>
	public float Ratio { get; } = (float)width / height;
}