namespace Snap.Engine.Assets.Fonts;

/// <summary>
/// Represents a bitmap font parsed from an FNT file. Provides fixed-size glyph rendering 
/// with custom spacing and line height control.
/// </summary>
public sealed class BitmapFont : Font
{
	private readonly float _spacing;
	private readonly float _lineSpacing;
	private readonly bool _smoothing;

	private float _finalLineSpacing;
	private readonly Dictionary<int, SFTexture> _pageTextures = new();

	/// <inheritdoc />
	public override float Spacing => _spacing;

	/// <inheritdoc />
	public override float LineSpacing => _finalLineSpacing + _lineSpacing;

	internal BitmapFont(uint id, string filename, float spacing, float lineSpacing, bool smoothing)
		: base(id, filename)
	{
		_spacing = spacing;
		_lineSpacing = lineSpacing;
		_smoothing = smoothing;
	}

	// inside your BitmapFont / SpriteFont loader class
	private static string Norm(string p) => p.Replace('\\', '/').TrimStart('/');

	/// <summary>
	/// Loads the font data and associated page textures from the configured FNT file.
	/// Uses lazy-loading to ensure data is loaded only once.
	/// </summary>
	/// <returns>The number of bytes read from the FNT file.</returns>
	/// <exception cref="FileNotFoundException">Thrown if the font or associated texture file is not found.</exception>
	public override ulong Load()
	{
		if (IsValid)
			return 0u;

		using var fntStream = AssetManager.OpenStream(Tag);
		using var fntMs = new MemoryStream();
		fntStream.CopyTo(fntMs);
		var fntBytes = fntMs.ToArray();

		var pagesFolder = Norm(Path.GetDirectoryName(Tag) ?? string.Empty);
		var pageCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

		byte[] PageLoader(string fileName)
		{
			var file = Norm(fileName); // some .fnt files contain backslashes
			var pagePath = string.IsNullOrEmpty(pagesFolder) ? file : $"{pagesFolder}/{file}";

			if (pageCache.TryGetValue(pagePath, out var cached))
				return cached;

			using var s = AssetManager.OpenStream(pagePath);
			using var ms = new MemoryStream();
			s.CopyTo(ms);
			var data = ms.ToArray();
			pageCache[pagePath] = data;

			return data;
		}

		InternalLoad(
			fntData: fntBytes,
			pageLoader: PageLoader,
			smoothing: _smoothing
		);

		IsValid = true;

		Length = (ulong)fntBytes.LongLength;
		return Length;
	}

	/// <summary>
	/// Unloads the font data if it is currently loaded.
	/// </summary>
	public override void Unload()
	{
		if (!IsValid)
			return;

		base.Unload();
	}

	/// <summary>
	/// Releases all resources used by the <see cref="BitmapFont"/>.
	/// Disposes texture data and glyph caches.
	/// </summary>
	public override void Dispose()
	{
		if (!IsValid)
			return;

		foreach (var kv in _pageTextures)
			kv.Value.Dispose();
		_pageTextures.Clear();

		Glyphs.Clear();

		base.Dispose();
	}

	/// <summary>
	/// Gets the primary page texture used by this font.
	/// </summary>
	/// <returns>The first available and valid texture, or throws if none are loaded.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no page textures are loaded.</exception>
	public override SFTexture GetTexture()
	{
		if (_pageTextures.TryGetValue(0, out var tex0) && tex0.IsInvalid)
			return tex0;
		if (tex0.IsInvalid)
			Load();

		return _pageTextures.Values.FirstOrDefault(t => t.IsInvalid)
			?? throw new InvalidOperationException("No page texture is loaded.");
	}

	internal void InternalLoad(
		byte[] fntData,
		Func<string, byte[]> pageLoader,
		bool smoothing = false
	)
	{
		using var memStream = new MemoryStream(fntData);
		using var reader = new StreamReader(memStream);

		string line;
		int parsedLineHeight = 0;

		while ((line = reader.ReadLine()) != null)
		{
			if (line.StartsWith("common "))
			{
				var data = ParseKeyValuePairs(line);
				if (data.TryGetValue("lineHeight", out var lh) && int.TryParse(lh, out var li))
					parsedLineHeight = li;
			}
			else if (line.StartsWith("page "))
			{
				var data = ParseKeyValuePairs(line);
				if (data.TryGetValue("id", out var idStr) && int.TryParse(idStr, out var pageId)
				&& data.TryGetValue("file", out var fileNameRaw))
				{
					var fileName = fileNameRaw.Trim('"');
					var imgBytes = pageLoader(fileName);
					if (imgBytes == null || imgBytes.Length == 0)
						throw new FileNotFoundException($"Page image bytes not found for '{fileName}'.");

					var texture = new SFTexture(imgBytes);
					if (smoothing)
						texture.Smooth = true;

					_pageTextures[pageId] = texture;
				}
			}
			else if (line.StartsWith("char "))
			{
				var data = ParseKeyValuePairs(line);
				var g = new Glyph();

				if (data.TryGetValue("id", out var idStr) && uint.TryParse(idStr, out var codePoint))
					g.Character = (char)codePoint;

				if (data.TryGetValue("x", out var xStr) && int.TryParse(xStr, out var x))
					g.Cell.X = x;
				if (data.TryGetValue("y", out var yStr) && int.TryParse(yStr, out var y))
					g.Cell.Y = y;
				if (data.TryGetValue("width", out var wStr) && int.TryParse(wStr, out var w))
					g.Cell.Width = w;
				if (data.TryGetValue("height", out var hStr) && int.TryParse(hStr, out var h))
					g.Cell.Height = h;

				if (data.TryGetValue("xoffset", out var xoStr) && int.TryParse(xoStr, out var xo))
					g.XOffset = xo;
				if (data.TryGetValue("yoffset", out var yoStr) && int.TryParse(yoStr, out var yo))
					g.YOffset = yo;
				if (data.TryGetValue("xadvance", out var xaStr) && int.TryParse(xaStr, out var xa))
					g.Advance = xa;

				if (data.TryGetValue("page", out var pgStr) && int.TryParse(pgStr, out var pg))
					g.Page = pg;

				Glyphs[g.Character] = g;
			}
			else if (line.StartsWith("kerning "))
			{
				var data = ParseKeyValuePairs(line);
				if (data.TryGetValue("first", out var fStr) && uint.TryParse(fStr, out var first)
				&& data.TryGetValue("second", out var sStr) && uint.TryParse(sStr, out var second)
				&& data.TryGetValue("amount", out var amtStr) && int.TryParse(amtStr, out var amount))
				{
					KerningLookup[(first, second)] = amount;
				}
			}
		}

		_finalLineSpacing = parsedLineHeight;
	}

	private static Dictionary<string, string> ParseKeyValuePairs(string line)
	{
		var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		for (int i = 1; i < parts.Length; i++)
		{
			var kv = parts[i].Split('=', 2);
			if (kv.Length == 2)
				dict[kv[0]] = kv[1];
		}

		return dict;
	}
}
