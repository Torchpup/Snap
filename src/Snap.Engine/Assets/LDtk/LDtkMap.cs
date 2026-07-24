namespace Snap.Engine.Assets.LDTKImporter;

/// <summary>
/// Represents a parsed LDTK project asset, exposing access to levels, layers, entities, and tilesets.
/// Manages internal caches for fast hashed and indexed lookups.
/// </summary>
public sealed class LDtkMap : IAsset
{
	// cachced levels, entities, etc:
	private readonly Dictionary<uint, LDtkLevel> _levelCacheById = [];
	private readonly Dictionary<uint, LDtkLevel> _levelCacheByName = [];
	private readonly Dictionary<ulong, LDtkEntityInstance> _entityCacheById = [];
	private readonly Dictionary<uint, MapLayer> _layerCacheById = [];
	private readonly Dictionary<uint, LDtkTileset> _tilesetCacheById = [];
	private readonly Dictionary<uint, LDtkTileset> _tilesetCacheByName = [];

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

	/// <summary>Gets the last time this LDtk map was accessed. Used by the asset manager for eviction decisions.</summary>
	public DateTime LastAccessTime { get; private set; }

	/// <summary>Gets the raw JSON byte data of the LDtk map file.</summary>
	public byte[] Data { get; private set; }

	internal LDtkMap(byte[] data, uint id, string filename)
	{
		Data = data;
		Id = id;
		Tag = filename;

		LastAccessTime = DateTime.Now;
	}

	/// <summary>
	/// Destructor to clean up unmanaged resources.
	/// </summary>
	~LDtkMap() => Dispose();

	/// <summary>
	/// Loads the project data and parses levels, entities, layers, and tilesets into memory.
	/// </summary>
	/// <returns>The size in bytes of the loaded file.</returns>
	/// <exception cref="FileNotFoundException">Thrown if the LDTK file is not found.</exception>
	public void Load()
	{
		if (IsValid)
		{
			LastAccessTime = DateTime.Now;
			return;
		}

		// byte[] bytes;
		// using (var s = AssetManager.OpenStream(Tag))
		// using (var ms = new MemoryStream())
		// {
		// 	s.CopyTo(ms);
		// 	bytes = ms.ToArray();
		// }

		var doc = JsonDocument.Parse(Data);
		var root = doc.RootElement;

		if (!root.TryGetProperty("defs", out var jDefs))
			throw new InvalidOperationException("Unable to find LDtk Defs");
		if (!jDefs.TryGetProperty("tilesets", out var jTilesets))
			throw new InvalidOperationException("Unable to find LDtk Tilesets");
		if (!root.TryGetProperty("defaultGridSize", out var jDefaultGridSize))
			throw new InvalidOperationException("Unable to find LDtk 'DefaultGridSize'.");
		if (!root.TryGetProperty("levels", out var jLevels))
			throw new InvalidOperationException("Unable to find LDtk 'Levels'.");

		var tilesets = LDtkTileset.Process(jTilesets);
		var levels = LDtkLevel.Process(jLevels, jDefaultGridSize.GetInt32());

		foreach (var tileset in tilesets)
		{
			var tilesetId = tileset.Id;
			var tilesetName = HashHelpers.Cache32(tileset.Name);

			_tilesetCacheById[tilesetId] = tileset;
			_tilesetCacheByName[tilesetName] = tileset;
		}

		foreach (var level in levels)
		{
			var lvlCacheId = HashHelpers.Cache32(level.Id);
			var lvlCacheName = HashHelpers.Cache32(level.Name);

			_levelCacheById[lvlCacheId] = level;
			_levelCacheByName[lvlCacheName] = level;

			foreach (var layer in level.Layers)
			{
				var layerCache = HashHelpers.Cache32(layer.Id);

				_layerCacheById[layerCache] = layer;

				if (layer.Type != LDtkLayerType.Entities)
					continue;

				foreach (var entity in layer.InstanceAs<LDtkEntityInstance>())
				{
					var entityCache = HashHelpers.Cache64(entity.Id);

					_entityCacheById[entityCache] = entity;
				}
			}
		}

		IsValid = true;
		LastAccessTime = DateTime.Now;

		return;
	}

	/// <summary>
	/// Unloads the project and clears all caches.
	/// </summary>
	public void Unload()
	{
		// Don't need to unload anything here. No heavy data here.
	}

