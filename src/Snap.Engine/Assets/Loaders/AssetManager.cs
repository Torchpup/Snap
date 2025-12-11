using Snap.Content.Abstractions;
using Snap.Content.Abstractions.Interfaces;

namespace Snap.Engine.Assets.Loaders;

/// <summary>
/// Global manager for loading, accessing, and tracking disposable assets across the project.
/// Supports caching, lifetime tracking, and type-safe retrieval.
/// </summary>
public sealed class AssetManager
{
	private sealed class AssetEntry
	{
		public IAsset Asset { get; }
		public DateTime LastAccessFrame { get; set; }
		public ulong Length { get; set; }

		public AssetEntry(IAsset asset)
		{
			Asset = asset;
			LastAccessFrame = DateTime.UtcNow;
		}
	}

	private const long EvictAfterMinutes = 15;

	private static readonly object _gate = new();
	private static IContentProvider _provider;
	private static readonly ConcurrentDictionary<string, byte[]> _byteCache = new(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<uint, AssetEntry> _assets = new(32);

	private static readonly string[] TextureExtentions = {
		".png", ".bmp", ".tga", ".jpg", ".gif", ".psd", ".hdr", ".pic", ".pnm" };
	private static readonly string[] SpriteFontExtentions = {
		".png", ".bmp", ".tga", ".jpg", ".gif", ".psd", ".hdr", ".pic", ".pnm" };
	private static readonly string[] LDTKExtentions = { ".ldtk", ".json" };
	private static readonly string[] SpritesheetExtentions = { ".sheet", ".json" };
	private static readonly string[] SoundExtentions = {
		".wav", ".mp3", ".ogg", ".flac", ".aiff", ".au", ".raw", ".paf", ".svx",
		".nist", ".voc", ".ircam", ".w64", ".mat4", ".mat5", ".pvf", ".htk",
		".sds", ".avr", ".sd2", ".caf", ".wve", ".mpc2k", ".rf64"
	};

	internal static uint Id { get; set; } = 1u;

	/// <summary>
	/// Gets the singleton instance of the <see cref="AssetManager"/>.
	/// Automatically initialized on first access.
	/// </summary>
	public static AssetManager Instance { get; private set; }

	/// <summary>
	/// Gets the total number of assets tracked, including those not yet loaded.
	/// </summary>
	public int TotalCount => _assets.Count;

	/// <summary>
	/// Gets the cumulative number of bytes loaded across all assets since launch.
	/// </summary>
	public long BytesLoaded { get; private set; }

	/// <summary>
	/// Gets the number of assets currently marked as valid (loaded and usable).
	/// </summary>
	public int Count => _assets.Count(x => x.Value.Asset.IsValid);

	/// <summary>
	/// Initializes the singleton instance of the asset manager if not already created.
	/// </summary>
	internal AssetManager()
	{
		Instance ??= this;
	}

	/// <summary>Install the active content provider (pak, FS, composite…).</summary>
	public static void SetProvider(IContentProvider provider, bool disposeOld = true, bool clearCaches = true)
	{
		ArgumentNullException.ThrowIfNull(provider);

		lock (_gate)
		{
			var old = _provider;
			_provider = provider;

			if (clearCaches) _byteCache.Clear();

			if (disposeOld && !ReferenceEquals(old, provider) && old is IDisposable d)
				d.Dispose();
		}
	}

	/// <summary>Check if a logical asset path exists (e.g. "images/ui/logo.png").</summary>
	public static bool Exists(string path) => _provider.Exists(Norm(path));

	/// <summary>Open raw bytes for an asset. Caller owns the returned stream.</summary>
	public static Stream OpenStream(string path) => _provider.OpenRead(Norm(path));

	/// <summary>Load whole file bytes (with a simple cache).</summary>
	public static byte[] LoadBytes(string path)
	{
		path = Norm(path);
		return _byteCache.GetOrAdd(path, p =>
		{
			using var s = _provider.OpenRead(p);
			using var ms = new MemoryStream();
			s.CopyTo(ms);
			return ms.ToArray();
		});
	}

	/// <summary>List logical paths under a folder (merged view if composite).</summary>
	public static IEnumerable<string> List(string folder) => _provider.List(Norm(folder));

	/// <summary>Clear in-memory caches (use after hot-swapping packs).</summary>
	public static void ClearCaches()
	{
		_byteCache.Clear();
		if (_provider is CompositeContentProvider cc) cc.ClearCache();
	}

	private static string Norm(string p) => p.Replace('\\', '/').TrimStart('/');












	/// <summary>
	/// Adds a new asset to the manager using an enum key.
	/// Throws if an asset with the same name already exists.
	/// </summary>
	/// <param name="name">An enum value representing the asset’s key.</param>
	/// <param name="asset">The asset to register.</param>
	/// <exception cref="InvalidOperationException">Thrown if the asset key already exists.</exception>
	public void Add(Enum name, IAsset asset) => Add(name.ToEnumString(), asset);

	/// <summary>
	/// Adds a new asset to the manager using a string key.
	/// Throws if an asset with the same name already exists.
	/// </summary>
	/// <param name="name">The string identifier for the asset.</param>
	/// <param name="asset">The asset to register.</param>
	/// <exception cref="InvalidOperationException">Thrown if the asset key already exists.</exception>
	public void Add(string name, IAsset asset)
	{
		var hash = HashHelpers.Hash32(name);
		if (_assets.ContainsKey(hash))
			throw new InvalidOperationException($"An asset with the name '{name}' already exists.");

		_assets[hash] = new AssetEntry(asset);
	}

	/// <summary>
	/// Removes an asset from the manager using an enum key.
	/// </summary>
	/// <param name="name">The enum identifier of the asset to remove.</param>
	public void Remove(Enum name) => Remove(name.ToEnumString());

	/// <summary>
	/// Removes an asset from the manager using a string key.
	/// </summary>
	/// <param name="name">The string identifier of the asset to remove.</param>
	public void Remove(string name) =>
		InternalRemove(HashHelpers.Hash32(name), true);

	/// <summary>
	/// Retrieves and lazily loads an asset using an enum key.
	/// </summary>
	/// <typeparam name="T">The expected type of the asset.</typeparam>
	/// <param name="name">The enum identifier of the asset.</param>
	/// <returns>The loaded and valid asset instance.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if no asset matches the key.</exception>
	public T Get<T>(Enum name) where T : IAsset => Get<T>(name.ToEnumString());

	/// <summary>
	/// Retrieves and lazily loads an asset using a string key.
	/// </summary>
	/// <typeparam name="T">The expected type of the asset.</typeparam>
	/// <param name="name">The string name of the asset.</param>
	/// <returns>The loaded and valid asset instance.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if no asset matches the name.</exception>
	public T Get<T>(string name) where T : IAsset
	{
		var hash = HashHelpers.Hash32(name);
		if (!_assets.TryGetValue(hash, out var entry))
			throw new KeyNotFoundException($"No asset found for '{name}'.");

		entry.LastAccessFrame = DateTime.UtcNow;

		EvictAssets();

		if (!entry.Asset.IsValid)
		{
			entry.Length = entry.Asset.Load();
			BytesLoaded += (long)entry.Length;

			Logger.Instance.Log(LogLevel.Info, $"Loaded asset with ID: {entry.Asset.Id}, type: '{entry.Asset.GetType().Name}'.");
		}

		return (T)entry.Asset;
	}

	/// <summary>
	/// Tries to retrieve and load an asset using an enum key.
	/// </summary>
	/// <typeparam name="T">The expected type of the asset.</typeparam>
	/// <param name="name">The enum identifier for the asset.</param>
	/// <param name="asset">The resulting asset, or <c>null</c> if not found.</param>
	/// <returns><c>true</c> if the asset was successfully retrieved; otherwise <c>false</c>.</returns>
	public bool TryGet<T>(Enum name, out T asset) where T : IAsset =>
		TryGet(name.ToEnumString(), out asset);

	/// <summary>
	/// Tries to retrieve and load an asset using a string key.
	/// </summary>
	/// <typeparam name="T">The expected type of the asset.</typeparam>
	/// <param name="name">The string key for the asset.</param>
	/// <param name="asset">The resulting asset, or <c>null</c> if not found.</param>
	/// <returns><c>true</c> if the asset was successfully retrieved; otherwise <c>false</c>.</returns>
	public bool TryGet<T>(string name, out T asset) where T : IAsset
	{
		asset = Get<T>(name);

		return asset != null;
	}

	/// <summary>
	/// Unloads all tracked assets from memory without removing their references from the manager.
	/// Useful for freeing memory while preserving asset metadata for potential reload.
	/// </summary>
	public void UnloadAll()
	{
		foreach (var item in _assets)
			InternalRemove(item.Key, false);
	}

	internal void Clear()
	{
		foreach (var item in _assets)
			InternalRemove(item.Key, true);
		_assets.Clear();
	}

	#region Loaders
	/// <summary>
	/// Loads a texture asset from the specified file.
	/// </summary>
	/// <param name="filename">The texture file name or relative path (extension optional).</param>
	/// <param name="repeat">Whether the texture should wrap/repeat.</param>
	/// <param name="smooth">Whether to apply smoothing/filtering.</param>
	/// <returns>The loaded <see cref="Texture"/> instance.</returns>
	/// <exception cref="FileNotFoundException">Thrown if the texture file is not found.</exception>
	public static Texture LoadTexture(string filename, bool repeat = false, bool smooth = false)
	{
		if (!TryResolveLogicalPath(filename, TextureExtentions, out var path))
			throw new FileNotFoundException($"Asset not found: {filename}");

		return new Texture(Id++, path, repeat, smooth);
	}

	/// <summary>
	/// Loads a sprite font from the specified file path with optional spacing and rendering settings.
	/// </summary>
	/// <param name="filename">The path to the image file associated with the sprite font.</param>
	/// <param name="spacing">Optional horizontal spacing added between characters. Default is 0.</param>
	/// <param name="lineSpacing">Optional vertical spacing added between lines. Default is 0.</param>
	/// <param name="smoothing">Whether smoothing/antialiasing should be applied to the font rendering.</param>
	/// <param name="charList">
	/// Optional list of characters to include. If null, the full default character set will be used.
	/// This can be used to limit the font to specific characters to save memory or processing time.
	/// </param>
	/// <returns>The loaded <see cref="SpriteFont"/> instance.</returns>
	/// <exception cref="FileNotFoundException">Thrown if the specified file could not be found.</exception>
	public static SpriteFont LoadSpriteFont(string filename, float spacing = 0f, float lineSpacing = 0f,
		bool smoothing = false, string charList = null)
	{
		if (!TryResolveLogicalPath(filename, SpriteFontExtentions, out var fullPath))
			throw new FileNotFoundException($"Sprite font file '{filename}' could not be found.");

		return new SpriteFont(Id++, fullPath, spacing, lineSpacing, smoothing, charList);
	}

	/// <summary>
	/// Loads an LDTK map project from disk.
	/// </summary>
	/// <param name="filename">The path to the map file (.ldtk or .json).</param>
	/// <returns>The parsed <see cref="LDTKProject"/> instance.</returns>
	/// <exception cref="FileNotFoundException">Thrown if the file is not found.</exception>
	public static LDTKProject LoadMap(string filename)
	{
		if (!TryResolveLogicalPath(filename, LDTKExtentions, out var fullPath))
			throw new FileNotFoundException($"Map file '{filename}' could not be found.");

		return new LDTKProject(Id++, fullPath);
	}

	/// <summary>
	/// Loads a spritesheet definition from disk.
	/// </summary>
	/// <param name="filename">The spritesheet file name (.sheet or .json).</param>
	/// <returns>The loaded <see cref="Spritesheet"/> object.</returns>
	/// <exception cref="FileNotFoundException">Thrown if the file is not found.</exception>
	public static Spritesheet LoadSheet(string filename)
	{
		if (!TryResolveLogicalPath(filename, SpritesheetExtentions, out var fullPath))
			throw new FileNotFoundException($"Spritesheet file '{filename}' could not be found.");

		return new Spritesheet(Id++, fullPath);
	}

	/// <summary>
	/// Loads a sound asset from the specified file.
	/// </summary>
	/// <param name="filename">Path to the audio file.</param>
	/// <param name="looped">Whether the sound should loop when played.</param>
	/// <returns>The loaded <see cref="Sound"/> instance.</returns>
	/// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
	public static Sound LoadSound(string filename, bool looped = false)
	{
		if (!TryResolveLogicalPath(filename, SoundExtentions, out var fullPath))
			throw new FileNotFoundException($"Sound file '{filename}' could not be found.");

		return new Sound(Id++, fullPath, looped);
	}
	#endregion


	#region Getters:
	/// <summary>
	/// Retrieves a texture asset by string key.
	/// </summary>
	/// <param name="name">The registered name of the texture.</param>
	/// <returns>The requested <see cref="Texture"/> asset.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if the texture is not registered.</exception>
	public static Texture GetTexture(string name) => Instance.Get<Texture>(name);

	/// <summary>
	/// Retrieves a texture asset by enum key.
	/// </summary>
	/// <param name="name">An enum value converted to string as the asset name.</param>
	/// <returns>The requested <see cref="Texture"/> asset.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if the texture is not registered.</exception>
	public static Texture GetTexture(Enum name) => Instance.Get<Texture>(name);
	/// <summary>
	/// Attempts to retrieve a texture asset by name.
	/// </summary>
	/// <param name="name">The texture key.</param>
	/// <param name="asset">The output asset reference, or <c>null</c> if not found.</param>
	/// <returns><c>true</c> if the texture exists and was returned; otherwise <c>false</c>.</returns>
	public static bool TryGetTexture(string name, out Texture asset) => Instance.TryGet(name, out asset);
	/// <summary>
	/// Attempts to retrieve a texture asset by name.
	/// </summary>
	/// <param name="name">The texture key.</param>
	/// <param name="asset">The output asset reference, or <c>null</c> if not found.</param>
	/// <returns><c>true</c> if the texture exists and was returned; otherwise <c>false</c>.</returns>
	public static bool TryGetTexture(Enum name, out Texture asset) => Instance.TryGet(name, out asset);


	/// <summary>
	/// Retrieves an LDTK map asset using a string key.
	/// </summary>
	/// <param name="name">The asset name as registered in the manager.</param>
	/// <returns>The loaded <see cref="LDTKProject"/> instance.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if the asset was not registered.</exception>
	public static LDTKProject GetMap(string name) => Instance.Get<LDTKProject>(name);

	/// <summary>
	/// Retrieves an LDTK map asset using an enum key.
	/// </summary>
	/// <param name="name">The enum identifier of the asset.</param>
	/// <returns>The loaded <see cref="LDTKProject"/> instance.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if the asset was not registered.</exception>
	public static LDTKProject GetMap(Enum name) => Instance.Get<LDTKProject>(name);

	/// <summary>
	/// Attempts to retrieve an LDTK map asset by string key.
	/// </summary>
	/// <param name="name">The name used to register the map.</param>
	/// <param name="asset">The output variable containing the map, or <c>null</c> if not found.</param>
	/// <returns><c>true</c> if the map was found and retrieved.</returns>
	public static bool TryGetMap(string name, out LDTKProject asset) => Instance.TryGet(name, out asset);

	/// <summary>
	/// Attempts to retrieve an LDTK map asset by enum key.
	/// </summary>
	/// <param name="name">The enum identifier of the map asset.</param>
	/// <param name="asset">The output variable containing the map, or <c>null</c> if not found.</param>
	/// <returns><c>true</c> if the map was found and retrieved.</returns>
	public static bool TryGetMap(Enum name, out LDTKProject asset) => Instance.TryGet(name, out asset);


	/// <summary>
	/// Retrieves a spritesheet asset by its string key.
	/// </summary>
	/// <param name="name">The name used to register the spritesheet.</param>
	/// <returns>The requested <see cref="Spritesheet"/> asset.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if the asset is not found.</exception>
	public static Spritesheet GetSheet(string name) => Instance.Get<Spritesheet>(name);

	/// <summary>
	/// Retrieves a spritesheet asset using an enum key.
	/// </summary>
	/// <param name="name">The enum identifier of the spritesheet.</param>
	/// <returns>The requested <see cref="Spritesheet"/> asset.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if the asset is not found.</exception>
	public static Spritesheet GetSheet(Enum name) => Instance.Get<Spritesheet>(name);

	/// <summary>
	/// Attempts to retrieve a spritesheet by name.
	/// </summary>
	/// <param name="name">The registered string identifier.</param>
	/// <param name="asset">The retrieved asset, or <c>null</c> if not found.</param>
	/// <returns><c>true</c> if the asset exists and was retrieved; otherwise <c>false</c>.</returns>
	public static bool TryGetSheet(string name, out Spritesheet asset) => Instance.TryGet(name, out asset);
	/// <summary>
	/// Attempts to retrieve a spritesheet using an enum key.
	/// </summary>
	/// <param name="name">The enum identifier of the asset.</param>
	/// <param name="asset">The retrieved asset, or <c>null</c> if not found.</param>
	/// <returns><c>true</c> if the asset exists and was retrieved; otherwise <c>false</c>.</returns>
	public static bool TryGetSheet(Enum name, out Spritesheet asset) => Instance.TryGet(name, out asset);


	/// <summary>
	/// Retrieves a font asset by name.
	/// </summary>
	/// <param name="name">The string identifier used at registration.</param>
	/// <returns>The requested <see cref="Font"/> asset.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if the asset is not found.</exception>
	public static Font GetFont(string name) => Instance.Get<Font>(name);

	/// <summary>
	/// Retrieves a font asset using an enum value.
	/// </summary>
	/// <param name="name">The enum identifier for the font asset.</param>
	/// <returns>The requested <see cref="Font"/> asset.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if the asset is not found.</exception>
	public static Font GetFont(Enum name) => Instance.Get<Font>(name);

	/// <summary>
	/// Attempts to retrieve a font asset by name.
	/// </summary>
	/// <param name="name">The font’s registered name.</param>
	/// <param name="asset">The resulting asset, if found; otherwise <c>null</c>.</param>
	/// <returns><c>true</c> if the asset is available and valid.</returns>
	public static bool TryGetFont(string name, out Font asset) => Instance.TryGet(name, out asset);

	/// <summary>
	/// Attempts to retrieve a font asset using an enum key.
	/// </summary>
	/// <param name="name">The enum-based asset identifier.</param>
	/// <param name="asset">The resulting asset, if found; otherwise <c>null</c>.</param>
	/// <returns><c>true</c> if the font was successfully located.</returns>
	public static bool TryGetFont(Enum name, out Font asset) => Instance.TryGet(name, out asset);


	/// <summary>
	/// Retrieves a bitmap font asset by its string identifier.
	/// </summary>
	/// <param name="name">The registered name of the bitmap font asset.</param>
	/// <returns>The associated <see cref="BitmapFont"/> asset.</returns>
	/// <exception cref="KeyNotFoundException">
	/// Thrown if the asset is not found in the registry.
	/// </exception>
	public static BitmapFont GetBitmapFont(string name) => Instance.Get<BitmapFont>(name);

	/// <summary>
	/// Retrieves a bitmap font asset by an enum identifier.
	/// </summary>
	/// <param name="name">The enum key used to reference the bitmap font.</param>
	/// <returns>The associated <see cref="BitmapFont"/> asset.</returns>
	/// <exception cref="KeyNotFoundException">
	/// Thrown if the asset is not found in the registry.
	/// </exception>
	public static BitmapFont GetBitmapFont(Enum name) => Instance.Get<BitmapFont>(name);

	/// <summary>
	/// Attempts to retrieve a bitmap font asset using a string key.
	/// </summary>
	/// <param name="name">The string identifier of the font.</param>
	/// <param name="asset">
	/// When this method returns, contains the <see cref="BitmapFont"/> if found; otherwise <c>null</c>.
	/// </param>
	/// <returns><c>true</c> if the asset was found; otherwise <c>false</c>.</returns>
	public static bool TryGetBitmapFont(string name, out BitmapFont asset) => Instance.TryGet(name, out asset);

	/// <summary>
	/// Attempts to retrieve a bitmap font asset using an enum key.
	/// </summary>
	/// <param name="name">The enum key associated with the font.</param>
	/// <param name="asset">
	/// When this method returns, contains the <see cref="BitmapFont"/> if found; otherwise <c>null</c>.
	/// </param>
	/// <returns><c>true</c> if the asset was found; otherwise <c>false</c>.</returns>
	public static bool TryGetBitmapFont(Enum name, out BitmapFont asset) => Instance.TryGet(name, out asset);


	/// <summary>
	/// Retrieves a sprite font asset using a string identifier.
	/// </summary>
	/// <param name="name">The name of the asset as registered with the asset manager.</param>
	/// <returns>The associated <see cref="SpriteFont"/> instance.</returns>
	/// <exception cref="KeyNotFoundException">
	/// Thrown if no sprite font is registered under the provided name.
	/// </exception>
	public static SpriteFont GetSpriteFont(string name) => Instance.Get<SpriteFont>(name);

	/// <summary>
	/// Retrieves a sprite font asset using an enum identifier.
	/// </summary>
	/// <param name="name">The enum key used to look up the sprite font.</param>
	/// <returns>The associated <see cref="SpriteFont"/> instance.</returns>
	/// <exception cref="KeyNotFoundException">
	/// Thrown if no sprite font is registered under the given enum name.
	/// </exception>
	public static SpriteFont GetSpriteFont(Enum name) => Instance.Get<SpriteFont>(name);

	/// <summary>
	/// Attempts to retrieve a sprite font asset using a string key.
	/// </summary>
	/// <param name="name">The string key of the registered sprite font.</param>
	/// <param name="asset">
	/// When this method returns, contains the <see cref="SpriteFont"/> asset if found; otherwise, <c>null</c>.
	/// </param>
	/// <returns><c>true</c> if the asset was found and is valid; otherwise, <c>false</c>.</returns>
	public static bool TryGetSpriteFont(string name, out SpriteFont asset) => Instance.TryGet(name, out asset);

	/// <summary>
	/// Attempts to retrieve a sprite font asset using an enum identifier.
	/// </summary>
	/// <param name="name">The enum key used to find the registered sprite font.</param>
	/// <param name="asset">
	/// When this method returns, contains the <see cref="SpriteFont"/> asset if found; otherwise, <c>null</c>.
	/// </param>
	/// <returns><c>true</c> if the asset was successfully found and retrieved; otherwise, <c>false</c>.</returns>
	public static bool TryGetSpriteFont(Enum name, out SpriteFont asset) => Instance.TryGet(name, out asset);

	/// <summary>
	/// Retrieves a sound asset using a string identifier.
	/// </summary>
	/// <param name="name">The name of the asset as registered in the asset manager.</param>
	/// <returns>The associated <see cref="Sound"/> asset.</returns>
	/// <exception cref="KeyNotFoundException">
	/// Thrown if no sound asset is registered under the specified name.
	/// </exception>
	public static Sound GetSound(string name) => Instance.Get<Sound>(name);

	/// <summary>
	/// Retrieves a sound asset using an enum identifier.
	/// </summary>
	/// <param name="name">The enum key used to look up the asset.</param>
	/// <returns>The associated <see cref="Sound"/> asset.</returns>
	/// <exception cref="KeyNotFoundException">
	/// Thrown if no sound asset is registered under the specified enum name.
	/// </exception>
	public static Sound GetSound(Enum name) => Instance.Get<Sound>(name);

	/// <summary>
	/// Attempts to retrieve a sound asset using a string name.
	/// </summary>
	/// <param name="name">The string key of the registered asset.</param>
	/// <param name="asset">
	/// When this method returns, contains the <see cref="Sound"/> asset if found; otherwise, <c>null</c>.
	/// </param>
	/// <returns><c>true</c> if the asset was successfully found and retrieved; otherwise, <c>false</c>.</returns>
	public static bool TryGetSound(string name, out Sound asset) => Instance.TryGet(name, out asset);

	/// <summary>
	/// Attempts to retrieve a sound asset using an enum identifier.
	/// </summary>
	/// <param name="name">The enum key used to reference the asset.</param>
	/// <param name="asset">
	/// When this method returns, contains the <see cref="Sound"/> asset if found; otherwise, <c>null</c>.
	/// </param>
	/// <returns><c>true</c> if the asset was successfully found and retrieved; otherwise, <c>false</c>.</returns>
	public static bool TryGetSound(Enum name, out Sound asset) => Instance.TryGet(name, out asset);
	#endregion


	#region TilesetTexture
	/// <summary>
	/// Retrieves the texture associated with a given tileset ID from an LDTK project.
	/// </summary>
	/// <param name="project">The loaded <see cref="LDTKProject"/> that contains the tileset reference.</param>
	/// <param name="tilesetId">The unique ID of the tileset to locate.</param>
	/// <returns>
	/// The corresponding <see cref="Texture"/> asset if found in the asset manager.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="tilesetId"/> is -1, indicating the layer has no tileset assigned or is not a tile instance layer.
	/// </exception>
	/// <exception cref="KeyNotFoundException">
	/// Thrown if the tileset ID does not exist in the <paramref name="project"/>.
	/// </exception>
	public Texture GetTilesetTexture(LDTKProject project, int tilesetId)
	{
		if (tilesetId == -1)
			throw new InvalidOperationException("Tileset ID is -1.");
		if (!project.Tilesets.TryGetValue(tilesetId, out var tileset))
			throw new KeyNotFoundException($"Tileset with ID {tilesetId} was not found in LDTK project");

		// Get the desired *logical* path
		var wanted = Norm(FileHelpers.RemapLDTKPath(tileset.Path, EngineSettings.Instance.AppContentRoot));

		// Find a loaded texture whose Tag is the same logical path
		var texture = _assets
			.Where(kv => kv.Value.Asset is Texture)
			.Select(kv => (Texture)kv.Value.Asset)
			.FirstOrDefault(t => string.Equals(Norm(t.Tag), wanted, StringComparison.OrdinalIgnoreCase));

		return texture;
	}



	/// <summary>
	/// Attempts to retrieve the texture associated with a tileset from an LDTK project.
	/// </summary>
	/// <param name="project">The LDTK project containing the tileset definition.</param>
	/// <param name="tilesetID">The ID of the tileset to resolve to a texture.</param>
	/// <param name="texture">
	/// When this method returns, contains the <see cref="Texture"/> if found; otherwise, <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the tileset texture was located and returned; otherwise <c>false</c>.
	/// </returns>
	public bool TryGetTilesetTexture(LDTKProject project, int tilesetID, out Texture texture)
	{
		texture = GetTilesetTexture(project, tilesetID);

		return texture != null;
	}
	#endregion


	#region Private Methods


	private static bool TryFindFullPath(string assetPathWithoutExtention, string[] extentions,
	out string foundFullPath)
	{
		if (string.IsNullOrEmpty(assetPathWithoutExtention))
		{
			foundFullPath = null;
			return false;
		}

		if (extentions == null || extentions.Length == 0)
		{
			foundFullPath = null;
			return false;
		}

		string baseDir = AppContext.BaseDirectory;
		string contentFolder = Path.Combine(baseDir, EngineSettings.Instance.AppContentRoot);
		string normalizeAssetPath = assetPathWithoutExtention
			.Replace('/', Path.DirectorySeparatorChar)
			.Replace('\\', Path.DirectorySeparatorChar);

		foreach (var ext in extentions)
		{
			if (string.IsNullOrEmpty(ext))
				continue;
			string normalizeExt = ext.StartsWith('.') ? ext : $".{ext}";

			string candidate = Path.Combine(
				contentFolder, $"{normalizeAssetPath}{normalizeExt}"
			);

			if (File.Exists(candidate))
			{
				foundFullPath = candidate;
				return true;
			}
		}

		foundFullPath = null;
		return false;
	}




	// only if absolutely required by a third-party API
	private static bool TryGetPhysicalPath(string logical, out string physical)
	{
		var baseDir = AppContext.BaseDirectory;
		var root = EngineSettings.Instance.AppContentRoot ?? "Content";
		var rel = logical.Replace('/', Path.DirectorySeparatorChar);

		physical = Path.Combine(baseDir, root, rel);

		return File.Exists(physical);
	}

	private static bool TryResolveLogicalPath(
	string baseWithoutExt,
	string[] exts,
	out string logicalPath)
	{
		logicalPath = null!;
		if (string.IsNullOrWhiteSpace(baseWithoutExt) || exts is null || exts.Length == 0)
			return false;

		var baseLogical = baseWithoutExt.Replace('\\', '/').TrimStart('/');

		foreach (var ext in exts)
		{
			if (string.IsNullOrEmpty(ext)) continue;
			var e = ext[0] == '.' ? ext : "." + ext;
			var candidate = baseLogical + e;
			try
			{
				// Probe once; dispose immediately
				using (AssetManager.OpenStream(candidate)) { }
				logicalPath = candidate;
				return true;
			}
			catch (FileNotFoundException)
			{
				// try next
			}
		}
		return false;
	}

	private static bool TryResolveAssetPath(string assetPathWithoutExtension, string[] extensions, out string resolvedPath, out bool isPhysicalPath)
	{
		resolvedPath = null!;
		isPhysicalPath = false;

		if (string.IsNullOrWhiteSpace(assetPathWithoutExtension) || extensions is null || extensions.Length == 0)
			return false;

		var baseLogical = assetPathWithoutExtension.Replace('\\', '/').TrimStart('/');

		foreach (var ext in extensions)
		{
			if (string.IsNullOrEmpty(ext)) continue;
			var e = ext[0] == '.' ? ext : "." + ext;

			var candidateLogical = baseLogical + e;
			var candidatePhysical = ToPhysicalPath(candidateLogical);

			// 1) If the loose file exists under Content/, return its physical path (for path-based loaders)
			if (File.Exists(candidatePhysical))
			{
				resolvedPath = candidatePhysical;
				isPhysicalPath = true;
				return true;
			}

			// 2) Otherwise, try opening via provider (packs, overlays, etc.) and return the logical path
			try
			{
				using (AssetManager.OpenStream(candidateLogical)) { }
				resolvedPath = candidateLogical;
				isPhysicalPath = false;
				return true;
			}
			catch (FileNotFoundException)
			{
				// try next extension
			}
		}

		return false;
	}

	private static string ToPhysicalPath(string logicalPath)
	{
		var baseDir = AppContext.BaseDirectory;
		var contentRoot = EngineSettings.Instance.AppContentRoot ?? "Content";
		var osPath = logicalPath.Replace('/', Path.DirectorySeparatorChar);
		return Path.Combine(baseDir, contentRoot, osPath);
	}

	private bool InternalRemove(uint hash, bool removeInDirectory)
	{
		if (!_assets.TryGetValue(hash, out var entry))
			throw new KeyNotFoundException($"No asset found with hash 0x{hash:X8}, unable to remove.");
		if (!entry.Asset.IsValid)
		{
			if (removeInDirectory)
				return _assets.Remove(hash);
			else
				return default;
		}

		TextureAtlasManager.Instance.UnloadAsset(entry.Asset.Handle);

		if (removeInDirectory)
		{
			if (entry.Asset is Sound s)
				s.Dispose();
			else
				entry.Asset.Unload();

			BytesLoaded -= (long)entry.Length;
		}
		else
		{
			entry.Asset.Unload();
			BytesLoaded -= (long)entry.Length;
		}

		if (BytesLoaded < 0) BytesLoaded = 0;

		if (removeInDirectory)
			return _assets.Remove(hash);

		return false;
	}

	private void EvictAssets()
	{
		DateTime now = DateTime.UtcNow;
		TimeSpan evictAfter = TimeSpan.FromMinutes(EvictAfterMinutes);
		var toEvict = new List<uint>();

		foreach (var kvp in _assets)
		{
			TimeSpan age = now - kvp.Value.LastAccessFrame;

			if (age >= evictAfter)
				toEvict.Add(kvp.Key);
		}

		if (toEvict.Count > 0)
		{
			foreach (var key in toEvict)
				InternalRemove(key, false);
		}
	}
	#endregion
}
