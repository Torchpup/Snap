namespace Snap.Engine.Graphics.Atlas;

/// <summary>
/// Manages a collection of texture atlas pages, handling texture packing,
/// retrieval, and capacity tracking.
/// </summary>
/// <remarks>
/// <para>
/// This manager maintains multiple <see cref="AtlasPage"/> instances, each of which
/// uses a <see cref="SkylinePacker"/> to place textures efficiently.
/// </para>
/// <para>
/// The manager tracks registered texture regions to avoid duplication, monitors
/// atlas usage over time, and can enforce idle time limits for cleanup.
/// </para>
/// </remarks>
public sealed class TextureAtlasManager
{
	private readonly Dictionary<int, AtlasPage> _pagesById = [];
	private readonly Dictionary<(uint texHandle, SFRectI rect), SliceInfo> _registered = [];

	/// <summary>
	/// Gets or sets the maximum amount of time an atlas page can remain unused
	/// before being eligible for cleanup.
	/// </summary>
	public TimeSpan MaxIdle { get; set; }

	/// <summary>
	/// Gets the singleton instance of the <see cref="TextureAtlasManager"/>.
	/// </summary>
	public static TextureAtlasManager Instance { get; private set; }

	/// <summary>
	/// Gets the size (width and height in pixels) of each atlas page. Pages are always square.
	/// </summary>
	public int PageSize { get; }

	/// <summary>
	/// Gets the maximum number of atlas pages allowed in this manager.
	/// </summary>
	public int MaxPages { get; }

	/// <summary>
	/// Gets the current number of atlas pages managed.
	/// </summary>
	public int Pages => _pagesById.Count;

	/// <summary>
	/// Gets the total fill ratio across all atlas pages.
	/// </summary>
	/// <remarks>
	/// This value is calculated as the total used pixel count divided by the
	/// total available pixel capacity across all pages.
	/// </remarks>
	public double TotalFillRatio =>
		TotalCapacityPixels == 0
		   ? 0
		   : (double)TotalUsedPixels / TotalCapacityPixels;

	private long TotalCapacityPixels =>
		(long)PageSize * PageSize * _pagesById.Count;

	private long TotalUsedPixels =>
		_pagesById.Values.Sum(p => p.UsedPixels);

	internal TextureAtlasManager(int pageSize, int maxPages)
	{
		Instance ??= this;

		PageSize = pageSize;
		MaxPages = maxPages;
		MaxIdle = TimeSpan.FromMinutes(30);

		// start with one page (id = 0)
		_pagesById[0] = new AtlasPage(pageSize, 0);
	}

	/// <summary>
	/// Gets the total number of registered texture regions across all atlas pages.
	/// </summary>
	/// <remarks>
	/// This is primarily intended for debugging and metrics, and counts unique
	/// registered slices stored in the internal registry.
	/// </remarks>
	public int Count => _registered.Count;

	// Internal record per slice
	private struct SliceInfo
	{
		public AtlasHandle Handle;
		public DateTime LastUsedUtc;
	}

