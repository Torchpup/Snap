namespace Snap.Engine.Entities.Graphics;

/// <summary>
/// A render target panel that renders entities or draw commands into an offscreen texture,
/// which can then be drawn to the main screen. Supports batching, draw ordering, and texture atlasing.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RenderTarget"/> class.
/// </remarks>
/// <param name="entities">Optional child entities to add to this panel.</param>
public class RenderTarget(params Entity[] entities) : Panel(entities)
{
	private const int MaxDrawCalls = 256;
	private const int MaxVerticies = 6;

	private readonly Dictionary<uint, List<DrawCommand>> _drawCommands = new(32);
	private int _vertexBufferSize, _batches;
	private SFRenderTexture _rendTexture;
	private SFVertexBuffer _vertexBuffer;
	private SFVertex[] _vertexCache;
	private Texture _texture;
	private SFView _view;
	private Vect2 _offset;
	private long _seqCounter;

	/// <summary>
	/// Indicates whether the render target is currently rendering.
	/// </summary>
	public bool IsRendering { get; private set; }

	// /// <summary>
	// /// Gets or sets the color tint applied when drawing the render target texture.
	// /// </summary>
	// public Color Color { get; set; } = Color.White;

	/// <summary>
	/// Gets or sets the size of the render target. Resizes and recreates the underlying render texture and view.
	/// </summary>
	public new Vect2 Size
	{
		get => base.Size;
		set
		{
			if (base.Size == value)
				return;
			if (value.X <= 0 || value.Y <= 0)
				return;

			base.Size = value;

			_rendTexture?.Dispose();
			_texture?.Dispose();
			_view?.Dispose();

			_rendTexture = new SFRenderTexture((uint)base.Size.X, (uint)base.Size.Y);
			_texture = new(_rendTexture.Texture);
			_view = new SFView(new SFRectF(0, 0, Size.X, Size.Y));
			_rendTexture.SetView(_view);
		}
	}

	/// <summary>
	/// Called when the entity enters the scene. Initializes vertex buffer, render texture, and view.
	/// Throws if size is invalid or if nested RenderTargets are detected.
	/// </summary>
	protected override void OnEnter()
	{
		if (Size.X <= 0 || Size.Y <= 0)
			throw new Exception();
		if (this.HasAncestorOfType<RenderTarget>())
			throw new Exception(); // cannot have nested render targets
		if (_rendTexture != null && (_rendTexture.Size.X != Size.X || _rendTexture.Size.Y != Size.Y))
		{
			_rendTexture?.Dispose();
			_texture.Dispose();

			_rendTexture = new SFRenderTexture((uint)Size.X, (uint)Size.Y);
			_texture = new Texture(_rendTexture.Texture);
		}

		_vertexBufferSize = MaxDrawCalls;
		_vertexBuffer = new((uint)_vertexBufferSize, SFPrimitiveType.Triangles, SFVertexBuffer.UsageSpecifier.Dynamic);
		_vertexCache = new SFVertex[_vertexBufferSize];
		_view = new SFView(new SFRectF(0, 0, Size.X, Size.Y));
		_rendTexture.SetView(_view);

		base.OnEnter();
	}

	private void EnqueueCommand(uint texHandle, SFTexture tex, SFVertex[] quad, int depth)
	{
		if (!_drawCommands.TryGetValue(texHandle, out var list))
		{
			list = [];
			_drawCommands[texHandle] = list;
		}

		list.Add(new DrawCommand(tex, quad, depth, _seqCounter++));

		// _allCommands.Add(cmd);
	}


	internal void RenderAll()
	{
		var index = 0;
		SFTexture currentTexture = null;
		var allCommands = _drawCommands.Values
			.SelectMany(list => list)
			.OrderBy(cmd => cmd.Depth)
			.ThenBy(cmd => cmd.Sequence);

		foreach (ref readonly var cmd in CollectionsMarshal.AsSpan(allCommands.ToList()))
		{
			bool willOverflow = index + cmd.Vertex.Length > _vertexBufferSize;
			bool textureChanged = currentTexture != null && currentTexture != cmd.Texture;

			if (willOverflow || textureChanged)
			{
				if (index > 0 && currentTexture != null)
					Flush(index, _vertexCache, currentTexture);
				index = 0;

				if (willOverflow)
					EnsureVertexBufferCapacity(index + cmd.Vertex.Length);
			}

			// copy verts into the cache
			var src = cmd.Vertex.AsSpan();
			var dst = _vertexCache.AsSpan(index, src.Length);
			src.CopyTo(dst);
			index += src.Length;

			currentTexture = cmd.Texture;
		}

		// Final flush
		if (index > 0 && currentTexture != null)
			Flush(index, _vertexCache, currentTexture);

		_drawCommands.Clear();
		_seqCounter = 0;
	}

