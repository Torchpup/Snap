namespace Snap.Engine.Graphics;

internal readonly struct DrawCommand
{
	public SFTexture Texture { get; }
	public SFVertex[] Vertex { get; }
	public int Depth { get; }
	public long Sequence { get; }
	public int CameraId { get; }

	internal DrawCommand(SFTexture texture, SFVertex[] vertex, int depth, long seq, int cameraId)
	{
		Texture = texture;
		Vertex = vertex;
		Depth = depth;
		Sequence = seq;
		CameraId = cameraId;
	}
}

/// <summary>
/// Provides batched rendering functionality for sprites and manages engine-level texture atlas packing.
/// </summary>
/// <remarks>
/// <see cref="Renderer"/> is a sealed class that optimizes 2D rendering by batching draw calls and
/// reducing graphics state changes. In addition to sprite batching, it also performs engine atlas packing,
/// ensuring that multiple textures are combined into shared atlases for efficient GPU usage.
/// 
/// Responsibilities include:
/// <list type="bullet">
///   <item>
///     <description>Managing the sprite batch lifecycle (Begin, Draw, End, Flush).</description>
///   </item>
///   <item>
///     <description>Applying transformations such as position, rotation, scale, and origin.</description>
///   </item>
///   <item>
///     <description>Handling render states like blending, depth, and sampler settings.</description>
///   </item>
///   <item>
///     <description>Performing texture atlas packing, merging smaller textures into larger atlases
///     to minimize texture switches and improve performance.</description>
///   </item>
///   <item>
///     <description>Providing access to packed atlas regions for efficient sprite rendering.</description>
///   </item>
/// </list>
/// </remarks>
public sealed class Renderer
{
	// Fix: 1 Red-Flag: InsertSord does List.Insert - O(n2) Total => Saves ~0.8ms
	// Fix: 2 Red-Flag: #2 I sort twice
	// Fix: 3 Red-Flag: Quad Arrays built eagerly, than copied
	//
	// ===========================================================================
	//
	// After "Red-Flag" Fixes, ADD SIMD:
	//
	// Fix: Add SIMD Support for the renderer, should get an 70% increase
	// from the red-flags fixes alone. Also, defer quad builder to End(), 
	// architecture ready supports SIMD.

	private const int MaxVerticies = 6;
	// private const float TexelOffset = 0.05f;

	private int _activeVertexCount = 0;
	private SFVertexBuffer _vertexBuffer;
	private SFVertex[] _vertexCache;
	private readonly Dictionary<uint, List<DrawCommand>> _drawCommands = new(1024);
	private static long _seqCounter = 0;
	private int _vertexBufferSize, _batches;
	private Camera _camera;
	private int _currentCameraId = 0;
	private int _nextCameraId = 1;
	private readonly Dictionary<Camera, int> _cameraIdMap = [];
	private readonly Dictionary<int, Camera> _cameraById = [];
	private readonly List<SFVertex[]> _rentedQuads = new(1024);
	private readonly List<DrawCommand> _tempCommandList = new(1024);
	private static readonly ObjectPool<SFVertex[]> QuadPool = new(() =>
		new SFVertex[6], quad =>
		{
			// Clear the array for reuse
			for (int i = 0; i < 6; i++)
				quad[i] = default;
		}
	);

	/// <summary>
	/// Gets the number of individual draw calls issued during the current frame.
	/// </summary>
	/// <remarks>
	/// This property is incremented internally whenever a draw operation is submitted.  
	/// It can be used for performance diagnostics and profiling to measure rendering efficiency.
	/// </remarks>
	public int DrawCalls { get; private set; }

	/// <summary>
	/// Gets the number of sprite batches processed during the current frame.
	/// </summary>
	/// <remarks>
	/// A batch represents a group of draw calls combined to minimize state changes.  
	/// This property is useful for understanding how effectively the renderer is batching sprites.
	/// </remarks>
	public int Batches { get; private set; }

	/// <summary>
	/// Gets the singleton instance of the <see cref="Renderer"/>.
	/// </summary>
	/// <remarks>
	/// The renderer is implemented as a sealed singleton, ensuring a single global instance
	/// responsible for sprite batching and atlas packing throughout the engine.
	/// </remarks>
	public static Renderer Instance { get; private set; }