	/// <summary>
	/// Retrieves an existing slice from the atlas or packs it in if it does not already exist.
	/// </summary>
	/// <param name="srcTexture">The source texture containing the region to pack.</param>
	/// <param name="srcRect">The rectangle region within the source texture to pack.</param>
	/// <returns>
	/// An <see cref="AtlasHandle"/> describing the location of the packed slice, or
	/// <c>null</c> if the slice could not be packed after eviction attempts.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This is the main entry point for requesting texture regions from the atlas manager.
	/// The method follows this sequence:
	/// </para>
	/// <list type="number">
	/// <item><description>Evict any stale slices that have exceeded <see cref="MaxIdle"/> time.</description></item>
	/// <item><description>If the requested slice is already registered, update its last-used timestamp and return it.</description></item>
	/// <item><description>Attempt to pack the slice into any existing page without eviction.</description></item>
	/// <item><description>If that fails, retry after evicting stale slices.</description></item>
	/// <item><description>If still failing, perform Least Recently Used (LRU) eviction, removing the oldest slices until it fits.</description></item>
	/// </list>
	/// <para>
	/// If no space is found after all eviction strategies, the method returns <c>null</c>.
	/// </para>
	/// </remarks>
	public AtlasHandle? GetOrCreateSlice(SFTexture srcTexture, SFRectI srcRect)
	{
		var key = (srcTexture.NativeHandle, srcRect);

		// EVICT STALE ON *EVERY* ACCESS (must be before cache lookup)
		var evicted = EvictStaleSlices(MaxIdle);
		if (evicted.Count > 0)
			Logger.Instance.Log(LogLevel.Info, $"[Atlas] ⚠️ Evicted {evicted.Count} stale slices at {DateTime.UtcNow:HH:mm:ss}");

		// Cache hit?
		if (_registered.TryGetValue(key, out var info))
		{
			info.LastUsedUtc = DateTime.UtcNow;
			_registered[key] = info;
			return info.Handle;
		}

		// AtlasHandle handle;

		// Try packing straight away (no eviction)
		if (TryPackIntoAnyPage(srcTexture, srcRect, out var handle))
		{
			_registered[key] = new SliceInfo { Handle = handle, LastUsedUtc = DateTime.UtcNow };
			return handle;
		}

		// Evict truly idle slices
		// EvictStaleSlices(MaxIdle);

		if (TryPackIntoAnyPage(srcTexture, srcRect, out var otherHandle))
		{
			_registered[key] = new SliceInfo { Handle = handle, LastUsedUtc = DateTime.UtcNow };
			return otherHandle;
		}

		// LRU eviction: remove oldest one by one until it fits
		var lruList = _registered
			.OrderBy(kv => kv.Value.LastUsedUtc)
			.Select(kv => kv.Key)
			.ToList();

		foreach (var (texHandle, rect) in lruList)
		{
			// remove from page & registry
			RemoveSlice(texHandle, rect);

			if (TryPackIntoAnyPage(srcTexture, srcRect, out handle))
			{
				_registered[key] = new SliceInfo { Handle = handle, LastUsedUtc = DateTime.UtcNow };
				return handle;
			}
		}

		// Still no room
		return null;
	}

	/// <summary>
	/// Attempts to pack the specified texture region into any existing atlas page,
	/// or creates a new page if the maximum page limit has not been reached.
	/// </summary>
	/// <param name="srcTexture">The source texture containing the region to pack.</param>
	/// <param name="srcRect">The rectangle region within the source texture to pack.</param>
	/// <param name="handle">
	/// When this method returns <c>true</c>, contains the <see cref="AtlasHandle"/> describing
	/// the location of the packed texture region.
	/// </param>
	/// <returns>
	/// <c>true</c> if the region was successfully packed into an existing or new page; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The method first attempts to place the region into each existing <see cref="AtlasPage"/>.
	/// If all pages fail, and the current page count is less than <see cref="MaxPages"/>,
	/// a new page is created and the region is packed into it.
	/// </para>
	/// <para>
	/// If the method returns <c>false</c>, no suitable placement could be found,
	/// even after creating a new page.
	/// </para>
	/// </remarks>
	private bool TryPackIntoAnyPage(SFTexture srcTexture, SFRectI srcRect, out AtlasHandle handle)
	{
		// Existing pages
		foreach (var page in _pagesById.Values)
		{
			if (page.TryPack(srcTexture, srcRect, out handle))
				return true;
		}

		// New page
		if (_pagesById.Count < MaxPages)
		{
			int newId = _pagesById.Count;
			var page = new AtlasPage(PageSize, newId);
			_pagesById[newId] = page;

			if (page.TryPack(srcTexture, srcRect, out handle))
				return true;
		}

		handle = default;
		return false;
	}