	private void EnsureVertexBufferCapacity(int neededSize)
	{
		if (neededSize <= _vertexBufferSize)
			return;

		// double the size until big enough:
		int newSize = _vertexBufferSize;
		while (newSize < neededSize)
			newSize += EngineSettings.Instance.BatchIncreasment;

		Logger.Instance.Log(LogLevel.Info, $"Resizing Render Target Vertex buffer to {newSize}");

		_vertexBuffer.Dispose();
		_vertexBuffer = new SFVertexBuffer((uint)newSize, SFPrimitiveType.Triangles, SFVertexBuffer.UsageSpecifier.Dynamic);

		Array.Resize(ref _vertexCache, newSize);

		_vertexBufferSize = newSize;
	}

	internal void Flush(int vertexCount, SFVertex[] vertices, SFTexture texture)
	{
		if (vertexCount == 0 || texture == null || texture.IsInvalid)
			return;

		// ZERO OUT any leftover verts so they don't draw
		var totalVerts = vertices.Length;
		if (vertexCount < totalVerts)
			Array.Clear(vertices, vertexCount, totalVerts - vertexCount);

		_vertexBuffer.Update(vertices);

		_rendTexture.Draw(_vertexBuffer, new SFRenderStates
		{
			Texture = texture,
			Transform = SFTransform.Identity,
			BlendMode = SFBlendMode.Alpha,
		});

		_batches++;
	}

	/// <summary>
	/// Gets or sets the offset applied to the view center for panning.
	/// Changing this updates the internal view center position.
	/// </summary>
	public Vect2 Offset
	{
		get => _offset;
		set
		{
			if (_offset == value)
				return;
			_offset = value;

			if (_view == null || _view.IsInvalid)
				throw new Exception();

			_view.Center = new SFVectF(
				Size.X / 2 + _offset.X, Size.Y / 2 + _offset.Y);
		}
	}

	/// <summary>
	/// Adjusts the view's center by the given delta for panning.
	/// </summary>
	/// <param name="delta">Amount to pan the view.</param>
	public void Pan(Vect2 delta) => _view.Center += delta;

	/// <summary>
	/// Zooms the view by the specified factor.
	/// </summary>
	/// <param name="factor">Zoom factor, where &gt;1 zooms in and &lt;1 zooms out.</param>
	public void Zoom(float factor) => _view.Zoom(factor);

	/// <summary>
	/// Updates the render target by clearing, rendering all queued commands,
	/// displaying the render texture, and drawing it to the screen.
	/// </summary>
	protected override void OnUpdate()
	{
		IsRendering = true;

		_rendTexture.Clear(SFColor.Transparent);
		_rendTexture.SetView(_view);

		RenderAll();

		_rendTexture.Display();

		Renderer.DrawBypassAtlas(_texture, Position, _color, Layer);

		IsRendering = false;

		// if (EngineSettings.Instance.DebugDraw)
		// 	BE.Renderer.DrawRectangleOutline(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, 1f, BoxColor.AllShades.Teal);
		base.OnUpdate();
	}


	private Color _color = Color.White;

	// public new Color Color
	// {
	// 	get => _color;
	// 	set
	// 	{
	// 		_color = value;
	// 	}
	// }


	public void OverrideTextureColor(Color color) => _color = color;


	/// <summary>
	/// Draws a texture to the current render target with the specified transform and effects.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to draw.
	/// </param>
	/// <param name="dstRect">
	/// The destination rectangle in render target coordinates where the texture will be drawn.
	/// </param>
	/// <param name="srcRect">
	/// The source rectangle within the texture to sample from.
	/// </param>
	/// <param name="color">
	/// The tint color to apply to the texture.
	/// </param>
	/// <param name="origin">
	/// Optional origin point for rotation and scaling. If <c>null</c>, defaults to the texture’s center.
	/// </param>
	/// <param name="scale">
	/// Optional scale factor. If <c>null</c>, defaults to (1,1).
	/// </param>
	/// <param name="rotation">
	/// Rotation angle in radians (or degrees, depending on your engine convention).
	/// </param>
	/// <param name="effects">
	/// Texture effects to apply (e.g., flipping, mirroring).
	/// </param>
	/// <param name="depth">
	/// The draw order depth. Higher values may render above lower values.
	/// </param>
	/// <remarks>
	/// This method delegates to <c>EngineDraw</c>, which performs the actual rendering.
	/// It provides a unified interface for drawing textures with transformations, color tinting,
	/// and optional effects.
	/// </remarks>
	public void Draw(Texture texture, Rect2 dstRect, Rect2 srcRect, Color color, Vect2? origin = null,
		Vect2? scale = null, float rotation = 0f, TextureEffects effects = TextureEffects.None, int depth = 0) =>
		EngineDraw(texture, dstRect, srcRect, color, origin, scale, rotation, effects, depth);

