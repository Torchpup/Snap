namespace Snap.Engine.Helpers;

/// <summary>
/// Provides helper methods for converting between tile-based map coordinates
/// and world-space positions.
/// </summary>
/// <remarks>
/// This static utility class includes methods for:
/// <list type="bullet">
///   <item>
///     <description>Mapping grid locations to world-space coordinates (<see cref="MapToWorld"/>).</description>
///   </item>
///   <item>
///     <description>Mapping world-space positions back to grid coordinates (<see cref="WorldToMap"/>).</description>
///   </item>
///   <item>
///     <description>Converting between 1D tile indices and 2D coordinates (<see cref="To2D"/> and <see cref="To1D"/>).</description>
///   </item>
/// </list>
/// These conversions are useful in tile-based games or applications where
/// positions need to be translated between logical grid space and pixel space.
/// </remarks>
public static class MapHelpers
{
	/// <summary>
	/// Converts a tile-based grid location into world-space coordinates.
	/// </summary>
	/// <param name="location">The grid location.</param>
	/// <param name="tilesize">The size of one tile.</param>
	/// <returns>World-space coordinates in pixels.</returns>
	public static Vect2 MapToWorld(in Vect2 location, int tilesize)
		=> Vect2.Floor(location * tilesize);

	/// <summary>
	/// Converts a world-space position into map grid coordinates.
	/// </summary>
	/// <param name="position">World-space position.</param>
	/// <param name="tilesize">The size of one tile.</param>
	/// <returns>Tile-based grid coordinates.</returns>
	public static Vect2 WorldToMap(in Vect2 position, int tilesize)
		=> Vect2.Floor(position / tilesize);

	/// <summary>
	/// Converts a 1‑dimensional tile index into a 2D coordinate.
	/// </summary>
	/// <param name="index">The flat index.</param>
	/// <param name="tilesize">The width (and height) of the tile grid.</param>
	/// <returns>A <see cref="Vect2"/> representing the (x, y) tile position.</returns>
	public static Vect2 To2D(int index, int tilesize) =>
		new(index % tilesize, index / tilesize);

	/// <summary>
	/// Converts a 2D tile coordinate into a 1‑dimensional index.
	/// </summary>
	/// <param name="location">The (x, y) tile position.</param>
	/// <param name="tilesize">The width (and height) of the tile grid.</param>
	/// <returns>The flat index corresponding to <paramref name="location"/>.</returns>
	public static int To1D(Vect2 location, int tilesize) =>
		(int)location.Y * tilesize + (int)location.X;
}
