namespace Snap.Engine.Assets.Loaders;

/// <summary>
/// Represents a texture asset used for rendering, loaded from file, created from color fill, or backed by render targets.
/// Supports repeat/smooth filtering and on-demand GPU upload.
/// </summary>
public class Texture : IAsset
{
	private enum TextureState
	{
		Create,
		Load,
		RenderTexture,
		Font // for fnt fonts
	}

	private SFImage _image;
	private SFTexture _texture;
	private Vect2 _texSize;
	private Color _texColor;
	private readonly TextureState _state;
	private bool _repeat, _smooth;

	/// <summary>
	/// Gets or sets whether the texture is repeated when rendered.
	/// Changing this after load will update the GPU resource if possible.
	/// </summary>
	public bool RepeatedTexture
	{
		get => _repeat;
		set
		{
			if (_repeat == value)
				return;
			_repeat = value;

			if (_texture?.IsInvalid == false)
				_texture.Repeated = _repeat;
		}
	}

	/// <summary>
	/// Gets or sets whether the texture uses smoothing (linear interpolation) when scaled.
	/// </summary>
	public bool SmoothTexture
	{
		get => _smooth;
		set
		{
			if (_smooth == value)
				return;
			_smooth = value;

			if (_texture?.IsInvalid == false)
				_texture.Smooth = _smooth;
		}
	}

	/// <inheritdoc/>
	public byte[] Data { get; }

	/// <inheritdoc/>
	public uint Id { get; }

	/// <inheritdoc/>
	public string Tag { get; }

	/// <inheritdoc/>
	public bool IsValid { get; private set; }

	/// <inheritdoc/>
	public uint Handle => IsValid ? _texture.NativeHandle : 0;

	/// <summary>
	/// The width of the texture in pixels, or 0 if invalid.
	/// </summary>
	public int Width => (int)Size.X;

	/// <summary>
	/// The height of the texture in pixels, or 0 if invalid.
	/// </summary>
	public int Height => (int)Size.Y;

	/// <summary>
	/// The full size of the texture in pixels as a <see cref="Vect2"/>.
	/// </summary>
	public Vect2 Size
	{
		get
		{
			if (!IsValid)
				Load();

			if (_texSize.IsZero)
				_texSize = new Vect2(_texture.Size.X, _texture.Size.Y);

			return _texSize;
		}
	}

	/// <summary>
	/// The bounding rectangle of the texture in local space, starting at (0,0).
	/// </summary>
	public Rect2 Bounds => new(Vect2.Zero, Size);

	/// <inheritdoc/>
	public DateTime LastAccessTime { get; private set; }


	internal Texture(byte[] data, uint id, string filename, bool repeat, bool smooth)
	{
		Id = id;
		Tag = filename;
		_state = TextureState.Load;
		_smooth = smooth;
		_repeat = repeat;
		Data = data;

		LastAccessTime = DateTime.Now;
	}

	internal Texture(byte[] data, uint id)
	{
		// used for fnt fonts. a must.
		Id = id;
		Tag = $"{data.Length:X8}";
		_texture = new SFTexture(data);
		_state = TextureState.Font;
		IsValid = true;
		LastAccessTime = DateTime.Now;

		Logger.Instance.Log(LogLevel.Info, $"Created FNT Texture with ID: {Id}, Size: (W{_texture.Size.X}, H{_texture.Size.Y})");
	}

	internal Texture(SFTexture texture)
	{
		// Used for Render Target
		Id = AssetManager.Id++;
		_texture = texture;
		_state = TextureState.RenderTexture;
		IsValid = true;
		LastAccessTime = DateTime.Now;

		Logger.Instance.Log(LogLevel.Info, $"Created RT Texture with ID: {Id}, Size: (W{_texture.Size.X}, H{_texture.Size.Y})");
	}

	/// <summary>
	/// Creates a blank white texture of the specified size.
	/// </summary>
	/// <param name="size">Dimensions of the texture.</param>
	public Texture(Vect2 size) : this(size, Color.White) { }

	/// <summary>
	/// Creates a blank texture filled with a specified color.
	/// </summary>
	/// <param name="size">Size in pixels.</param>
	/// <param name="color">Fill color.</param>
	/// <exception cref="Exception">Thrown if <paramref name="size"/> is zero.</exception>
	public Texture(Vect2 size, Color color)
	{
		if (size.IsZero)
			throw new Exception();

		Id = AssetManager.Id++;
		Tag = $"{(int)size.X:X8}{(int)size.Y:X8}{color.R:X8}{color.G:X8}{color.B:X8}{color.A:X8}";
		_texSize = size;
		_texColor = color;
		_state = TextureState.Create;
		IsValid = true;

		CreateTexture();

		LastAccessTime = DateTime.Now;
	}

	/// <summary>
	/// Destructor that disposes GPU resources for dynamically created or render textures.
	/// </summary>
	~Texture()
	{
		if (_state == TextureState.Create || _state == TextureState.RenderTexture)
			Dispose();
	}

	/// <inheritdoc/>
	public void Load()
	{
		if (IsValid)
		{
			LastAccessTime = DateTime.Now;
			return;
		}

		// Created texture should have been initialized on the constructor 
		// not thru here. If the dev created an texture and puts it on
		// the assets manager, than yes, it will need to create the 
		// texture again (if evicted).
		switch (_state)
		{
			case TextureState.Create:
				CreateTexture(); break;

			case TextureState.Load:
				LoadTexture(); break;
		}

		IsValid = true;
		LastAccessTime = DateTime.Now;
	}

	/// <inheritdoc/>
	public void Unload()
	{
		// never unload render textures even in eviction
		if (_state == TextureState.RenderTexture)
			return;
		if (!IsValid)
			return;

		_texture?.Dispose();

		Logger.Instance.Log(LogLevel.Info, $"Unloaded asset with ID {Id}, type: '{GetType().Name}', State: {_state}.");

		IsValid = false;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_texture?.Dispose();

		Logger.Instance.Log(LogLevel.Info, $"Unloaded asset with ID {Id}, type: '{GetType().Name}', State: {_state}.");

		GC.SuppressFinalize(this);

		IsValid = false;
	}

	private void CreateTexture()
	{
		_image = new SFImage(new((uint)_texSize.X, (uint)_texSize.Y), _texColor);
		_texture = new SFTexture(_image);

		Logger.Instance.Log(LogLevel.Info, $"Created Blank Texture with ID: {Id}, Size: (W{_texture.Size.X}, H{_texture.Size.Y})");
	}

	private void LoadTexture()
	{
		_texture = new SFTexture(Data)
		{
			Smooth = _smooth,
			Repeated = _repeat
		};
	}

	/// <summary>
	/// Allows implicit casting of a <see cref="Texture"/> to its underlying <see cref="SFTexture"/> object.
	/// </summary>
	/// <param name="tex">The source texture wrapper.</param>
	/// <returns>The SFML native texture instance.</returns>
	public static implicit operator SFTexture(Texture tex)
	{
		tex.LastAccessTime = DateTime.Now;

		return tex._texture;
	}
}