	/// <summary>
	/// Draws an entire texture to the current render target with the specified transform and effects.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to draw.
	/// </param>
	/// <param name="rect">
	/// The destination rectangle in render target coordinates where the texture will be drawn.
	/// </param>
	/// <param name="color">
	/// The tint color to apply to the texture.
	/// </param>
	/// <param name="origin">
	/// Optional origin point for rotation and scaling. If <c>null</c>, defaults to the texture’s center.
	/// </param>
	/// <param name="scale">
	/// Optional scale factor. If <c>null</c>, defaults to (1,1).
	/// </param>
	/// <param name="rotation">
	/// Rotation angle in radians (or degrees, depending on your engine convention).
	/// </param>
	/// <param name="effects">
	/// Texture effects to apply (e.g., flipping, mirroring).
	/// </param>
	/// <param name="depth">
	/// The draw order depth. Higher values may render above lower values.
	/// </param>
	/// <remarks>
	/// This overload automatically uses <see cref="Texture.Bounds"/> as the source rectangle,
	/// making it convenient for drawing the entire texture without specifying a source region.
	/// </remarks>
	public void Draw(Texture texture, Rect2 rect, Color color, Vect2? origin = null,
		Vect2? scale = null, float rotation = 0f, TextureEffects effects = TextureEffects.None, int depth = 0) =>
		EngineDraw(texture, rect, texture.Bounds, color, origin, scale, rotation, effects, depth);

	/// <summary>
	/// Draws a portion of a texture at the specified position with the given transform and effects.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to draw.
	/// </param>
	/// <param name="position">
	/// The position in render target coordinates where the source rectangle will be drawn.
	/// </param>
	/// <param name="srcRect">
	/// The source rectangle within the texture to sample from.
	/// </param>
	/// <param name="color">
	/// The tint color to apply to the texture.
	/// </param>
	/// <param name="origin">
	/// Optional origin point for rotation and scaling. If <c>null</c>, defaults to the center of <paramref name="srcRect"/>.
	/// </param>
	/// <param name="scale">
	/// Optional scale factor. If <c>null</c>, defaults to (1,1).
	/// </param>
	/// <param name="rotation">
	/// Rotation angle in radians (or degrees, depending on your engine convention).
	/// </param>
	/// <param name="effects">
	/// Texture effects to apply (e.g., flipping, mirroring).
	/// </param>
	/// <param name="depth">
	/// The draw order depth. Higher values may render above lower values.
	/// </param>
	/// <remarks>
	/// This overload constructs the destination rectangle from the specified <paramref name="position"/> and
	/// the size of <paramref name="srcRect"/>, then delegates to <c>EngineDraw</c>.
	/// </remarks>
	public void Draw(Texture texture, Vect2 position, Rect2 srcRect, Color color, Vect2? origin = null,
		Vect2? scale = null, float rotation = 0f, TextureEffects effects = TextureEffects.None, int depth = 0) =>
		EngineDraw(texture, new(position, srcRect.Size), srcRect, color, origin, scale, rotation, effects, depth);

	/// <summary>
	/// Draws a portion of a texture at the specified position with a tint color and depth.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to draw.
	/// </param>
	/// <param name="position">
	/// The position in render target coordinates where the source rectangle will be drawn.
	/// </param>
	/// <param name="srcRect">
	/// The source rectangle within the texture to sample from.
	/// </param>
	/// <param name="color">
	/// The tint color to apply to the texture.
	/// </param>
	/// <param name="depth">
	/// The draw order depth. Higher values may render above lower values.
	/// </param>
	/// <remarks>
	/// This overload constructs the destination rectangle from the specified <paramref name="position"/> and
	/// the size of <paramref name="srcRect"/>, then delegates to <c>EngineDraw</c>.  
	/// It omits optional transform parameters (origin, scale, rotation, effects) for convenience when drawing
	/// untransformed sprites.
	/// </remarks>
	public void Draw(Texture texture, Vect2 position, Rect2 srcRect, Color color, int depth = 0) =>
		EngineDraw(texture, new Rect2(position, srcRect.Size), srcRect, color, depth: depth);