	/// <summary>
	/// Evicts any registered texture slice that has not been used within the specified idle time span.
	/// </summary>
	/// <param name="maxIdle">
	/// The maximum allowed idle time for a slice before it is considered stale and eligible for eviction.
	/// </param>
	/// <returns>
	/// A list of tuple keys <c>(texHandle, rect)</c> representing the evicted slices.
	/// This can be used for logging or metrics.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method checks all registered slices against the cutoff timestamp
	/// (<see cref="DateTime.UtcNow"/> minus <paramref name="maxIdle"/>).
	/// Any slice last used before this cutoff is removed from the registry and has its
	/// pixel usage deducted from the corresponding <see cref="AtlasPage"/> via <see cref="AtlasPage.RemoveLazy"/>.
	/// </para>
	/// <para>
	/// Evicted slices free up capacity in the atlas for future packing requests,
	/// but do not reorganize or repack existing content.
	/// </para>
	/// </remarks>
	public List<(uint texHandle, SFRectI rect)> EvictStaleSlices(TimeSpan maxIdle)
	{
		var cutoff = DateTime.UtcNow - maxIdle;
		var toEvict = _registered
			.Where(kv => kv.Value.LastUsedUtc < cutoff)
			.Select(kv => kv.Key)
			.ToList();

		foreach (var key in toEvict)
		{
			var info = _registered[key];
			_pagesById[info.Handle.PageId]
				.RemoveLazy(info.Handle.SourceRect);
			_registered.Remove(key);
		}

		return toEvict;
	}

	/// <summary>
	/// Immediately evicts a single slice from the atlas, typically during LRU eviction.
	/// </summary>
	/// <param name="texHandle">The native handle of the source texture.</param>
	/// <param name="rect">The rectangle region of the slice within the source texture.</param>
	/// <returns>
	/// <c>true</c> if the slice was found and removed; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// This method removes the slice from both the registry and the corresponding
	/// <see cref="AtlasPage"/> via <see cref="AtlasPage.RemoveLazy"/>.
	/// </remarks>
	public bool RemoveSlice(uint texHandle, SFRectI rect)
	{
		var key = (texHandle, rect);
		if (!_registered.TryGetValue(key, out var info))
			return false;

		_pagesById[info.Handle.PageId]
			.RemoveLazy(info.Handle.SourceRect);
		_registered.Remove(key);
		return true;
	}

	/// <summary>
	/// Retrieves the <see cref="SFTexture"/> associated with a given atlas page ID.
	/// </summary>
	/// <param name="pageId">The ID of the atlas page to retrieve.</param>
	/// <returns>The <see cref="SFTexture"/> for the specified page.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if the specified <paramref name="pageId"/> does not exist in the atlas manager.
	/// </exception>
	public SFTexture GetPageTexture(int pageId)
	{
		if (!_pagesById.TryGetValue(pageId, out var page))
			throw new ArgumentOutOfRangeException(nameof(pageId));
		return page.Texture;
	}

	/// <summary>
	/// Unloads all registered slices that originate from the specified texture.
	/// </summary>
	/// <param name="texHandle">The native handle of the texture to unload.</param>
	/// <remarks>
	/// This method iterates through all registered slices with the given 
	/// <paramref name="texHandle"/> and removes them from both the registry and their 
	/// associated <see cref="AtlasPage"/>.
	/// Pixel usage tracking is adjusted accordingly via <see cref="AtlasPage.RemoveLazy"/>.
	/// </remarks>
	public void UnloadAsset(uint texHandle)
	{
		// find every key for that handle
		var keys = _registered.Keys
			.Where(k => k.texHandle == texHandle)
			.ToList();

		foreach (var key in keys)
		{
			// remove from registry and page
			var info = _registered[key];
			_pagesById[info.Handle.PageId]
				.RemoveLazy(info.Handle.SourceRect);
			_registered.Remove(key);
		}
	}
}