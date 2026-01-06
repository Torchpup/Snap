namespace Snap.Engine.Assets.LDTKImporter;

/// <summary>
/// Represents a parsed LDTK project asset, exposing access to levels, layers, entities, and tilesets.
/// Manages internal caches for fast hashed and indexed lookups.
/// </summary>
public sealed class LDTKProject : IAsset
{
	// cachced levels, entities, etc:
	private Dictionary<uint, MapLevel> _levelCache = [];
	private readonly Dictionary<uint, MapEntityInstance> _entityCache = [];
	private readonly Dictionary<uint, MapLayer> _layerCache = [];
	private Dictionary<int, MapTileset> _tilesetCache = [];
	private List<MapLevel> _levels;

	/// <summary>
	/// Unique identifier for this LDTK project asset.
	/// </summary>
	public uint Id { get; }

	/// <summary>
	/// File path or tag used to locate the LDTK project JSON file.
	/// </summary>
	public string Tag { get; }

	/// <summary>
	/// Indicates whether the project has been successfully loaded into memory.
	/// </summary>
	public bool IsValid { get; private set; }

	/// <summary>
	/// Returns a native resource handle if applicable. Value is implementation-specific.
	/// </summary>
	public uint Handle { get; }

	/// <summary>
	/// Gets a read-only list of all levels defined in the project.
	/// </summary>
	public IReadOnlyList<MapLevel> Levels => _levels;

	/// <summary>
	/// Gets a read-only dictionary of tilesets defined in the project, keyed by internal index.
	/// </summary>
	public IReadOnlyDictionary<int, MapTileset> Tilesets => _tilesetCache;

	internal LDTKProject(uint id, string filename)
	{
		Id = id;
		Tag = filename;
	}

	/// <summary>
	/// Destructor to clean up unmanaged resources.
	/// </summary>
	~LDTKProject() => Dispose();

	/// <summary>
	/// Loads the project data and parses levels, entities, layers, and tilesets into memory.
	/// </summary>
	/// <returns>The size in bytes of the loaded file.</returns>
	/// <exception cref="FileNotFoundException">Thrown if the LDTK file is not found.</exception>
	public ulong Load()
	{
		if (IsValid)
			return 0u;

		byte[] bytes;
		using (var s = AssetManager.OpenStream(Tag))
		using (var ms = new MemoryStream())
		{
			s.CopyTo(ms);
			bytes = ms.ToArray();
		}
		
		var doc = JsonDocument.Parse(bytes);
		var root = doc.RootElement;

		var defs = root.GetProperty("defs");
		var defTilesets = defs.GetProperty("tilesets");

		_tilesetCache = MapTileset.Process(defTilesets);
		_levels = MapLevel.Process(root.GetProperty("levels"),
			root.GetPropertyOrDefault<int>("defaultGridSize"));

		_levelCache = new Dictionary<uint, MapLevel>(_levels.Count);
		for (int i = 0; i < _levels.Count; i++)
		{
			var level = _levels[i];

			_levelCache[HashHelpers.Cache32(level.Id)] = level;

			for (int x = 0; x < level.Layers.Count; x++)
			{
				var layer = level.Layers[x];

				_layerCache[HashHelpers.Cache32(layer.Id)] = layer;

				if (layer.Type == MapLayerType.Entities)
				{
					var instances = layer.InstanceAs<MapEntityInstance>();
					for (int z = 0; z < instances.Count; z++)
					{
						var entity = instances[z];

						_entityCache[HashHelpers.Cache32(entity.Id)] = entity;
					}
				}
			}
		}

		IsValid = true;

		return (ulong)bytes.Length;
	}

	/// <summary>
	/// Unloads the project and clears all caches.
	/// </summary>
	public void Unload()
	{
		if (!IsValid)
			return;

		Dispose();

		IsValid = false;
	}

	/// <summary>
	/// Disposes the project and releases cached objects and resources.
	/// </summary>
	public void Dispose()
	{
		_levels?.Clear();
		_levelCache?.Clear();
		_entityCache?.Clear();
		_layerCache?.Clear();

		Logger.Instance.Log(LogLevel.Info, $"Unloaded asset with ID {Id}, type: '{GetType().Name}'.");
	}