	/// <summary>
	/// Draws text using the specified font at the given position with a tint color and depth.
	/// </summary>
	/// <param name="font">
	/// The <see cref="Font"/> to use for rendering the text.
	/// </param>
	/// <param name="text">
	/// The string content to draw.
	/// </param>
	/// <param name="position">
	/// The position in render target coordinates where the text will be drawn.
	/// </param>
	/// <param name="color">
	/// The tint color to apply to the text.
	/// </param>
	/// <param name="depth">
	/// The draw order depth. Higher values may render above lower values.
	/// </param>
	/// <remarks>
	/// This overload delegates to <c>EngineDrawText</c>, providing a simplified interface for text rendering.
	/// It omits optional transform parameters (origin, scale, rotation, effects) for convenience when drawing
	/// untransformed text.
	/// </remarks>
	public void DrawText(Font font, string text, Vect2 position, Color color, int depth = 0)
		=> EngineDrawText(font, text, position, color, depth);

	/// <summary>
	/// Draws a texture directly to the render target, bypassing the engine's internal atlas batching.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to draw.
	/// </param>
	/// <param name="dstRect">
	/// The destination rectangle in render target coordinates where the texture will be drawn.
	/// </param>
	/// <param name="srcRect">
	/// The source rectangle within the texture to sample from.
	/// </param>
	/// <param name="color">
	/// The tint color to apply to the texture.
	/// </param>
	/// <param name="origin">
	/// Optional origin point for rotation and scaling. If <c>null</c>, defaults to the center of <paramref name="srcRect"/>.
	/// </param>
	/// <param name="scale">
	/// Optional scale factor. If <c>null</c>, defaults to (1,1).
	/// </param>
	/// <param name="rotation">
	/// Rotation angle in radians (or degrees, depending on your engine convention).
	/// </param>
	/// <param name="effects">
	/// Texture effects to apply (e.g., flipping, mirroring).
	/// </param>
	/// <param name="depth">
	/// The draw order depth. Higher values may render above lower values.
	/// </param>
	/// <remarks>
	/// Unlike the standard <c>Draw</c> methods, this overload calls <c>EngineDrawBypassAtlas</c> to skip the engine's
	/// built‑in atlas batching.  
	/// Use this when you need direct control over texture rendering, or when atlas management would cause
	/// unwanted side effects (e.g., custom shaders, dynamic textures).
	/// </remarks>
	public void DrawBypassAtlas(Texture texture, Rect2 dstRect, Rect2 srcRect, Color color, Vect2? origin = null,
		Vect2? scale = null, float rotation = 0f, TextureEffects effects = TextureEffects.None, int depth = 0) =>
		EngineDrawBypassAtlas(texture, dstRect, srcRect, color, origin, scale, rotation, effects, depth);

	/// <summary>
	/// Draws a portion of a texture at the specified position, bypassing the engine's internal atlas batching.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to draw.
	/// </param>
	/// <param name="position">
	/// The position in render target coordinates where the texture will be drawn.
	/// </param>
	/// <param name="srcRect">
	/// The source rectangle within the texture to sample from.
	/// </param>
	/// <param name="color">
	/// The tint color to apply to the texture.
	/// </param>
	/// <param name="origin">
	/// Optional origin point for rotation and scaling. If <c>null</c>, defaults to the center of <paramref name="srcRect"/>.
	/// </param>
	/// <param name="scale">
	/// Optional scale factor. If <c>null</c>, defaults to (1,1).
	/// </param>
	/// <param name="rotation">
	/// Rotation angle in radians (or degrees, depending on your engine convention).
	/// </param>
	/// <param name="effects">
	/// Texture effects to apply (e.g., flipping, mirroring).
	/// </param>
	/// <param name="depth">
	/// The draw order depth. Higher values may render above lower values.
	/// </param>
	/// <remarks>
	/// This overload constructs the destination rectangle from the specified <paramref name="position"/> and the
	/// texture’s size, then delegates to <c>EngineDrawBypassAtlas</c>.  
	/// Use this when you need direct control over rendering without atlas batching, such as dynamic textures
	/// or custom shaders.
	/// </remarks>
	public void DrawBypassAtlas(Texture texture, Vect2 position, Rect2 srcRect, Color color, Vect2? origin = null,
		Vect2? scale = null, float rotation = 0f, TextureEffects effects = TextureEffects.None, int depth = 0) =>
		EngineDrawBypassAtlas(texture, new Rect2(position, texture.Size), srcRect, color, origin, scale, rotation, effects, depth);

