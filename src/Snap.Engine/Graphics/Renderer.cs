namespace Snap.Engine.Graphics;

internal readonly struct DrawCommand
{
	public SFTexture Texture { get; }
	public SFVertex[] Vertex { get; }
	public int Depth { get; }
	public long Sequence { get; }

	internal DrawCommand(SFTexture texture, SFVertex[] vertex, int depth, long seq)
	{
		Texture = texture;
		Vertex = vertex;
		Depth = depth;
		Sequence = seq;
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
	private const int MaxVerticies = 6;
	private const float TexelOffset = 0.05f;

	private SFVertexBuffer _vertexBuffer;
	private SFVertex[] _vertexCache;
	private readonly Dictionary<uint, List<DrawCommand>> _drawCommands = new(16);
	private long _seqCounter = 0;
	private int _vertexBufferSize, _batches;
	private Camera _camera;
	private readonly List<SFVertex[]> _rentedQuads = new(256);
	private static readonly ObjectPool<SFVertex[]> QuadPool =
		new(() => new SFVertex[6], quad =>
		{
			// Clear the array for reuse
			for (int i = 0; i < 6; i++)
				quad[i] = default;
		});

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
	/// <remarks>
	/// This property reflects the active viewport dimensions as defined in <see cref="EngineSettings.Instance"/>.  
	/// It can be used to align rendering operations with the engine’s configured resolution.
	/// </remarks>
	public Vect2 Size => EngineSettings.Instance.Viewport;

	internal Renderer(int maxDrawCalls = 512)
	{
		Instance ??= this;

		_vertexBufferSize = maxDrawCalls;
		_vertexBuffer = new((uint)_vertexBufferSize, SFPrimitiveType.Triangles, SFVertexBuffer.UsageSpecifier.Dynamic);
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

		// list.Add(new DrawCommand(tex, quad, depth, _seqCounter++));
		var cmd = new DrawCommand(tex, quad, depth, _seqCounter++);

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
	/// <remarks>
	/// This method delegates to <c>EngineDraw</c>, which performs the actual batched rendering.  
	/// It supports atlas packing by allowing partial texture regions via <paramref name="srcRect"/>.  
	/// The combination of <paramref name="origin"/>, <paramref name="scale"/>, and <paramref name="rotation"/> 
	/// provides full transformation control for sprite rendering.
	/// </remarks>
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
	/// <remarks>
	/// This overload is a convenience method that uses the full <see cref="Texture.Bounds"/> as the source rectangle.  
	/// It delegates to <c>EngineDraw</c> with the provided destination rectangle and transformation parameters.
	/// </remarks>
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
	/// <remarks>
	/// This overload is a convenience method that constructs a destination rectangle from the given
	/// <paramref name="position"/> and the size of <paramref name="srcRect"/>.  
	/// It delegates to <c>EngineDraw</c> with the calculated destination rectangle and provided parameters.
	/// </remarks>
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
	/// <remarks>
	/// This overload is a convenience method that constructs a destination rectangle from the given
	/// <paramref name="position"/> and the size of <paramref name="srcRect"/>.  
	/// It delegates to <c>EngineDraw</c> with default transformation parameters (no origin, scale, or rotation).
	/// </remarks>
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
	/// <remarks>
	/// This overload is the simplest form of <c>Draw</c>.  
	/// It constructs a destination rectangle from the given <paramref name="position"/> and the full texture size,  
	/// then delegates to <c>EngineDraw</c> with default transformation parameters (no origin, scale, or rotation).
	/// </remarks>
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
	/// <remarks>
	/// This method delegates to <c>EngineDrawText</c>, which performs the actual batched text rendering.  
	/// It supports layering via <paramref name="depth"/> and applies the specified <paramref name="color"/> tint.  
	/// The <paramref name="font"/> determines glyph metrics, spacing, and rendering style.
	/// </remarks>
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
	/// <remarks>
	/// Unlike <see cref="Renderer.Draw(Texture,Rect2,Rect2,Color,Vect2?,Vect2?,float,TextureEffects,int)"/>,  
	/// this method bypasses the atlas packing system and draws the texture directly.  
	/// It is useful for cases where textures are not part of an atlas or when direct rendering is required.
	/// </remarks>
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
	/// <remarks>
	/// This overload constructs a destination rectangle from the given <paramref name="position"/> and the full texture size,  
	/// then delegates to <c>EngineDrawBypassAtlas</c> with the provided source rectangle and transformation parameters.  
	/// Unlike the standard <c>Draw</c> methods, this bypasses atlas packing and renders the texture directly.
	/// </remarks>
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
	/// <remarks>
	/// This overload is a convenience method that uses the full <see cref="Texture.Bounds"/> as the source rectangle.  
	/// It delegates to <c>EngineDrawBypassAtlas</c> with the provided destination rectangle and default transformation parameters.  
	/// Unlike the standard <c>Draw</c> methods, this bypasses atlas packing and renders the texture directly.
	/// </remarks>
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
	/// <remarks>
	/// This overload constructs a destination rectangle from the given <paramref name="position"/> and the full texture size,  
	/// then delegates to <c>EngineDrawBypassAtlas</c> with the full <see cref="Texture.Bounds"/> as the source rectangle.  
	/// Unlike the standard <c>Draw</c> methods, this bypasses atlas packing and renders the texture directly.
	/// </remarks>
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
	/// <remarks>
	/// This overload delegates directly to <c>EngineDrawBypassAtlas</c> with the provided destination and source rectangles.  
	/// Unlike the standard <c>Draw</c> methods, this bypasses atlas packing and renders the texture directly.  
	/// It is useful for cases where textures are not part of an atlas or when direct rendering is required.
	/// </remarks>
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
		if (!_camera.CullBounds.Intersects(dstRect))
			return;
		if (!texture.IsValid)
			texture.Load();

		// convert float Rect2 → IntRect
		var srcInt = new SFRectI(
			(int)srcRect.Left,
			(int)srcRect.Top,
			(int)srcRect.Width,
			(int)srcRect.Height
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
		// var result = new SFVertex[MaxVerticies];
		var result = QuadPool.Rent();
		_rentedQuads.Add(result);

		QuadBuilder.BuildQuad(result, dstRect, srcRect, color, origin, scale, rotation, effects, texture);

		// Compute a pivot that accounts for both origin and scale exactly once:
		// float pivotX = origin.X * dstRect.Width * scale.X;
		// float pivotY = origin.Y * dstRect.Height * scale.Y;

		// // Build “local” corner positions already multiplied by scale:
		// var localPos = new SFVectF[4];
		// localPos[0] = new SFVectF(-pivotX, -pivotY);
		// localPos[1] = new SFVectF(dstRect.Width * scale.X - pivotX, -pivotY);
		// localPos[2] = new SFVectF(dstRect.Width * scale.X - pivotX, dstRect.Height * scale.Y - pivotY);
		// localPos[3] = new SFVectF(-pivotX, dstRect.Height * scale.Y - pivotY);

		// float cos = MathF.Cos(rotation);
		// float sin = MathF.Sin(rotation);

		// // Rotate each corner and then translate by (dstRect.X, dstRect.Y) + pivot
		// for (int i = 0; i < localPos.Length; i++)
		// {
		// 	float x = localPos[i].X;
		// 	float y = localPos[i].Y;

		// 	localPos[i].X = cos * x - sin * y + dstRect.X + pivotX;
		// 	localPos[i].Y = sin * x + cos * y + dstRect.Y + pivotY;
		// }

		// // Texture coordinates (UVs)
		// float u1 = srcRect.Left;
		// float v1 = srcRect.Top;
		// float u2 = srcRect.Right;
		// float v2 = srcRect.Bottom;

		// if (effects.HasFlag(TextureEffects.FlipHorizontal))
		// {
		// 	(u1, u2) = (u2, u1);
		// }
		// if (effects.HasFlag(TextureEffects.FlipVertical))
		// {
		// 	(v1, v2) = (v2, v1);
		// }

		// if (EngineSettings.Instance.HalfTexelOffset)
		// {
		// 	float texelOffsetX = TexelOffset / texture.Size.X;
		// 	float texelOffsetY = TexelOffset / texture.Size.Y;

		// 	if (u1 < u2) { u1 += texelOffsetX; u2 -= texelOffsetX; }
		// 	else { u1 -= texelOffsetX; u2 += texelOffsetX; }

		// 	if (v1 < v2) { v1 += texelOffsetY; v2 -= texelOffsetY; }
		// 	else { v1 -= texelOffsetY; v2 += texelOffsetY; }
		// }

		// // Build two triangles (6 vertices)
		// result[0] = new SFVertex(localPos[0], color, new SFVectF(u1, v1));
		// result[1] = new SFVertex(localPos[1], color, new SFVectF(u2, v1));
		// result[2] = new SFVertex(localPos[3], color, new SFVectF(u1, v2));
		// result[3] = new SFVertex(localPos[1], color, new SFVectF(u2, v1));
		// result[4] = new SFVertex(localPos[2], color, new SFVectF(u2, v2));
		// result[5] = new SFVertex(localPos[3], color, new SFVectF(u1, v2));

		return result;
	}



	internal void Begin(Camera camera)
	{
		Game.Instance.ToRenderer.SetView(camera.ToEngine);

		_camera = camera;

		DrawCalls = (int)_vertexBuffer.VertexCount;
		Batches = _batches;

		_drawCommands.Clear();
		_batches = 0;
	}





	internal void End()
	{
		var index = 0;
		SFTexture currentTexture = null;

		// var allCommands = _drawCommands.Values
		// 	.SelectMany(list => list)
		// 	.OrderBy(cmd => cmd.Depth)
		// 	.ThenBy(cmd => cmd.Sequence);
		var allCommands = _drawCommands.Values
			.SelectMany(list => list);

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
				{
					EnsureVertexBufferCapacity(_vertexBufferSize + cmd.Vertex.Length);
				}
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

		// reset for next frame
		_drawCommands.Clear();
		_seqCounter = 0;

		ReturnAllQuads();
	}

	private void EnsureVertexBufferCapacity(int neededSize)
	{
		if (neededSize <= _vertexBufferSize)
			return;

		// double the size until big enough:
		int newSize = _vertexBufferSize;
		while (newSize < neededSize)
			newSize += EngineSettings.Instance.BatchIncreasment;

		Logger.Instance.Log(LogLevel.Info, $"[Renderer]: Resizing vertex buffer array to {newSize}");

		_vertexBuffer.Dispose();
		_vertexBuffer = new SFVertexBuffer((uint)newSize, SFPrimitiveType.Triangles, SFVertexBuffer.UsageSpecifier.Dynamic);

		Array.Resize(ref _vertexCache, newSize);

		_vertexBufferSize = newSize;
	}

	internal void Flush(int vertexCount, SFVertex[] vertices, SFTexture texture)
	{
		if (vertexCount == 0 || texture == null)
			return;

		// ZERO OUT any leftover verts so they don't draw
		var totalVerts = vertices.Length;

		if (vertexCount < totalVerts)
			Array.Clear(vertices, vertexCount, totalVerts - vertexCount);

		_vertexBuffer.Update(vertices);

		Game.Instance.ToRenderer.Draw(_vertexBuffer, new SFRenderStates
		{
			Texture = texture,
			Transform = SFTransform.Identity,
			BlendMode = SFBlendMode.Alpha,
		});

		_batches++;
	}

	private void ReturnAllQuads()
	{
		foreach (var quad in _rentedQuads)
		{
			QuadPool.Return(quad);
		}
		_rentedQuads.Clear();
	}
}