	/// <summary>
	/// Gets the current viewport size of the renderer.
	/// </summary>
	public Vect2 Size => EngineSettings.Instance.Viewport;

	/// <summary>
	/// Gets the current viewport width in pixels.
	/// </summary>
	public int Width => (int)Size.X;

	/// <summary>
	/// Gets the current viewport height in pixels.
	/// </summary>
	public int Height => (int)Size.Y;

	internal Renderer(int maxDrawCalls = 8192)
	{
		Instance ??= this;

		_vertexBufferSize = maxDrawCalls;
		_vertexBuffer = new((uint)_vertexBufferSize, SFPrimitiveType.Triangles, SFVertexBuffer.UsageSpecifier.Stream);
		_vertexCache = new SFVertex[_vertexBufferSize];
	}

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

				var quad = DrawQuad(
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

		var directQuad = DrawQuad(
			texture,
			dstRect, directSrc, color,
			origin ?? Vect2.Zero, scale ?? Vect2.One,
			rotation, effects
		);

		EnqueueCommand(texture.NativeHandle, texture, directQuad, depth);
	}

	private void EnqueueCommand(
		uint texHandle,
		SFTexture tex,
		SFVertex[] quad,
		int depth
	)
	{
		if (!_drawCommands.TryGetValue(texHandle, out var list))
		{
			list = [];
			_drawCommands[texHandle] = list;
		}

		var cmd = new DrawCommand(tex, quad, depth, _seqCounter++, _currentCameraId);

		InsertSorted(list, cmd);
	}
	private static void InsertSorted(List<DrawCommand> list, DrawCommand cmd)
	{
		// Binary search for insertion point
		int index = list.BinarySearch(cmd, DrawCommandComparer.Instance);
		if (index < 0) index = ~index; // Bitwise complement gives insertion point
		list.Insert(index, cmd);
	}
	private class DrawCommandComparer : IComparer<DrawCommand>
	{
		public static readonly DrawCommandComparer Instance = new();

		public int Compare(DrawCommand x, DrawCommand y)
		{
			int cameraCompare = x.CameraId.CompareTo(y.CameraId);
			if (cameraCompare != 0) return cameraCompare;

			int depthCompare = x.Depth.CompareTo(y.Depth);
			if (depthCompare != 0) return depthCompare;

			return x.Sequence.CompareTo(y.Sequence);
		}
	}


	/// <summary>
	/// Draws a textured sprite to the render target with the specified destination, source, and rendering parameters.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to render. Must not be <c>null</c>.
	/// </param>
	/// <param name="dstRect">
	/// A <see cref="Rect2"/> defining the destination rectangle in screen space where the sprite will be drawn.
	/// </param>
	/// <param name="srcRect">
	/// A <see cref="Rect2"/> defining the source rectangle within the texture to sample from.  
	/// Useful for atlas packing or partial texture rendering.
	/// </param>
	/// <param name="color">
	/// A <see cref="Color"/> tint applied to the sprite.  
	/// Use <see cref="Color.White"/> for no tint.
	/// </param>
	/// <param name="origin">
	/// An optional <see cref="Vect2"/> specifying the origin point for transformations (rotation, scaling).  
	/// Defaults to <c>null</c>, which uses the top-left corner.
	/// </param>
	/// <param name="scale">
	/// An optional <see cref="Vect2"/> specifying scaling factors for the sprite.  
	/// Defaults to <c>null</c>, which uses a scale of (1,1).
	/// </param>
	/// <param name="rotation">
	/// The rotation angle in radians applied around the <paramref name="origin"/>.  
	/// Defaults to 0 (no rotation).
	/// </param>
	/// <param name="effects">
	/// A <see cref="TextureEffects"/> flag specifying sprite effects such as flipping.  
	/// Defaults to <see cref="TextureEffects.None"/>.
	/// </param>
	/// <param name="depth">
	/// The draw depth (z-order) of the sprite.  
	/// Lower values are rendered first; higher values appear on top.
	/// </param>
	public void Draw(Texture texture, Rect2 dstRect, Rect2 srcRect, Color color, Vect2? origin = null,
		Vect2? scale = null, float rotation = 0f, TextureEffects effects = TextureEffects.None, int depth = 0) =>
		EngineDraw(texture, dstRect, srcRect, color, origin, scale, rotation, effects, depth);