	#region Entity
	/// <summary>
	/// Retrieves a map entity instance using its hashed ID.
	/// </summary>
	/// <param name="hash">The hashed entity identifier.</param>
	/// <returns>The matching <see cref="MapEntityInstance"/>.</returns>
	public MapEntityInstance GetEntityByHash(uint hash)
	{
		if (_entityCache.Count == 0)
			throw new Exception("You don't have any entities to search");

		if (!_entityCache.TryGetValue(hash, out var entity))
			throw new Exception($"Unable to find a map entity with the hash id '{hash}'.");

		return entity;
	}

	/// <summary>
	/// Retrieves a map entity instance by its original ID string.
	/// </summary>
	/// <param name="id">The entity ID string.</param>
	/// <returns>The matching <see cref="MapEntityInstance"/>.</returns>
	public MapEntityInstance GetEntityById(string id)
	{
		if (_entityCache.Count == 0)
			throw new Exception("You don't have any entities to search");

		if (!_entityCache.TryGetValue(HashHelpers.Cache32(id), out var entity))
			throw new Exception($"Unable to find a entity with the id '{id}'.");

		return entity;
	}
	#endregion


	#region Layer
	/// <summary>
	/// Retrieves a map layer by its hashed ID.
	/// </summary>
	/// <param name="hash">The hashed layer ID.</param>
	/// <returns>The matching <see cref="MapLayer"/>.</returns>
	public MapLayer GetLayerByHash(uint hash)
	{
		if (_layerCache.Count == 0)
			throw new Exception("You don't have any layers to search");

		if (!_layerCache.TryGetValue(hash, out var layer))
			throw new Exception($"Unable to find a map layer with the hash id '{hash}'.");

		return layer;
	}

	/// <summary>
	/// Retrieves a map layer by its original ID string.
	/// </summary>
	/// <param name="id">The layer ID string.</param>
	/// <returns>The matching <see cref="MapLayer"/>.</returns>
	public MapLayer GetLayerById(string id)
	{
		if (_layerCache.Count == 0)
			throw new Exception("You don't have any layers to search");

		if (!_layerCache.TryGetValue(HashHelpers.Cache32(id), out var layer))
			throw new Exception($"Unable to find a layer with the id '{id}'.");

		return layer;
	}
	#endregion


	#region Levels
	/// <summary>
	/// Attempts to retrieve a map level using its hashed identifier.
	/// </summary>
	/// <param name="hash">The 32-bit hash of the level ID.</param>
	/// <param name="level">
	/// When this method returns, contains the <see cref="MapLevel"/> associated with the specified hash,
	/// or <c>null</c> if no matching level is found.
	/// </param>
	/// <returns>
	/// <c>true</c> if a level matching the hash exists; otherwise, <c>false</c>.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if the level cache is empty, indicating that no levels are available to search.
	/// </exception>
	public bool TryGetLevelByHash(uint hash, out MapLevel level)
	{
		level = GetLevelByHash(hash);

		return level != null;
	}

	/// <summary>
	/// Retrieves a map level using its hashed identifier.
	/// </summary>
	/// <param name="hash">The 32-bit hash of the level ID.</param>
	/// <returns>The matching <see cref="MapLevel"/> if found; otherwise, <c>null</c>.</returns>
	/// <exception cref="Exception">
	/// Thrown if the level cache is empty, indicating that no levels are available to search.
	/// </exception>
	public MapLevel GetLevelByHash(uint hash)
	{
		if (_layerCache.Count == 0)
			throw new Exception("You don't have any map levels to search");

		if (!_levelCache.TryGetValue(hash, out var level))
			return null;

		return level;
	}

	/// <summary>
	/// Attempts to retrieve a map level using its original string identifier.
	/// </summary>
	/// <param name="id">The string ID assigned to the level in the LDTK project.</param>
	/// <param name="level">
	/// When this method returns, contains the <see cref="MapLevel"/> associated with the specified ID,
	/// or <c>null</c> if no matching level is found.
	/// </param>
	/// <returns>
	/// <c>true</c> if a level with the given ID exists; otherwise, <c>false</c>.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if the level cache is empty, indicating that no levels are available to search.
	/// </exception>
	public bool TryGetLevelById(string id, out MapLevel level)
	{
		level = GetLevelById(id);

		return level != null;
	}

