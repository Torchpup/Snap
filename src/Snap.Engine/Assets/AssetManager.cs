namespace Snap.Engine.Assets;

/// <summary>
/// Manages lazy-loaded assets with automatic eviction of GPU resources based on access time.
/// Assets are cached by virtual path, with bytes stored permanently and GPU resources evicted
/// gradually as new assets are loaded.
/// </summary>
/// <remarks>
/// <para>
/// The asset manager uses a two-tier caching strategy:
/// <list type="bullet">
///   <item><description><b>Bytes:</b> Raw file data is stored permanently in memory for fast reloading</description></item>
///   <item><description><b>GPU Resources:</b> Textures, sounds, and other hardware resources can evict when not accessed</description></item>
///   <item><description><b>Metadata:</b> Spritesheet rectangles, font glyphs, and map data remain in memory</description></item>
/// </list>
/// </para>
/// <para>
/// Assets are loaded lazily on first request. When memory pressure increases, one expired asset
/// is evicted per new asset load, ensuring gradual cleanup without frame spikes.
/// </para>
/// <para>
/// Supports multiple mount points (file system, packed archives, platform-specific bundles)
/// with configurable priority order.
/// </para>
/// </remarks>
public sealed class AssetManager
{
    internal static uint Id;

    private readonly Dictionary<string, IAsset> _assets = [];
    private readonly List<IMount> _mounts = [];

    /// <summary>Gets the singleton instance of the asset manager.</summary>
    public static AssetManager Instance { get; private set; }

    // Supported extensions per asset type
    private static readonly Dictionary<Type, string[]> SupportedExtensions = new()
    {
        { typeof(Texture), new[] { ".png", ".bmp", ".tga", ".jpg", ".gif", ".psd", ".hdr", ".pic", ".pnm" } },
        { typeof(LDtkMap), new[] { ".ldtk", ".json" }},
        { typeof(SpriteFont), new[] { ".png", ".bmp", ".tga", ".jpg", ".gif", ".psd", ".hdr", ".pic", ".pnm" }},
        { typeof(BitmapFont), new[] { ".fnt" }},
        { typeof(Spritesheet), new[] { ".sheet", ".json" }},
        { typeof(Sound), new[] {
            ".wav", ".mp3", ".ogg", ".flac", ".aiff", ".au", ".raw", ".paf", ".svx",
            ".nist", ".voc", ".ircam", ".w64", ".mat4", ".mat5", ".pvf", ".htk",
            ".sds", ".avr", ".sd2", ".caf", ".wve", ".mpc2k", ".rf64"
        }}
    };

    // Default loaders for each asset type (now take bytes, not path)
    private static readonly Dictionary<Type, Func<byte[], uint, string, IAsset>> DefaultLoaders = new()
    {
        { typeof(Texture), (data, id, tag) => new Texture(data, id, tag, false, false) },
        { typeof(LDtkMap), (data, id,tag) => new LDtkMap(data, id, tag) },
        { typeof(SpriteFont), (data, id, tag) => new SpriteFont(data, id, tag, 0, 0, false, SpriteFont.CharListAll) },
        { typeof(BitmapFont), (data, id, tag) => new BitmapFont(data, id, tag, 0, 0, false)},
        { typeof(Spritesheet), (data,id,tag) => new Spritesheet(data, id, tag)},
        { typeof(Sound), (data, id, tag) => new Sound(data, id, tag, false)}
    };

    internal AssetManager()
    {
        Instance ??= this;

        // Add platform-specific mount first (highest priority)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // On macOS, bundle mount has highest priority
            _mounts.Add(new MacOsMount());
        }