	/// <summary>
	/// Disposes the project and releases cached objects and resources.
	/// </summary>
	public void Dispose()
	{
		if (!IsValid)
			return;

		_levelCacheById.Clear();
		_levelCacheByName.Clear();
		_entityCacheById.Clear();
		_layerCacheById.Clear();
		_tilesetCacheById.Clear();
		_tilesetCacheByName.Clear();

		Logger.Instance.Log(LogLevel.Info, $"Unloaded asset with ID {Id}, type: '{GetType().Name}'.");

		GC.SuppressFinalize(this);

		IsValid = false;
	}


	#region Entity
	/// <summary>
	/// Retrieves a map entity instance by its original ID string.
	/// </summary>
	/// <param name="id">The entity ID string.</param>
	/// <returns>The matching <see cref="LDtkEntityInstance"/>.</returns>
	public LDtkEntityInstance GetEntityById(string id)
	{
		if (id.IsEmpty())
			throw new ArgumentNullException(nameof(id));
		var hash = HashHelpers.Cache64(id);
		if (!_entityCacheById.TryGetValue(hash, out var entity))
			throw new Exception($"Unable to find a entity with the id '{id}'.");

		LastAccessTime = DateTime.Now;

		return entity;
	}

	/// <summary>
	/// Attempts to retrieve an entity instance by its identifier.
	/// </summary>
	/// <param name="id">The unique entity identifier to look up.</param>
	/// <param name="value">
	/// When this method returns, contains the resolved <see cref="LDtkEntityInstance"/> 
	/// if the lookup succeeded; otherwise <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the entity was found and returned successfully; 
	/// <c>false</c> if the lookup failed or the identifier does not exist.
	/// </returns>
	public bool TryGetEntityById(string id, out LDtkEntityInstance value)
	{
		try
		{
			value = GetEntityById(id);
			LastAccessTime = DateTime.Now;

			return true;
		}
		catch
		{
			value = null;
			return false;
		}
	}
	#endregion


	#region Layer
	/// <summary>
	/// Retrieves a map layer by its original ID string.
	/// </summary>
	/// <param name="id">The layer ID string.</param>
	/// <returns>The matching <see cref="MapLayer"/>.</returns>
	public MapLayer GetLayerById(string id)
	{
		if (id.IsEmpty())
			throw new ArgumentNullException(nameof(id));
		if (!_layerCacheById.TryGetValue(HashHelpers.Cache32(id), out var layer))
			throw new KeyNotFoundException($"Unable to find a layer with the id '{id}'.");

		LastAccessTime = DateTime.Now;

		return layer;
	}

	/// <summary>
	/// Attempts to retrieve a layer by its identifier.
	/// </summary>
	/// <param name="id">The unique layer identifier to look up.</param>
	/// <param name="value">
	/// When this method returns, contains the resolved <see cref="MapLayer"/> 
	/// if the lookup succeeded; otherwise <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the layer was found and returned successfully;
	/// <c>false</c> if the lookup failed or the identifier does not exist.
	/// </returns>
	public bool TryGetLayerById(string id, out MapLayer value)
	{
		try
		{
			value = GetLayerById(id);
			LastAccessTime = DateTime.Now;

			return true;
		}
		catch
		{
			value = null;
			return false;
		}
	}
	#endregion