	/// <summary>
	/// Draws an entire texture into the specified destination rectangle, bypassing the engine's internal atlas batching.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to draw.
	/// </param>
	/// <param name="rect">
	/// The destination rectangle in render target coordinates where the texture will be drawn.
	/// </param>
	/// <param name="color">
	/// The tint color to apply to the texture.
	/// </param>
	/// <param name="depth">
	/// The draw order depth. Higher values may render above lower values.
	/// </param>
	/// <remarks>
	/// This overload automatically uses <see cref="Texture.Bounds"/> as the source rectangle, ensuring the entire texture
	/// is drawn.  
	/// Unlike the standard <c>Draw</c> methods, this bypasses the engine's atlas batching by calling
	/// <c>EngineDrawBypassAtlas</c>.  
	/// Use this when you need direct control over rendering without atlas batching, such as dynamic textures,
	/// custom shaders, or debugging raw draw behavior.
	/// </remarks>
	public void DrawBypassAtlas(Texture texture, Rect2 rect, Color color, int depth = 0) =>
		EngineDrawBypassAtlas(texture, rect, texture.Bounds, color, depth: depth);

	/// <summary>
	/// Draws an entire texture at the specified position, bypassing the engine's internal atlas batching.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to draw.
	/// </param>
	/// <param name="position">
	/// The position in render target coordinates where the texture will be drawn.
	/// </param>
	/// <param name="color">
	/// The tint color to apply to the texture.
	/// </param>
	/// <param name="depth">
	/// The draw order depth. Higher values may render above lower values.
	/// </param>
	/// <remarks>
	/// This overload constructs the destination rectangle from the specified <paramref name="position"/> and the
	/// texture’s size, then delegates to <c>EngineDrawBypassAtlas</c>.  
	/// Unlike the standard <c>Draw</c> methods, this bypasses the engine’s atlas batching, making it useful for
	/// direct rendering of dynamic textures, debugging, or cases where batching is undesirable.
	/// </remarks>
	public void DrawBypassAtlas(Texture texture, Vect2 position, Color color, int depth = 0) =>
		EngineDrawBypassAtlas(texture, new Rect2(position, texture.Size), texture.Bounds, color, depth: depth);

	/// <summary>
	/// Draws a portion of a texture into the specified destination rectangle, bypassing the engine's internal atlas batching.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to draw.
	/// </param>
	/// <param name="dst">
	/// The destination rectangle in render target coordinates where the texture will be drawn.
	/// </param>
	/// <param name="src">
	/// The source rectangle within the texture to sample from.
	/// </param>
	/// <param name="color">
	/// The tint color to apply to the texture.
	/// </param>
	/// <param name="depth">
	/// The draw order depth. Higher values may render above lower values.
	/// </param>
	/// <remarks>
	/// This overload delegates to <c>EngineDrawBypassAtlas</c>, allowing explicit control over both source and destination rectangles.  
	/// Unlike the standard <c>Draw</c> methods, this bypasses the engine’s atlas batching, making it useful for direct rendering
	/// of dynamic textures, debugging, or cases where batching is undesirable.
	/// </remarks>
	public void DrawBypassAtlas(Texture texture, Rect2 dst, Rect2 src, Color color, int depth = 0) =>
		EngineDrawBypassAtlas(texture, dst, src, color, depth: depth);






	private void EnqueueDraw(
		SFTexture texture,
		SFRectI srcIntRect,
		Rect2 dstRect,
		Color color,
		Vect2? origin = null,
		Vect2? scale = null,
		float rotation = 0f,
		TextureEffects effects = TextureEffects.None,
		int depth = 0)
	{
		// Try atlas first (only if it fits)
		if (srcIntRect.Width <= TextureAtlasManager.Instance.PageSize &&
			srcIntRect.Height <= TextureAtlasManager.Instance.PageSize)
		{
			var maybeHandle = TextureAtlasManager.Instance.GetOrCreateSlice(texture, srcIntRect);
			if (maybeHandle.HasValue)
			{
				// build quad from atlas
				var pageTex = TextureAtlasManager.Instance.GetPageTexture(maybeHandle.Value.PageId);
				var sr = maybeHandle.Value.SourceRect;
				var atlasSrc = new Rect2(sr.Left, sr.Top, sr.Width, sr.Height);

				var quad = Renderer.DrawQuad(
					texture,
					dstRect, atlasSrc, color,
					origin ?? Vect2.Zero, scale ?? Vect2.One,
					rotation, effects
				);

				EnqueueCommand(pageTex.NativeHandle, pageTex, quad, depth);
				return;
			}
		}

		// Fallback: direct‐draw from the original texture
		var directSrc = new Rect2(
			srcIntRect.Left, srcIntRect.Top,
			srcIntRect.Width, srcIntRect.Height
		);

		var directQuad = Renderer.DrawQuad(
			texture,
			dstRect, directSrc, color,
			origin ?? Vect2.Zero, scale ?? Vect2.One,
			rotation, effects
		);

		EnqueueCommand(texture.NativeHandle, texture, directQuad, depth);
	}








