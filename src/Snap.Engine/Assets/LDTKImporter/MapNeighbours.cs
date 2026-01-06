namespace Snap.Engine.Assets.LDTKImporter;

// N, NE, E, SE, S, SW, W, NW

/// <summary>
/// Represents the possible directions for neighboring map tiles or cells.
/// </summary>
public enum MapNeighbourDirection
{
	/// <summary>
	/// No direction; used when there is no neighboring tile.
	/// </summary>
	None,

	/// <summary>
	/// The tile directly to the north (up).
	/// </summary>
	North,

	/// <summary>
	/// The tile to the northeast (up and right).
	/// </summary>
	NorthEast,

	/// <summary>
	/// The tile directly to the east (right).
	/// </summary>
	East,

	/// <summary>
	/// The tile to the southeast (down and right).
	/// </summary>
	SouthEast,

	/// <summary>
	/// The tile directly to the south (down).
	/// </summary>
	South,

	/// <summary>
	/// The tile to the southwest (down and left).
	/// </summary>
	SouthWest,

	/// <summary>
	/// The tile directly to the west (left).
	/// </summary>
	West,

	/// <summary>
	/// The tile to the northwest (up and left).
	/// </summary>
	NorthWest
}

/// <summary>
/// Represents the neighboring tiles of a map tile, indexed by direction.
/// </summary>
public sealed class MapNeighbour
{
	/// <summary>
	/// Gets the ID of the neighbor to the north, or an empty string if none exists.
	/// </summary>
	public string North => Neighbours.TryGetValue(HashHelpers.Cache32(MapNeighbourDirection.North), out var v) ? v : string.Empty;

	/// <summary>
	/// Gets the ID of the neighbor to the northeast, or an empty string if none exists.
	/// </summary>
	public string NorthEast => Neighbours.TryGetValue(HashHelpers.Cache32(MapNeighbourDirection.NorthEast), out var v) ? v : string.Empty;

	/// <summary>
	/// Gets the ID of the neighbor to the east, or an empty string if none exists.
	/// </summary>
	public string East => Neighbours.TryGetValue(HashHelpers.Cache32(MapNeighbourDirection.East), out var v) ? v : string.Empty;

	/// <summary>
	/// Gets the ID of the neighbor to the southeast, or an empty string if none exists.
	/// </summary>
	public string SouthEast => Neighbours.TryGetValue(HashHelpers.Cache32(MapNeighbourDirection.SouthEast), out var v) ? v : string.Empty;

	/// <summary>
	/// Gets the ID of the neighbor to the south, or an empty string if none exists.
	/// </summary>
	public string South => Neighbours.TryGetValue(HashHelpers.Cache32(MapNeighbourDirection.South), out var v) ? v : string.Empty;

	/// <summary>
	/// Gets the ID of the neighbor to the southwest, or an empty string if none exists.
	/// </summary>
	public string SouthWest => Neighbours.TryGetValue(HashHelpers.Cache32(MapNeighbourDirection.SouthWest), out var v) ? v : string.Empty;

	/// <summary>
	/// Gets the ID of the neighbor to the west, or an empty string if none exists.
	/// </summary>
	public string West => Neighbours.TryGetValue(HashHelpers.Cache32(MapNeighbourDirection.West), out var v) ? v : string.Empty;

	/// <summary>
	/// Gets the ID of the neighbor to the northwest, or an empty string if none exists.
	/// </summary>
	public string NorthWest => Neighbours.TryGetValue(HashHelpers.Cache32(MapNeighbourDirection.NorthWest), out var v) ? v : string.Empty;

	/// <summary>
	/// Gets the read-only dictionary of all neighbor entries, keyed by direction hash.
	/// </summary>
	public IReadOnlyDictionary<uint, string> Neighbours { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="MapNeighbour"/> class with the given neighbor mapping.
	/// </summary>
	/// <param name="neighbours">A dictionary mapping hashed direction values to neighbor IDs.</param>
	public MapNeighbour(Dictionary<uint, string> neighbours) =>
		Neighbours = neighbours;

	internal static MapNeighbour Process(JsonElement e)
	{
		var result = new Dictionary<uint, string>(e.GetArrayLength()); // Pre-allocate
		foreach (var element in e.EnumerateArray())
		{
			(MapNeighbourDirection dir, string id) data = element.GetPropertyOrDefault("dir", string.Empty) switch
			{
				var v when v == "n" => (MapNeighbourDirection.North, element.GetPropertyOrDefault("levelIid", string.Empty)),
				var v when v == "ne" => (MapNeighbourDirection.NorthEast, element.GetPropertyOrDefault("levelIid", string.Empty)),
				var v when v == "e" => (MapNeighbourDirection.East, element.GetPropertyOrDefault("levelIid", string.Empty)),
				var v when v == "se" => (MapNeighbourDirection.SouthEast, element.GetPropertyOrDefault("levelIid", string.Empty)),
				var v when v == "s" => (MapNeighbourDirection.South, element.GetPropertyOrDefault("levelIid", string.Empty)),
				var v when v == "sw" => (MapNeighbourDirection.SouthWest, element.GetPropertyOrDefault("levelIid", string.Empty)),
				var v when v == "w" => (MapNeighbourDirection.West, element.GetPropertyOrDefault("levelIid", string.Empty)),
				var v when v == "nw" => (MapNeighbourDirection.NorthWest, element.GetPropertyOrDefault("levelIid", string.Empty)),
				_ => (MapNeighbourDirection.None, string.Empty)
			};

			if (data.dir == MapNeighbourDirection.None || string.IsNullOrWhiteSpace(data.id))
				continue;

			result[HashHelpers.Cache32(data.dir)] = data.id;
		}

		return new MapNeighbour(result);
	}
}