	/// <summary>
	/// Draws a textured sprite to the render target using the entire texture as the source.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to render. Must not be <c>null</c>.  
	/// The full bounds of the texture are used as the source region.
	/// </param>
	/// <param name="rect">
	/// A <see cref="Rect2"/> defining the destination rectangle in screen space where the sprite will be drawn.
	/// </param>
	/// <param name="color">
	/// A <see cref="Color"/> tint applied to the sprite.  
	/// Use <see cref="Color.White"/> for no tint.
	/// </param>
	/// <param name="origin">
	/// An optional <see cref="Vect2"/> specifying the origin point for transformations (rotation, scaling).  
	/// Defaults to <c>null</c>, which uses the top-left corner.
	/// </param>
	/// <param name="scale">
	/// An optional <see cref="Vect2"/> specifying scaling factors for the sprite.  
	/// Defaults to <c>null</c>, which uses a scale of (1,1).
	/// </param>
	/// <param name="rotation">
	/// The rotation angle in radians applied around the <paramref name="origin"/>.  
	/// Defaults to 0 (no rotation).
	/// </param>
	/// <param name="effects">
	/// A <see cref="TextureEffects"/> flag specifying sprite effects such as flipping.  
	/// Defaults to <see cref="TextureEffects.None"/>.
	/// </param>
	/// <param name="depth">
	/// The draw depth (z-order) of the sprite.  
	/// Lower values are rendered first; higher values appear on top.
	/// </param>
	public void Draw(Texture texture, Rect2 rect, Color color, Vect2? origin = null,
		Vect2? scale = null, float rotation = 0f, TextureEffects effects = TextureEffects.None, int depth = 0) =>
		EngineDraw(texture, rect, texture.Bounds, color, origin, scale, rotation, effects, depth);

	/// <summary>
	/// Draws a textured sprite to the render target at the specified position,
	/// using a source rectangle from the texture.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to render. Must not be <c>null</c>.
	/// </param>
	/// <param name="position">
	/// A <see cref="Vect2"/> specifying the screen-space position where the sprite will be drawn.  
	/// The size of the sprite is determined by <paramref name="srcRect"/>.
	/// </param>
	/// <param name="srcRect">
	/// A <see cref="Rect2"/> defining the source rectangle within the texture to sample from.  
	/// Useful for atlas packing or partial texture rendering.
	/// </param>
	/// <param name="color">
	/// A <see cref="Color"/> tint applied to the sprite.  
	/// Use <see cref="Color.White"/> for no tint.
	/// </param>
	/// <param name="origin">
	/// An optional <see cref="Vect2"/> specifying the origin point for transformations (rotation, scaling).  
	/// Defaults to <c>null</c>, which uses the top-left corner.
	/// </param>
	/// <param name="scale">
	/// An optional <see cref="Vect2"/> specifying scaling factors for the sprite.  
	/// Defaults to <c>null</c>, which uses a scale of (1,1).
	/// </param>
	/// <param name="rotation">
	/// The rotation angle in radians applied around the <paramref name="origin"/>.  
	/// Defaults to 0 (no rotation).
	/// </param>
	/// <param name="effects">
	/// A <see cref="TextureEffects"/> flag specifying sprite effects such as flipping.  
	/// Defaults to <see cref="TextureEffects.None"/>.
	/// </param>
	/// <param name="depth">
	/// The draw depth (z-order) of the sprite.  
	/// Lower values are rendered first; higher values appear on top.
	/// </param>
	public void Draw(Texture texture, Vect2 position, Rect2 srcRect, Color color, Vect2? origin = null,
		Vect2? scale = null, float rotation = 0f, TextureEffects effects = TextureEffects.None, int depth = 0) =>
		EngineDraw(texture, new(position, srcRect.Size), srcRect, color, origin, scale, rotation, effects, depth);