	/// <summary>
	/// Retrieves a map level using its original string identifier.
	/// </summary>
	/// <param name="id">The string ID assigned to the level in the LDTK project.</param>
	/// <returns>The matching <see cref="MapLevel"/> if found; otherwise, <c>null</c>.</returns>
	/// <exception cref="Exception">
	/// Thrown if the level cache is empty, indicating that no levels are available to search.
	/// </exception>
	public MapLevel GetLevelById(string id)
	{
		if (_layerCache.Count == 0)
			throw new Exception("You don't have any map levels to search");

		if (string.IsNullOrWhiteSpace(id))
			return null;
		if (!_levelCache.TryGetValue(HashHelpers.Cache32(id), out var level))
			return null;

		return level;
	}

	/// <summary>
	/// Attempts to retrieve a map level by matching its display name.
	/// </summary>
	/// <param name="name">The name of the level to search for.</param>
	/// <param name="ignoreCase">
	/// If <c>true</c>, performs a case-insensitive comparison; otherwise, uses case-sensitive matching.
	/// </param>
	/// <param name="level">
	/// When this method returns, contains the <see cref="MapLevel"/> with the specified name,
	/// or <c>null</c> if no matching level is found.
	/// </param>
	/// <returns>
	/// <c>true</c> if a level with the given name exists; otherwise, <c>false</c>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="name"/> is <c>null</c> or empty.
	/// </exception>
	/// <exception cref="Exception">
	/// Thrown if the level list is uninitialized or empty.
	/// </exception>
	public bool TryGetLevelByName(string name, bool ignoreCase, out MapLevel level)
	{
		level = GetLevelByName(name, ignoreCase);

		return level != null;
	}

	/// <summary>
	/// Retrieves a map level by matching its display name.
	/// </summary>
	/// <param name="name">The name of the level to search for.</param>
	/// <param name="ignoreCase">
	/// If <c>true</c>, performs a case-insensitive comparison; otherwise, uses case-sensitive matching.
	/// </param>
	/// <returns>The <see cref="MapLevel"/> with the specified name, or <c>null</c> if not found.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="name"/> is <c>null</c> or empty.
	/// </exception>
	/// <exception cref="Exception">
	/// Thrown if the level list is uninitialized or empty.
	/// </exception>
	public MapLevel GetLevelByName(string name, bool ignoreCase)
	{
		if (name.IsEmpty())
			throw new ArgumentNullException(nameof(name), "Is null or empty");
		if (_tilesetCache.Count == 0)
			throw new Exception("You don't have any map levels to search");

		var type = ignoreCase
			? StringComparison.OrdinalIgnoreCase
			: StringComparison.Ordinal;

		foreach (var lv in _levels)
		{
			if (lv.Name.Equals(name, type))
				return lv;
		}

		return null;
	}
	#endregion


	#region Tileset
	/// <summary>
	/// Retrieves a tileset from the project by its numeric identifier index.
	/// </summary>
	/// <param name="index">The tileset's internal index value, as defined in the LDTK project.</param>
	/// <returns>The <see cref="MapTileset"/> associated with the given index.</returns>
	/// <exception cref="Exception">
	/// Thrown if the tileset cache is uninitialized or if no tileset matches the specified index.
	/// </exception>
	public MapTileset GetTileset(int index)
	{
		if (_tilesetCache.Count == 0)
			throw new Exception("You don't have any tilesets to search");
		if (!_tilesetCache.TryGetValue(index, out var tilemap))
			throw new Exception($"Unable to find a tileset with the id '{index}'.");
		return tilemap;
	}

	/// <summary>
	/// Retrieves a tileset from the project by matching its name.
	/// </summary>
	/// <param name="name">The name of the tileset as defined in the project.</param>
	/// <param name="ignoreCase">
	/// If <c>true</c>, performs a case-insensitive comparison; otherwise, name matching is case-sensitive.
	/// </param>
	/// <returns>The matching <see cref="MapTileset"/> instance.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="name"/> is null or an empty string.
	/// </exception>
	/// <exception cref="Exception">
	/// Thrown if the tileset cache is uninitialized or no tileset with the given name is found.
	/// </exception>
	public MapTileset GetTilesetByName(string name, bool ignoreCase)
	{
		if (name.IsEmpty())
			throw new ArgumentNullException(nameof(name), "Is null or empty");
		if (_tilesetCache.Count == 0)
			throw new Exception("You don't have any tilesets to search");

		var type = ignoreCase
			? StringComparison.OrdinalIgnoreCase
			: StringComparison.Ordinal;

		foreach (var kv in _tilesetCache)
		{
			if (kv.Value.Name.Equals(name, type))
				return kv.Value;
		}

		throw new Exception($"Unable to find a tileset called '{name}'.");
	}
	#endregion
}