	private unsafe void EngineDrawText(Font font, string text, Vect2 position, Color color, int depth)
	{
		var fontTex = font.GetTexture();
		if (fontTex.IsInvalid) return;

		Vect2 offset = Vect2.Zero;
		fixed (char* p = text)
		{
			for (int i = 0; i < text.Length; i++)
			{
				char c = p[i];
				if (c == '\r') continue;
				if (c == '\n')
				{
					offset.X = 0;
					offset.Y += font.LineSpacing;
					continue;
				}

				if (!font.Glyphs.TryGetValue(c, out var g)) continue;

				// compute on‐screen dst rect
				var dst = new Rect2(
					new Vect2(position.X + offset.X + g.XOffset,
							  position.Y + offset.Y + g.YOffset),
					new Vect2(g.Cell.Width, g.Cell.Height)
				);

				// source in font texture
				var srcInt = new SFRectI(
					(int)g.Cell.Left,
					(int)g.Cell.Top,
					(int)g.Cell.Width,
					(int)g.Cell.Height
				);

				EnqueueDraw(fontTex, srcInt, dst, color, depth: depth);

				offset.X += g.Advance;
			}
		}
	}

	private void EngineDrawBypassAtlas(
	Texture texture,
	Rect2 dstRect,
	Rect2 srcRect,
	Color color,
	Vect2? origin = null,
	Vect2? scale = null,
	float rotation = 0f,
	TextureEffects effects = TextureEffects.None,
	int depth = 0)
	{
		// if (!texture.IsValid)
		// 	texture.Load();

		var quad = Renderer.DrawQuad(
			texture,
			dstRect,
			srcRect,
			color,
			origin ?? Vect2.Zero,
			scale ?? Vect2.One,
			rotation,
			effects);

		uint textureId = texture.Handle;

		if (!_drawCommands.TryGetValue(textureId, out var list))
		{
			list = [];
			_drawCommands[textureId] = list;
		}

		list.Add(new DrawCommand(texture, quad, depth, _seqCounter++));
	}

	private void EngineDraw(
	Texture texture,
	Rect2 dstRect,
	Rect2 srcRect,
	Color color,
	Vect2? origin = null,
	Vect2? scale = null,
	float rotation = 0f,
	TextureEffects effects = TextureEffects.None,
	int depth = 0)
	{
		// if (!_camera.CullBounds.Intersects(dstRect))
		// 	return;

		if (!texture.IsValid)
			texture.Load();

		// convert float‐Rect2 → IntRect
		var srcInt = new SFRectI(
			(int)srcRect.Left,
			(int)srcRect.Top,
			(int)srcRect.Width,
			(int)srcRect.Height
		);

		EnqueueDraw(texture, srcInt, dstRect, color, origin, scale, rotation, effects, depth);
	}

	/// <summary>
	/// Waits until the renderer has completed its work, then invokes the specified callback.
	/// </summary>
	/// <param name="onReady">
	/// An <see cref="Action"/> to invoke once rendering is finished. If <c>null</c>, no callback is executed.
	/// </param>
	/// <returns>
	/// An <see cref="IEnumerator"/> suitable for use with Unity coroutines. The coroutine yields until
	/// <c>IsRendering</c> becomes <c>false</c>.
	/// </returns>
	/// <remarks>
	/// This method uses <see cref="WaitWhile"/> to suspend execution while <c>IsRendering</c> is true.
	/// Once rendering completes, the provided callback is invoked.
	/// </remarks>
	protected IEnumerator WaitForRenderer(Action onReady)
	{
		yield return new WaitWhile(() => IsRendering);

		onReady?.Invoke();
	}
}