	/// <summary>
	/// Draws a textured sprite to the render target at the specified position,
	/// using a source rectangle from the texture and default transformation parameters.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to render. Must not be <c>null</c>.
	/// </param>
	/// <param name="position">
	/// A <see cref="Vect2"/> specifying the screen-space position where the sprite will be drawn.  
	/// The size of the sprite is determined by <paramref name="srcRect"/>.
	/// </param>
	/// <param name="srcRect">
	/// A <see cref="Rect2"/> defining the source rectangle within the texture to sample from.  
	/// Useful for atlas packing or partial texture rendering.
	/// </param>
	/// <param name="color">
	/// A <see cref="Color"/> tint applied to the sprite.  
	/// Use <see cref="Color.White"/> for no tint.
	/// </param>
	/// <param name="depth">
	/// The draw depth (z-order) of the sprite.  
	/// Lower values are rendered first; higher values appear on top.  
	/// Defaults to 0.
	/// </param>
	public void Draw(Texture texture, Vect2 position, Rect2 srcRect, Color color, int depth = 0) =>
		EngineDraw(texture, new Rect2(position, srcRect.Size), srcRect, color, depth: depth);

	/// <summary>
	/// Draws a textured sprite to the render target at the specified position,
	/// using the entire texture as the source and default transformation parameters.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to render. Must not be <c>null</c>.  
	/// The full bounds of the texture are used as the source region.
	/// </param>
	/// <param name="position">
	/// A <see cref="Vect2"/> specifying the screen-space position where the sprite will be drawn.  
	/// The size of the sprite is determined by <see cref="Texture.Size"/>.
	/// </param>
	/// <param name="color">
	/// A <see cref="Color"/> tint applied to the sprite.  
	/// Use <see cref="Color.White"/> for no tint.
	/// </param>
	/// <param name="depth">
	/// The draw depth (z-order) of the sprite.  
	/// Lower values are rendered first; higher values appear on top.  
	/// Defaults to 0.
	/// </param>
	public void Draw(Texture texture, Vect2 position, Color color, int depth = 0) =>
		EngineDraw(texture, new Rect2(position, texture.Size), texture.Bounds, color, depth: depth);

	/// <summary>
	/// Draws a string of text to the render target at the specified position.
	/// </summary>
	/// <param name="font">
	/// The <see cref="Font"/> used to render the text. Must not be <c>null</c>.
	/// </param>
	/// <param name="text">
	/// The string of text to render. Must not be <c>null</c> or empty.
	/// </param>
	/// <param name="position">
	/// A <see cref="Vect2"/> specifying the screen-space position where the text will be drawn.  
	/// The position corresponds to the baseline origin of the text.
	/// </param>
	/// <param name="color">
	/// A <see cref="Color"/> tint applied to the text glyphs.  
	/// Use <see cref="Color.White"/> for no tint.
	/// </param>
	/// <param name="depth">
	/// The draw depth (z-order) of the text.  
	/// Lower values are rendered first; higher values appear on top.  
	/// Defaults to 0.
	/// </param>
	public void DrawText(Font font, string text, Vect2 position, Color color, int depth = 0)
		=> EngineDrawText(font, text, position, color, depth);

	/// <summary>
	/// Draws a textured sprite directly to the render target, bypassing the engine’s atlas packing system.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to render. Must not be <c>null</c>.  
	/// This texture is used directly rather than being packed into an atlas.
	/// </param>
	/// <param name="dstRect">
	/// A <see cref="Rect2"/> defining the destination rectangle in screen space where the sprite will be drawn.
	/// </param>
	/// <param name="srcRect">
	/// A <see cref="Rect2"/> defining the source rectangle within the texture to sample from.  
	/// Useful for partial texture rendering.
	/// </param>
	/// <param name="color">
	/// A <see cref="Color"/> tint applied to the sprite.  
	/// Use <see cref="Color.White"/> for no tint.
	/// </param>
	/// <param name="origin">
	/// An optional <see cref="Vect2"/> specifying the origin point for transformations (rotation, scaling).  
	/// Defaults to <c>null</c>, which uses the top-left corner.
	/// </param>
	/// <param name="scale">
	/// An optional <see cref="Vect2"/> specifying scaling factors for the sprite.  
	/// Defaults to <c>null</c>, which uses a scale of (1,1).
	/// </param>
	/// <param name="rotation">
	/// The rotation angle in radians applied around the <paramref name="origin"/>.  
	/// Defaults to 0 (no rotation).
	/// </param>
	/// <param name="effects">
	/// A <see cref="TextureEffects"/> flag specifying sprite effects such as flipping.  
	/// Defaults to <see cref="TextureEffects.None"/>.
	/// </param>
	/// <param name="depth">
	/// The draw depth (z-order) of the sprite.  
	/// Lower values are rendered first; higher values appear on top.  
	/// Defaults to 0.
	/// </param>
	public void DrawBypassAtlas(Texture texture, Rect2 dstRect, Rect2 srcRect, Color color, Vect2? origin = null,
		Vect2? scale = null, float rotation = 0f, TextureEffects effects = TextureEffects.None, int depth = 0) =>
		EngineDrawBypassAtlas(texture, dstRect, srcRect, color, origin, scale, rotation, effects, depth);