	#region Levels
	/// <summary>
	/// Attempts to retrieve a map level using its original string identifier.
	/// </summary>
	/// <param name="id">The string ID assigned to the level in the LDTK project.</param>
	/// <param name="level">
	/// When this method returns, contains the <see cref="LDtkLevel"/> associated with the specified ID,
	/// or <c>null</c> if no matching level is found.
	/// </param>
	/// <returns>
	/// <c>true</c> if a level with the given ID exists; otherwise, <c>false</c>.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if the level cache is empty, indicating that no levels are available to search.
	/// </exception>
	public bool TryGetLevelById(string id, out LDtkLevel level)
	{
		try
		{
			level = GetLevelById(id);
			LastAccessTime = DateTime.Now;

			return level != null;
		}
		catch
		{
			level = null;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a map level using its original string identifier.
	/// </summary>
	/// <param name="id">The string ID assigned to the level in the LDTK project.</param>
	/// <returns>The matching <see cref="LDtkLevel"/> if found; otherwise, <c>null</c>.</returns>
	/// <exception cref="Exception">
	/// Thrown if the level cache is empty, indicating that no levels are available to search.
	/// </exception>
	public LDtkLevel GetLevelById(string id)
	{
		if (id.IsEmpty())
			throw new ArgumentNullException(nameof(id));
		var hash = HashHelpers.Cache32(id);
		if (!_levelCacheById.TryGetValue(hash, out var level))
			throw new KeyNotFoundException($"Unable to find a level with the id '{id}'.");

		LastAccessTime = DateTime.Now;

		return level;
	}

	/// <summary>
	/// Attempts to retrieve a map level by matching its display name.
	/// </summary>
	/// <param name="name">The name of the level to search for.</param>
	/// <param name="level">
	/// When this method returns, contains the <see cref="LDtkLevel"/> with the specified name,
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
	public bool TryGetLevelByName(string name, out LDtkLevel level)
	{
		try
		{
			level = GetLevelByName(name);
			LastAccessTime = DateTime.Now;

			return level != null;
		}
		catch
		{
			level = null;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a map level by matching its display name.
	/// </summary>
	/// <param name="name">The name of the level to search for.</param>
	/// <returns>The <see cref="LDtkLevel"/> with the specified name, or <c>null</c> if not found.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="name"/> is <c>null</c> or empty.
	/// </exception>
	/// <exception cref="Exception">
	/// Thrown if the level list is uninitialized or empty.
	/// </exception>
	public LDtkLevel GetLevelByName(string name)
	{
		if (name.IsEmpty())
			throw new ArgumentNullException(nameof(name), "Is null or empty");
		var hash = HashHelpers.Cache32(name);
		if (!_levelCacheByName.TryGetValue(hash, out var level))
			throw new KeyNotFoundException($"Unable to find a level with the name '{name}'.");

		LastAccessTime = DateTime.Now;

		return level;
	}
	#endregion


	#region Tileset
	/// <summary>
	/// Retrieves a tileset from the project by its numeric identifier index.
	/// </summary>
	/// <param name="id">The tileset's internal index value, as defined in the LDTK project.</param>
	/// <returns>The <see cref="LDtkTileset"/> associated with the given index.</returns>
	/// <exception cref="Exception">
	/// Thrown if the tileset cache is uninitialized or if no tileset matches the specified index.
	/// </exception>
	public LDtkTileset GetTilesetId(uint id)
	{
		if (!_tilesetCacheById.TryGetValue(id, out var tilemap))
			throw new Exception($"Unable to find a tileset with the id '{id}'.");

		LastAccessTime = DateTime.Now;

		return tilemap;
	}

	/// <summary>
	/// Attempts to retrieve a tileset by its numeric identifier.
	/// </summary>
	/// <param name="id">The unique tileset identifier to look up.</param>
	/// <param name="value">
	/// When this method returns, contains the resolved <see cref="LDtkTileset"/>
	/// if the lookup succeeded; otherwise <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the tileset was found and returned successfully;
	/// <c>false</c> if the lookup failed or the identifier does not exist.
	/// </returns>
	public bool TryGetTilesetId(uint id, out LDtkTileset value)
	{
		try
		{
			value = GetTilesetId(id);
			LastAccessTime = DateTime.Now;

			return true;
		}
		catch
		{
			value = null;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a tileset from the project by matching its name.
	/// </summary>
	/// <param name="name">The name of the tileset as defined in the project.</param>
	/// <returns>The matching <see cref="LDtkTileset"/> instance.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="name"/> is null or an empty string.
	/// </exception>
	/// <exception cref="Exception">
	/// Thrown if the tileset cache is uninitialized or no tileset with the given name is found.
	/// </exception>
	public LDtkTileset GetTilesetByName(string name)
	{
		if (name.IsEmpty())
			throw new ArgumentNullException(nameof(name), "Is null or empty");
		var hash = HashHelpers.Cache32(name);
		if (!_tilesetCacheByName.TryGetValue(hash, out var tileset))
			throw new Exception($"Unable to find a tileset with the name '{name}'.");

		LastAccessTime = DateTime.Now;

		return tileset;
	}

	/// <summary>
	/// Attempts to retrieve a tileset by its name.
	/// </summary>
	/// <param name="name">The tileset name to look up.</param>
	/// <param name="value">
	/// When this method returns, contains the resolved <see cref="LDtkTileset"/>
	/// if the lookup succeeded; otherwise <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the tileset was found and returned successfully;
	/// <c>false</c> if the lookup failed or the name does not exist.
	/// </returns>
	public bool TryGetTilesetByName(string name, out LDtkTileset value)
	{
		try
		{
			value = GetTilesetByName(name);
			LastAccessTime = DateTime.Now;
			return true;
		}
		catch
		{
			value = null;
			return false;
		}
	}
	#endregion
}