        // Add VFS mount at the end (lowest priority, fallback)
        _mounts.Add(new VirtualFileSystemMount());
    }

    internal void Clear()
    {
        foreach (var asset in _assets.Values)
            asset.Dispose();
        _assets.Clear();

        foreach (var mount in _mounts.OfType<IDisposable>())
            mount.Dispose();
        _mounts.Clear();
    }



    #region Mount Management
    /// <summary>Adds a mount to the beginning of the mount list (highest priority).</summary>
    /// <param name="mount">The mount to add.</param>
    public void AddMountToStart(IMount mount) => _mounts.Insert(0, mount);

    /// <summary>Adds a mount to the end of the mount list (lowest priority).</summary>
    /// <param name="mount">The mount to add.</param>
    public void AddMountToEnd(IMount mount) => _mounts.Add(mount);

    /// <summary>Inserts a mount at the specified index in the mount list.</summary>
    /// <param name="index">The index at which to insert the mount.</param>
    /// <param name="mount">The mount to insert.</param>
    public void InsertMount(int index, IMount mount) => _mounts.Insert(index, mount);

    /// <summary>Removes the specified mount from the mount list.</summary>
    /// <param name="mount">The mount to remove.</param>
    public void RemoveMount(IMount mount) => _mounts.Remove(mount);

    /// <summary>
    /// Clears all custom mounts and resets to the default virtual file system mount as the only fallback.
    /// </summary>
    /// <remarks>
    /// The virtual file system mount is always preserved as the lowest priority fallback
    /// to ensure core assets can still be loaded from disk.
    /// </remarks>
    public void ClearMounts()
    {
        _mounts.Clear();

        // Add platform-specific mount first (highest priority)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // On macOS, bundle mount has highest priority
            _mounts.Add(new MacOsMount());
        }

        // Always keep VFS mount as fallback
        _mounts.Add(new VirtualFileSystemMount());
    }

    /// <summary>
    /// Mounts a packed archive file as a mount point, optionally with an encryption key file.
    /// </summary>
    /// <param name="packPath">The virtual path to the pack file to mount.</param>
    /// <param name="keyPath">Optional virtual path to a key file for decryption. If <c>null</c> or empty, the pack is treated as unencrypted.</param>
    /// <param name="insertAtStart">
    /// If <c>true</c>, the pack is added at the start of the mount list (highest priority);
    /// otherwise, added at the end (lowest priority).
    /// </param>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="keyPath"/> is specified but the key file does not exist.</exception>
    public void MountPack(string packPath, string keyPath = null, bool insertAtStart = true)
    {
        var fullPackPath = GetFullPath(packPath);
        byte[] key = null;

        if (!string.IsNullOrEmpty(keyPath))
        {
            var fullKeyPath = GetFullPath(keyPath);

            if (File.Exists(fullKeyPath))
                key = File.ReadAllBytes(fullKeyPath);
            else
                throw new FileNotFoundException($"Key file not found: {fullKeyPath}");
        }

        var mount = new PackMount(fullPackPath, key);

        if (insertAtStart)
            _mounts.Insert(0, mount);
        else
            _mounts.Add(mount);
    }

    /// <summary>
    /// Mounts a packed archive file as a mount point using the provided encryption key.
    /// </summary>
    /// <param name="packPath">The virtual path to the pack file to mount.</param>
    /// <param name="key">The encryption key bytes used to decrypt the pack. If <c>null</c>, the pack is treated as unencrypted.</param>
    /// <param name="insertAtStart">
    /// If <c>true</c>, the pack is added at the start of the mount list (highest priority);
    /// otherwise, added at the end (lowest priority).
    /// </param>
    public void MountPack(string packPath, byte[] key, bool insertAtStart = true)
    {
        var fullPackPath = GetFullPath(packPath);
        var mount = new PackMount(fullPackPath, key);

        if (insertAtStart)
            _mounts.Insert(0, mount);
        else
            _mounts.Add(mount);
    }
    #endregion



    #region GetOrLoad
    /// <summary>Loads an asset of type <typeparamref name="T"/> from the specified virtual path.</summary>
    /// <typeparam name="T">The asset type to load (must implement <see cref="IAsset"/>).</typeparam>
    /// <param name="path">The virtual path to the asset.</param>
    /// <returns>The loaded asset, or <c>default</c> if the asset could not be found or loaded.</returns>
    public T Load<T>(string path) where T : IAsset
        => GetOrLoadInternal<T>(path, null);

    /// <summary>Attempts to load an asset of type <typeparamref name="T"/> from the specified virtual path.</summary>
    /// <typeparam name="T">The asset type to load (must implement <see cref="IAsset"/>).</typeparam>
    /// <param name="path">The virtual path to the asset.</param>
    /// <param name="asset">When this method returns, contains the loaded asset if successful; otherwise, <c>default</c>.</param>
    /// <returns><c>true</c> if the asset was loaded successfully; otherwise, <c>false</c>.</returns>
    public bool TryLoad<T>(string path, out T asset) where T : IAsset
    {
        try
        {
            asset = GetOrLoadInternal<T>(path, null);
            return asset != null;
        }
        catch
        {
            asset = default;
            return false;
        }
    }

    /// <summary>Loads a texture from the specified virtual path with the given repeat and smoothing settings.</summary>
    /// <param name="path">The virtual path to the texture asset.</param>
    /// <param name="repeat">Whether the texture should repeat when tiled.</param>
    /// <param name="smoothing">Whether the texture should use smoothing (linear filtering).</param>
    /// <returns>The loaded texture, or <c>null</c> if the asset could not be found or loaded.</returns>
    public Texture LoadTexture(string path, bool repeat, bool smoothing)
    {
        return GetOrLoadInternal(path, (data, id, tag) => new Texture(data, id, tag, repeat, smoothing));
    }

    /// <summary>Attempts to load a texture from the specified virtual path with the given repeat and smoothing settings.</summary>
    /// <param name="path">The virtual path to the texture asset.</param>
    /// <param name="repeat">Whether the texture should repeat when tiled.</param>
    /// <param name="smoothing">Whether the texture should use smoothing (linear filtering).</param>
    /// <param name="asset">When this method returns, contains the loaded texture if successful; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the texture was loaded successfully; otherwise, <c>false</c>.</returns>
    public bool TryLoadTexture(string path, bool repeat, bool smoothing, out Texture asset)
    {
        asset = LoadTexture(path, repeat, smoothing);
        return asset != null;
    }

    /// <summary>Loads an LDtk map from the specified virtual path.</summary>
    /// <param name="path">The virtual path to the LDtk map asset (.ldtk or .json).</param>
    /// <returns>The loaded LDtk map, or <c>null</c> if the asset could not be found or loaded.</returns>
    public LDtkMap LoadLDtk(string path)
    {
        return GetOrLoadInternal(path, (data, id, tag) => new LDtkMap(data, id, tag));
    }

    /// <summary>Attempts to load an LDtk map from the specified virtual path.</summary>
    /// <param name="path">The virtual path to the LDtk map asset (.ldtk or .json).</param>
    /// <param name="asset">When this method returns, contains the loaded LDtk map if successful; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the LDtk map was loaded successfully; otherwise, <c>false</c>.</returns>
    public bool TryLoadLDtk(string path, out LDtkMap asset)
    {
        asset = LoadLDtk(path);
        return asset != null;
    }

    /// <summary>Loads a sprite font from the specified virtual path with the given spacing and smoothing settings.</summary>
    /// <param name="path">The virtual path to the sprite font texture asset.</param>
    /// <param name="spacing">Additional spacing between characters.</param>
    /// <param name="lineSpacing">Additional spacing between lines.</param>
    /// <param name="smoothing">Whether the font texture should use smoothing (linear filtering).</param>
    /// <param name="charset">The set of characters to include in the font. If <c>null</c> or whitespace, includes all available characters.</param>
    /// <returns>The loaded sprite font, or <c>null</c> if the asset could not be found or loaded.</returns>
    public SpriteFont LoadSpriteFont(string path, int spacing, int lineSpacing, bool smoothing, string charset)
    {
        return GetOrLoadInternal(path, (data, id, tag) => new SpriteFont(data, id, tag, spacing, lineSpacing,
            smoothing, string.IsNullOrWhiteSpace(charset) ? SpriteFont.CharListAll : charset));
    }

    /// <summary>Attempts to load a sprite font from the specified virtual path with the given spacing and smoothing settings.</summary>
    /// <param name="path">The virtual path to the sprite font texture asset.</param>
    /// <param name="spacing">Additional spacing between characters.</param>
    /// <param name="lineSpacing">Additional spacing between lines.</param>
    /// <param name="smoothing">Whether the font texture should use smoothing (linear filtering).</param>
    /// <param name="charset">The set of characters to include in the font. If <c>null</c> or whitespace, includes all available characters.</param>
    /// <param name="asset">When this method returns, contains the loaded sprite font if successful; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the sprite font was loaded successfully; otherwise, <c>false</c>.</returns>
    public bool TryLoadSpriteFont(string path, int spacing, int lineSpacing, bool smoothing, string charset, out SpriteFont asset)
    {
        asset = LoadSpriteFont(path, spacing, lineSpacing, smoothing, charset);
        return asset != null;
    }

    /// <summary>Loads a bitmap font from the specified virtual path with the given spacing and smoothing settings.</summary>
    /// <param name="path">The virtual path to the bitmap font asset (.fnt).</param>
    /// <param name="spacing">Additional spacing between characters.</param>
    /// <param name="lineSpacing">Additional spacing between lines.</param>
    /// <param name="smoothing">Whether the font texture should use smoothing (linear filtering).</param>
    /// <returns>The loaded bitmap font, or <c>null</c> if the asset could not be found or loaded.</returns>
    public BitmapFont LoadBitmapFont(string path, int spacing, int lineSpacing, bool smoothing)
    {
        return GetOrLoadInternal(path, (data, id, tag) => new BitmapFont(data, id, tag, spacing, lineSpacing,
            smoothing));
    }

    /// <summary>Attempts to load a bitmap font from the specified virtual path with the given spacing and smoothing settings.</summary>
    /// <param name="path">The virtual path to the bitmap font asset (.fnt).</param>
    /// <param name="spacing">Additional spacing between characters.</param>
    /// <param name="lineSpacing">Additional spacing between lines.</param>
    /// <param name="smoothing">Whether the font texture should use smoothing (linear filtering).</param>
    /// <param name="asset">When this method returns, contains the loaded bitmap font if successful; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the bitmap font was loaded successfully; otherwise, <c>false</c>.</returns>
    public bool TryLoadBitmapFont(string path, int spacing, int lineSpacing, bool smoothing, out BitmapFont asset)
    {
        asset = LoadBitmapFont(path, spacing, lineSpacing, smoothing);
        return asset != null;
    }

    /// <summary>Loads a spritesheet from the specified virtual path.</summary>
    /// <param name="path">The virtual path to the spritesheet asset (.sheet or .json).</param>
    /// <returns>The loaded spritesheet, or <c>null</c> if the asset could not be found or loaded.</returns>
    public Spritesheet LoadSheet(string path)
    {
        return GetOrLoadInternal(path, (data, id, tag) => new Spritesheet(data, id, tag));
    }

    /// <summary>Attempts to load a spritesheet from the specified virtual path.</summary>
    /// <param name="path">The virtual path to the spritesheet asset (.sheet or .json).</param>
    /// <param name="asset">When this method returns, contains the loaded spritesheet if successful; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the spritesheet was loaded successfully; otherwise, <c>false</c>.</returns>
    public bool TryLoadSheet(string path, out Spritesheet asset)
    {
        asset = LoadSheet(path);
        return asset != null;
    }

    /// <summary>Loads a sound from the specified virtual path with the given loop setting.</summary>
    /// <param name="path">The virtual path to the sound asset (supports .wav, .mp3, .ogg, and many other formats).</param>
    /// <param name="looped">Whether the sound should loop when played.</param>
    /// <returns>The loaded sound, or <c>null</c> if the asset could not be found or loaded.</returns>
    public Sound LoadSound(string path, bool looped)
    {
        return GetOrLoadInternal(path, (data, id, tag) => new Sound(data, id, tag, looped));
    }

    /// <summary>Attempts to load a sound from the specified virtual path with the given loop setting.</summary>
    /// <param name="path">The virtual path to the sound asset (supports .wav, .mp3, .ogg, and many other formats).</param>
    /// <param name="looped">Whether the sound should loop when played.</param>
    /// <param name="asset">When this method returns, contains the loaded sound if successful; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the sound was loaded successfully; otherwise, <c>false</c>.</returns>
    public bool TryLoadSound(string path, bool looped, out Sound asset)
    {
        asset = LoadSound(path, looped);
        return asset != null;
    }

    /// <summary>
    /// Retrieves the texture associated with a given tileset ID from an LDTK project.
    /// </summary>
    /// <param name="project">The loaded <see cref="LDtkMap"/> that contains the tileset reference.</param>
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
    public Texture GetTilesetTexture(LDtkMap project, uint tilesetId)
    {
        if (tilesetId == 0)
            throw new InvalidOperationException("Tileset ID is 0.");
        if (!project.TryGetTilesetId(tilesetId, out var tileset))
            throw new KeyNotFoundException($"Tileset with ID {tilesetId} was not found in LDTK project");

        // Get the desired *logical* path
        var wanted = NormalizePath(FileHelpers.RemapLDTKPath(tileset.Path, EngineSettings.Instance.AppContentRoot));

        // Find a loaded texture whose Tag is the same logical path
        var texture = Instance._assets
            .Where(kv => kv.Value is Texture)
            .Select(kv => (Texture)kv.Value)
            .FirstOrDefault(t => string.Equals(NormalizePath(t.Tag), wanted, StringComparison.OrdinalIgnoreCase));

        // If still null, try to load it directly.
        if (texture == null)
            texture = Instance.Load<Texture>(wanted);

        if (texture == null)
            throw new FileNotFoundException($"Unable to locate file: {wanted}");

        return texture;
    }

    /// <summary>
    /// Attempts to retrieve the texture associated with a given tileset ID from an LDTK project.
    /// </summary>
    /// <param name="project">The loaded <see cref="LDtkMap"/> that contains the tileset reference.</param>
    /// <param name="tilesetId">The unique ID of the tileset to locate.</param>
    /// <param name="texture">
    /// When this method returns, contains the <see cref="Texture"/> associated with the specified tileset ID 
    /// if the operation succeeded; otherwise, <c>null</c>. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <c>true</c> if the tileset texture was successfully retrieved; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method does not throw exceptions. Instead, it returns <c>false</c> under any failure condition, 
    /// including when:
    /// <list type="bullet">
    /// <item><description>The tileset ID is 0 (invalid).</description></item>
    /// <item><description>The tileset ID does not exist in the project.</description></item>
    /// <item><description>The texture file cannot be found or loaded.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="GetTilesetTexture(LDtkMap, uint)"/>
    public bool TryGetTilesetTexture(LDtkMap project, uint tilesetId, out Texture texture)
    {
        try
        {
            texture = GetTilesetTexture(project, tilesetId);
            return true;
        }
        catch
        {
            texture = null;
            return false;
        }
    }
    #endregion


    #region Private Methods
    private T GetOrLoadInternal<T>(string path, Func<byte[], uint, string, T> customLoader) where T : IAsset
    {
        var normalizedPath = NormalizePath(path);

        if (!IsValidExtension(normalizedPath, typeof(T)))
            throw new FileNotFoundException(
                $"Asset '{normalizedPath}' has an unsupported extension for type '{typeof(T).Name}'. " +
                $"Supported extensions: {string.Join(", ", SupportedExtensions[typeof(T)])}");

        // Check if asset exists in cache
        if (_assets.TryGetValue(normalizedPath, out IAsset existingAsset))
        {
            existingAsset.Load();
            EvictOneExpiredAsset();
            return (T)existingAsset;
        }

        // Find first mount that has the file
        byte[] assetData = null;
        string foundInMount = null;

        foreach (var mount in _mounts)
        {
            if (mount.HasFile(normalizedPath))
            {
                try
                {
                    assetData = mount.ReadFile(normalizedPath);
                    foundInMount = mount.GetType().Name;
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log(LogLevel.Warning,
                        $"[AssetManager] Mount '{mount.GetType().Name}' reported file '{normalizedPath}' but failed to read: {ex.Message}");
                    continue;
                }
            }
        }

        if (assetData == null)
            throw new FileNotFoundException(
                $"Asset '{normalizedPath}' of type '{typeof(T).Name}' was not found in any mount. " +
                $"Searched {_mounts.Count} mount(s): {string.Join(", ", _mounts.Select(m => m.GetType().Name))}");

        T newAsset;
        try
        {
            if (customLoader != null)
                newAsset = customLoader(assetData, Id++, normalizedPath);
            else if (DefaultLoaders.TryGetValue(typeof(T), out var defaultLoader))
                newAsset = (T)defaultLoader(assetData, Id++, normalizedPath);
            else
                throw new InvalidOperationException(
                    $"No loader found for asset type '{typeof(T).Name}'. " +
                    $"Registered types: {string.Join(", ", DefaultLoaders.Keys.Select(t => t.Name))}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create asset '{normalizedPath}' of type '{typeof(T).Name}' from mount '{foundInMount}'. " +
                $"Data size: {assetData.Length} bytes. Error: {ex.Message}", ex);
        }

        // Store and load the asset
        _assets.Add(normalizedPath, newAsset);
        newAsset.Load();

        // Evict one expired asset
        EvictOneExpiredAsset();

        return newAsset;
    }

    private void EvictOneExpiredAsset()
    {
        var evictionMinutes = EngineSettings.Instance.AssetEvictionMinutes;
        if (evictionMinutes <= 0) return;

        foreach (var kvp in _assets)
        {
            IAsset asset = kvp.Value;

            if (DateTime.Now - asset.LastAccessTime > TimeSpan.FromMinutes(evictionMinutes))
            {
                asset.Unload();
                break; // Only evict one per call
            }
        }
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        path = path.Replace('\\', '/');
        path = path.Replace("..", "");

        while (path.Contains("//"))
            path = path.Replace("//", "/");

        if (path.StartsWith('/'))
            path = path[1..];

        return path;
    }

    private bool IsValidExtension(string path, Type assetType)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (SupportedExtensions.TryGetValue(assetType, out var extensions))
            return extensions.Contains(ext);

        return false;
    }

    private string GetFullPath(string virtualPath)
    {
        var contentRoot = EngineSettings.Instance.AppContentRoot;

        if (!contentRoot.EndsWith('/') && !contentRoot.EndsWith('\\'))
            contentRoot += Path.DirectorySeparatorChar;

        var fullPath = Path.GetFullPath(Path.Combine(contentRoot, virtualPath));

        // Security check: ensure path is within content root
        if (!fullPath.StartsWith(Path.GetFullPath(contentRoot)))
            throw new UnauthorizedAccessException($"Cannot access file outside of ContentRoot: {virtualPath}");

        return fullPath;
    }
    #endregion
}