	/// <summary>
	/// Draws a textured sprite directly to the render target at the specified position,
	/// bypassing the engine’s atlas packing system.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to render. Must not be <c>null</c>.  
	/// This texture is used directly rather than being packed into an atlas.
	/// </param>
	/// <param name="position">
	/// A <see cref="Vect2"/> specifying the screen-space position where the sprite will be drawn.  
	/// The size of the sprite is determined by <see cref="Texture.Size"/>.
	/// </param>
	/// <param name="srcRect">
	/// A <see cref="Rect2"/> defining the source rectangle within the texture to sample from.  
	/// Useful for partial texture rendering.
	/// </param>
	/// <param name="color">
	/// A <see cref="Color"/> tint applied to the sprite.  
	/// Use <see cref="Color.White"/> for no tint.
	/// </param>
	/// <param name="origin">
	/// An optional <see cref="Vect2"/> specifying the origin point for transformations (rotation, scaling).  
	/// Defaults to <c>null</c>, which uses the top-left corner.
	/// </param>
	/// <param name="scale">
	/// An optional <see cref="Vect2"/> specifying scaling factors for the sprite.  
	/// Defaults to <c>null</c>, which uses a scale of (1,1).
	/// </param>
	/// <param name="rotation">
	/// The rotation angle in radians applied around the <paramref name="origin"/>.  
	/// Defaults to 0 (no rotation).
	/// </param>
	/// <param name="effects">
	/// A <see cref="TextureEffects"/> flag specifying sprite effects such as flipping.  
	/// Defaults to <see cref="TextureEffects.None"/>.
	/// </param>
	/// <param name="depth">
	/// The draw depth (z-order) of the sprite.  
	/// Lower values are rendered first; higher values appear on top.  
	/// Defaults to 0.
	/// </param>
	public void DrawBypassAtlas(Texture texture, Vect2 position, Rect2 srcRect, Color color, Vect2? origin = null,
		Vect2? scale = null, float rotation = 0f, TextureEffects effects = TextureEffects.None, int depth = 0) =>
		EngineDrawBypassAtlas(texture, new Rect2(position, texture.Size), srcRect, color, origin, scale, rotation, effects, depth);

	/// <summary>
	/// Draws a textured sprite directly to the render target at the specified destination rectangle,
	/// bypassing the engine’s atlas packing system.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to render. Must not be <c>null</c>.  
	/// This texture is used directly rather than being packed into an atlas.
	/// </param>
	/// <param name="rect">
	/// A <see cref="Rect2"/> defining the destination rectangle in screen space where the sprite will be drawn.  
	/// The full bounds of the texture are used as the source region.
	/// </param>
	/// <param name="color">
	/// A <see cref="Color"/> tint applied to the sprite.  
	/// Use <see cref="Color.White"/> for no tint.
	/// </param>
	/// <param name="depth">
	/// The draw depth (z-order) of the sprite.  
	/// Lower values are rendered first; higher values appear on top.  
	/// Defaults to 0.
	/// </param>
	public void DrawBypassAtlas(Texture texture, Rect2 rect, Color color, int depth = 0) =>
		EngineDrawBypassAtlas(texture, rect, texture.Bounds, color, depth: depth);

	/// <summary>
	/// Draws a textured sprite directly to the render target at the specified position,
	/// bypassing the engine’s atlas packing system.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to render. Must not be <c>null</c>.  
	/// This texture is used directly rather than being packed into an atlas.
	/// </param>
	/// <param name="position">
	/// A <see cref="Vect2"/> specifying the screen-space position where the sprite will be drawn.  
	/// The size of the sprite is determined by <see cref="Texture.Size"/>.
	/// </param>
	/// <param name="color">
	/// A <see cref="Color"/> tint applied to the sprite.  
	/// Use <see cref="Color.White"/> for no tint.
	/// </param>
	/// <param name="depth">
	/// The draw depth (z-order) of the sprite.  
	/// Lower values are rendered first; higher values appear on top.  
	/// Defaults to 0.
	/// </param>
	public void DrawBypassAtlas(Texture texture, Vect2 position, Color color, int depth = 0) =>
		EngineDrawBypassAtlas(texture, new Rect2(position, texture.Size), texture.Bounds, color, depth: depth);

	/// <summary>
	/// Draws a textured sprite directly to the render target at the specified destination and source rectangles,
	/// bypassing the engine’s atlas packing system.
	/// </summary>
	/// <param name="texture">
	/// The <see cref="Texture"/> to render. Must not be <c>null</c>.  
	/// This texture is used directly rather than being packed into an atlas.
	/// </param>
	/// <param name="dst">
	/// A <see cref="Rect2"/> defining the destination rectangle in screen space where the sprite will be drawn.
	/// </param>
	/// <param name="src">
	/// A <see cref="Rect2"/> defining the source rectangle within the texture to sample from.  
	/// Useful for partial texture rendering.
	/// </param>
	/// <param name="color">
	/// A <see cref="Color"/> tint applied to the sprite.  
	/// Use <see cref="Color.White"/> for no tint.
	/// </param>
	/// <param name="depth">
	/// The draw depth (z-order) of the sprite.  
	/// Lower values are rendered first; higher values appear on top.  
	/// Defaults to 0.
	/// </param>
	public void DrawBypassAtlas(Texture texture, Rect2 dst, Rect2 src, Color color, int depth = 0) =>
		EngineDrawBypassAtlas(texture, dst, src, color, depth: depth);

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
					new Vect2(
						position.X + offset.X + g.XOffset,
						position.Y + offset.Y + g.YOffset
					),
					new Vect2(g.Cell.Width, g.Cell.Height)
				);

				var srcInt = new SFRectI(
					new((int)g.Cell.Left, (int)g.Cell.Top),
					new((int)g.Cell.Width, (int)g.Cell.Height)
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
		if (!texture.IsValid)
			texture.Load();

		var quad = DrawQuad(
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
		list.Add(new DrawCommand(texture, quad, depth, _seqCounter++, _currentCameraId));
	}


	private Camera GetCurrentCamera()
	{
		// Try to find camera by current ID
		foreach (var kvp in _cameraIdMap)
		{
			if (kvp.Value == _currentCameraId)
				return kvp.Key;
		}

		// Fallback to _camera field
		return _camera;
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
		Camera currentCamera = GetCurrentCamera();

		if (!currentCamera.CullBounds.Intersects(dstRect))
			return;
		if (!texture.IsValid)
			texture.Load();

		var srcInt = new SFRectI(
			new((int)srcRect.Left, (int)srcRect.Top),
			new((int)srcRect.Width, (int)srcRect.Height)
		);

		EnqueueDraw(texture, srcInt, dstRect, color, origin, scale, rotation, effects, depth);
	}

	internal SFVertex[] DrawQuad(
		SFTexture texture,
		Rect2 dstRect,
		Rect2 srcRect,
		Color color,
		Vect2 origin,
		Vect2 scale,
		float rotation,
		TextureEffects effects)
	{
		var result = QuadPool.Rent();
		_rentedQuads.Add(result);

		QuadBuilder.BuildQuad(result, dstRect, srcRect, color, origin, scale, rotation, effects, texture);

		return result;
	}



	internal void Begin()
	{
		DrawCalls = (int)_vertexBuffer.VertexCount;
		Batches = _batches;

		_drawCommands.Clear();
		_cameraIdMap.Clear();
		_cameraById.Clear();
		_nextCameraId = 1;
		_currentCameraId = 0;
		_batches = 0;
	}


	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	internal void End() //  Hot Path
	{
		var index = 0;
		SFTexture currentTexture = null;
		int currentCameraId = -1;
		Camera currentCamera = null;

		var totalCommands = _drawCommands.Values.Sum(l => l.Count);

		_tempCommandList.Clear();
		_tempCommandList.EnsureCapacity(totalCommands);
		foreach (var list in _drawCommands.Values)
			_tempCommandList.AddRange(list);

		_tempCommandList.Sort(DrawCommandComparer.Instance);

		foreach (ref readonly var cmd in CollectionsMarshal.AsSpan(_tempCommandList.ToList()))
		{
			bool willOverflow = index + cmd.Vertex.Length > _vertexBufferSize;
			bool textureChanged = currentTexture != null && currentTexture != cmd.Texture;
			bool cameraChanged = currentCameraId != cmd.CameraId;

			if (willOverflow || textureChanged || cameraChanged)
			{
				if (index > 0 && currentTexture != null)
				{
					_activeVertexCount = index;
					Flush(index, _vertexCache, currentTexture);
				}
				_activeVertexCount = 0;
				index = 0;

				if (willOverflow)
				{
					EnsureVertexBufferCapacity(_vertexBufferSize + cmd.Vertex.Length);
				}

				if (cameraChanged)
				{
					var command = cmd;

					_cameraById.TryGetValue(cmd.CameraId, out currentCamera);
					if (currentCamera != null)
						Game.Instance.ToRenderer.SetView(currentCamera.ToEngine);
					currentCameraId = cmd.CameraId;
				}
			}

			unsafe
			{
				int vertexCount = cmd.Vertex.Length;

				// Calculate size in bytes
				int byteCount = vertexCount * sizeof(SFVertex);

				fixed (SFVertex* srcPtr = cmd.Vertex)
				fixed (SFVertex* dstPtr = &_vertexCache[index])
				{
					// Buffer.MemoryCopy is slightly faster than Span.CopyTo for large copies
					Buffer.MemoryCopy(
						srcPtr,           // source
						dstPtr,           // destination  
						byteCount,        // destination size in bytes
						byteCount         // bytes to copy
					);
				}

				index += vertexCount;
			}

			currentTexture = cmd.Texture;
		}

		// Final flush
		if (index > 0 && currentTexture != null)
			Flush(index, _vertexCache, currentTexture);

		// reset for next frame
		_drawCommands.Clear();
		_cameraById.Clear();
		_seqCounter = 0;

		ReturnAllQuads();
	}

	private void EnsureVertexBufferCapacity(int neededSize)
	{
		if (neededSize <= _vertexBufferSize)
			return;

		int newSize = Math.Max(_vertexBufferSize * 2, _vertexBufferSize + EngineSettings.Instance.BatchIncreasment);
		while (newSize < neededSize)
		{
			newSize = Math.Max(newSize * 2, newSize + EngineSettings.Instance.BatchIncreasment);
		}

		Logger.Instance.Log(LogLevel.Info, $"[Renderer]: Resizing vertex buffer array to {newSize}");

		_vertexBuffer.Dispose();
		_vertexBuffer = new SFVertexBuffer((uint)newSize, SFPrimitiveType.Triangles, SFVertexBuffer.UsageSpecifier.Stream);

		Array.Resize(ref _vertexCache, newSize);

		_vertexBufferSize = newSize;
	}

	private void Flush(int vertexCount, SFVertex[] vertices, SFTexture texture)
	{
		if (vertexCount == 0 || texture == null || texture.IsInvalid)
			return;

		var totalVerts = vertices.Length;
		if (vertexCount < totalVerts)
			Array.Clear(vertices, vertexCount, totalVerts - vertexCount);

		// Update entire cleared array
		_vertexBuffer.Update(vertices, (uint)totalVerts, 0);  // Update ALL

		Game.Instance.ToRenderer.Draw(_vertexBuffer, new SFRenderStates
		{
			Texture = texture,
			Transform = SFTransform.Identity,
			BlendMode = SFBlendMode.Alpha,
			CoordinateType = SFML.Graphics.CoordinateType.Pixels
		});

		// VertexArrayPool.Return(exactVertices);
		_batches++;
	}

	private void ReturnAllQuads()
	{
		if (_rentedQuads.Count == 0) return;

		foreach (var quad in _rentedQuads)
		{
			QuadPool.Return(quad);
		}
		_rentedQuads.Clear();
	}

	internal void SetCamera(Camera camera)
	{
		_camera = camera;

		// Get or create ID for this camera
		if (!_cameraIdMap.TryGetValue(camera, out int cameraId))
		{
			cameraId = _nextCameraId++;
			_cameraIdMap[camera] = cameraId;
			_cameraById[cameraId] = camera;
		}

		_currentCameraId = cameraId;
		Game.Instance.ToRenderer.SetView(camera.ToEngine);
	}
}

