namespace Snap.Engine.Entities.Graphics;

/// <summary>
/// Represents a single animation, including its name, texture, frames, playback speed, and loop setting.
/// </summary>
public sealed class Animation
{
	/// <summary>
	/// Gets the name of the animation.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the texture containing the animation frames.
	/// </summary>
	public Texture Texture { get; }

	/// <summary>
	/// Gets the list of frames represented as rectangles within the texture.
	/// </summary>
	public IReadOnlyList<Rect2> Frames { get; }

	/// <summary>
	/// Gets the playback speed of the animation in frames per second.
	/// </summary>
	public float Speed { get; }

	/// <summary>
	/// Gets a value indicating whether the animation loops when it reaches the end.
	/// </summary>
	public bool Looped { get; }

	/// <summary>
	/// Gets a value indicating whether the animation has no name or is considered empty.
	/// </summary>
	public bool IsEmpty => Name.IsEmpty();

	/// <summary>
	/// Gets the duration of each frame in seconds (calculated as 1 / Speed).
	/// </summary>
	public float FrameDuration { get; }

	internal Animation(string name, Texture texture, Rect2[] frames, float speed, bool looped)
	{
		Name = name;
		Texture = texture;
		Frames = frames;
		Speed = speed;
		Looped = looped;

		FrameDuration = 1f / Speed;
	}
}

/// <summary>
/// An entity that handles multiple animations, allowing playback, pausing, and drawing of animated sprites.
/// </summary>
public sealed class AnimatedSprite : Entity
{
	// Micro-opt:
	// No Diretory or property lookup in OnUpdate for frames and duration;
	// Single array lookup instead of Current.Frames[Frame] (two property calls)
	// FrameDuration Fetched once per Play, not devided every frame.

	private const float MinSpeed = 0.0001f;

	private readonly Dictionary<uint, Animation> _animations = new(16);
	private float _frameDelta;
	private int _frame;
	private RenderTarget _rt;
	private bool _rtChecked;
	private uint _currentHash;
	private Rect2[] _currentFrames;
	private float _currentDuration;

	/// <summary>
	/// Gets a value indicating whether an animation is currently playing.
	/// </summary>
	public bool IsPlaying { get; private set; }

	/// <summary>
	/// Gets a value indicating whether the current animation is finished (not playing and not looped).
	/// </summary>
	public bool IsFinished => !IsPlaying && !Current.Looped;

	/// <summary>
	/// Gets or sets the speed multiplier for the animation playback.
	/// </summary>
	public float SpeedScale { get; set; } = 1f;

	/// <summary>
	/// Gets the currently playing animation.
	/// </summary>
	public Animation Current { get; private set; }

	/// <summary>
	/// Gets the current frame index of the playing animation, clamped to valid frame range.
	/// </summary>
	public int Frame => Current.IsEmpty ? 0 : Math.Clamp(_frame, 0, Current.Frames.Count - 1);

	// /// <summary>
	// /// Gets or sets the color to tint the sprite when drawn.
	// /// </summary>
	// public Color Color { get; set; } = Color.White;

	/// <summary>
	/// Gets or sets the origin offset for rendering transformations.
	/// </summary>
	public Vect2 Origin { get; set; } = Vect2.Zero;

	/// <summary>
	/// Gets or sets the rotation angle of the sprite in radians.
	/// </summary>
	public float Rotation { get; set; }

	/// <summary>
	/// Gets or sets the scale factor for the sprite.
	/// </summary>
	public Vect2 Scale { get; set; } = Vect2.One;

	/// <summary>
	/// Gets or sets any special texture effects (e.g. flipping) to apply when drawing.
	/// </summary>
	public TextureEffects Effects { get; set; }

	/// <summary>
	/// Gets or sets the horizontal alignment used when rendering.
	/// </summary>
	public HAlign HAlign { get; set; }

	/// <summary>
	/// Gets or sets the vertical alignment used when rendering.
	/// </summary>
	public VAlign VAlign { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="AnimatedSprite"/> class.
	/// </summary>
	public AnimatedSprite() { }

	/// <summary>
	/// Adds an animation by specifying the frame range and grid size for the texture atlas.
	/// </summary>
	/// <param name="name">The unique animation name.</param>
	/// <param name="texture">The texture atlas containing frames.</param>
	/// <param name="grid">The size of each frame in pixels.</param>
	/// <param name="frame">The single frame index in the texture atlas.</param>
	/// <param name="speed">The playback speed in frames per second.</param>
	/// <param name="looped">Whether the animation should loop.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance (for chaining).</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if grid size is invalid or speed is negative.</exception>
	public AnimatedSprite AddAnimation(Enum name, Texture texture, Vect2 grid, int frame, float speed, bool looped) =>
		AddAnimation(name.ToEnumString(), texture, grid, [frame], speed, looped);

	/// <summary>
	/// Adds an animation by specifying the frame range and grid size for the texture atlas.
	/// </summary>
	/// <param name="name">The unique animation name.</param>
	/// <param name="texture">The texture atlas containing frames.</param>
	/// <param name="grid">The size of each frame in pixels.</param>
	/// <param name="frame">The single frame index in the texture atlas.</param>
	/// <param name="speed">The playback speed in frames per second.</param>
	/// <param name="looped">Whether the animation should loop.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance (for chaining).</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if grid size is invalid or speed is negative.</exception>
	public AnimatedSprite AddAnimation(string name, Texture texture, Vect2 grid, int frame, float speed, bool looped) =>
		AddAnimation(name, texture, grid, [frame], speed, looped);

	/// <summary>
	/// Adds an animation by specifying the frame range and grid size for the texture atlas.
	/// </summary>
	/// <param name="name">The unique animation name.</param>
	/// <param name="texture">The texture atlas containing frames.</param>
	/// <param name="grid">The size of each frame in pixels.</param>
	/// <param name="startFrame">The starting frame index in the texture atlas.</param>
	/// <param name="endFrame">The ending frame index in the texture atlas.</param>
	/// <param name="speed">The playback speed in frames per second.</param>
	/// <param name="looped">Whether the animation should loop.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance (for chaining).</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if grid size is invalid or speed is negative.</exception>
	public AnimatedSprite AddAnimation(Enum name, Texture texture, Vect2 grid, int startFrame, int endFrame,
	float speed, bool looped) => AddAnimation(name.ToEnumString(), texture, grid, startFrame, endFrame, speed, looped);

	/// <summary>
	/// Adds an animation by specifying the frame range and grid size for the texture atlas.
	/// </summary>
	/// <param name="name">The unique animation name.</param>
	/// <param name="texture">The texture atlas containing frames.</param>
	/// <param name="grid">The size of each frame in pixels.</param>
	/// <param name="startFrame">The starting frame index in the texture atlas.</param>
	/// <param name="endFrame">The ending frame index in the texture atlas.</param>
	/// <param name="speed">The playback speed in frames per second.</param>
	/// <param name="looped">Whether the animation should loop.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance (for chaining).</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if grid size is invalid or speed is negative.</exception>
	public AnimatedSprite AddAnimation(string name, Texture texture, Vect2 grid, int startFrame, int endFrame,
	float speed, bool looped)
	{
		if (grid.X <= 0 || grid.Y <= 0) throw new ArgumentOutOfRangeException(
			nameof(grid), grid, "Grid cell with and height must both be greaer than zero.");

		int step = endFrame >= startFrame ? 1 : -1;
		int length = Math.Abs(endFrame - startFrame) + 1;

		var frames = Enumerable
			.Range(0, length)
			.Select(i => startFrame + i * step)
			.ToArray();

		AddAnimation(name, texture, grid, frames, speed, looped);
		return this;
	}

	/// <summary>
	/// Adds an animation by specifying the frame range and grid size for the texture atlas.
	/// </summary>
	/// <param name="name">The unique animation name.</param>
	/// <param name="texture">The texture atlas containing frames.</param>
	/// <param name="grid">The size of each frame in pixels.</param>
	/// <param name="frames">The frames in the texture atlas.</param>
	/// <param name="speed">The playback speed in frames per second.</param>
	/// <param name="looped">Whether the animation should loop.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance (for chaining).</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if grid size is invalid or speed is negative.</exception>
	public AnimatedSprite AddAnimation(Enum name, Texture texture, Vect2 grid, int[] frames, float speed, bool looped) =>
		AddAnimation(name.ToEnumString(), texture, grid, frames, speed, looped);

	/// <summary>
	/// Adds an animation by specifying the frame range and grid size for the texture atlas.
	/// </summary>
	/// <param name="name">The unique animation name.</param>
	/// <param name="texture">The texture atlas containing frames.</param>
	/// <param name="grid">The size of each frame in pixels.</param>
	/// <param name="frames">The frames in the texture atlas.</param>
	/// <param name="speed">The playback speed in frames per second.</param>
	/// <param name="looped">Whether the animation should loop.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance (for chaining).</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if grid size is invalid or speed is negative.</exception>
	public AnimatedSprite AddAnimation(string name, Texture texture, Vect2 grid, int[] frames, float speed, bool looped)
	{
		if (frames == null || frames.Length == 0)
			throw new ArgumentException("Must supply at least one frame index", nameof(frames));
		if (speed < 0)
			throw new ArgumentOutOfRangeException(nameof(speed));
		if (grid.X <= 0 || grid.Y <= 0) throw new ArgumentOutOfRangeException(
			nameof(grid), grid, "Grid cell with and height must both be greaer than zero.");
		
		var tilesPerRow = (int)(texture.Size.X / grid.X);

		var rects = frames.Select(idx =>
		{
			var tile = MapHelpers.To2D(idx, tilesPerRow);
			var pixelPos = new Vect2(tile.X * grid.X, tile.Y * grid.Y);
			return new Rect2(pixelPos, grid);
		})
		.ToArray();

		var clampSpeed = MathF.Max(MinSpeed, speed);
		var anim = new Animation(name, texture, rects, clampSpeed, looped);
		var key = HashHelpers.Cache32(name);

		_animations[key] = anim;
		return this;
	}

	/// <summary>
	/// Stops the current animation playback.
	/// </summary>
	/// <returns>The current <see cref="AnimatedSprite"/> instance.</returns>
	public AnimatedSprite Stop()
	{
		if (!IsPlaying)
			return this;

		IsPlaying = false;
		return this;
	}

	/// <summary>
	/// Starts playing the specified animation.
	/// </summary>
	/// <param name="name">The name of the animation to play.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if animation with the given name does not exist.</exception>
	public AnimatedSprite Play(Enum name) => Play(name.ToEnumString(), false, true);

	/// <summary>
	/// Starts playing the specified animation.
	/// </summary>
	/// <param name="name">The name of the animation to play.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if animation with the given name does not exist.</exception>
	public AnimatedSprite Play(string name) => Play(name, false, true);

	/// <summary>
	/// Starts playing the specified animation.
	/// </summary>
	/// <param name="name">The name of the animation to play.</param>
	/// <param name="repeat">Whether to repeat the animation if it's already playing.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if animation with the given name does not exist.</exception>
	public AnimatedSprite Play(Enum name, bool repeat) => Play(name.ToEnumString(), repeat, true);

	/// <summary>
	/// Starts playing the specified animation.
	/// </summary>
	/// <param name="name">The name of the animation to play.</param>
	/// <param name="repeat">Whether to repeat the animation if it's already playing.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if animation with the given name does not exist.</exception>
	public AnimatedSprite Play(string name, bool repeat) => Play(name, repeat, true);

	/// <summary>
	/// Starts playing the specified animation.
	/// </summary>
	/// <param name="name">The name of the animation to play.</param>
	/// <param name="repeat">Whether to repeat the animation if it's already playing.</param>
	/// <param name="reset">Whether to reset the animation frame index when playing.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if animation with the given name does not exist.</exception>
	public AnimatedSprite Play(Enum name, bool repeat, bool reset) => Play(name.ToEnumString(), repeat, reset);

	/// <summary>
	/// Starts playing the specified animation.
	/// </summary>
	/// <param name="name">The name of the animation to play.</param>
	/// <param name="repeat">Whether to repeat the animation if it's already playing.</param>
	/// <param name="reset">Whether to reset the animation frame index when playing.</param>
	/// <returns>The current <see cref="AnimatedSprite"/> instance.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if animation with the given name does not exist.</exception>
	public AnimatedSprite Play(string name, bool repeat, bool reset)
	{
		var hash = HashHelpers.Cache32(name);
		if (!_animations.TryGetValue(hash, out var anim))
			throw new KeyNotFoundException($"Unable to find animation '{name}'.");

		if (_currentHash == hash && repeat)
			return this;

		if (reset)
		{
			_frame = 0;
			_frameDelta = 0;
		}
		else
		{
			if (!Current.IsEmpty)
			{
				_frame = Math.Clamp(_frame, 0, Current.Frames.Count - 1);
				_frameDelta = 0;
			}
		}

		if (Size.Length() < anim.Frames[0].Size.Length())
			Size = anim.Frames[0].Size;

		Current = anim;
		_currentHash = hash;
		_currentFrames = anim.Frames as Rect2[] ?? [.. anim.Frames];
		_currentDuration = anim.FrameDuration;
		IsPlaying = true;

		return this;
	}

	/// <summary>
	/// Pauses the currently playing animation.
	/// </summary>
	/// <returns>The current <see cref="AnimatedSprite"/> instance.</returns>
	public AnimatedSprite Pause()
	{
		if (!Current.IsEmpty)
			IsPlaying = false;
		return this;
	}

	/// <summary>
	/// Resumes playback of a paused animation.
	/// </summary>
	/// <returns>The current <see cref="AnimatedSprite"/> instance.</returns>
	public AnimatedSprite Resume()
	{
		if (!Current.IsEmpty)
			IsPlaying = false;
		return this;
	}

	/// <summary>
	/// Updates the animation frame based on elapsed time and speed.
	/// Called automatically each frame.
	/// </summary>
	protected override void OnUpdate()
	{
		if (IsPlaying && Current != null)
		{
			var frameDuration = _currentDuration;
			var delta = Clock.DeltaTime * SpeedScale;
			_frameDelta += delta;

			while (_frameDelta >= frameDuration)
			{
				_frameDelta -= frameDuration;
				_frame++;

				if (_frame >= _currentFrames.Length)
				{
					if (Current.Looped)
						_frame = 0;
					else
					{
						_frame = _currentFrames.Length - 1;
						IsPlaying = false;
						break;
					}
				}
			}
		}

		if (Current != null)
			UpdateDraw();

		base.OnUpdate();
	}

	private void UpdateDraw()
	{
		if (!_rtChecked)
		{
			this.TryGetAncestorOfType(out _rt);
			_rtChecked = true;
		}

		var idx = Frame;
		var frame = _currentFrames[idx];
		var size = frame.Size;
		var offsetX = AlignHelpers.AlignWidth(Size.X, size.X, HAlign);
		var offsetY = AlignHelpers.AlignHeight(Size.Y, size.Y, VAlign);

		if (_rt != null)
		{
			// world-space origin of the RT and into RT-local coords
			var world = this.GetGlobalPosition();
			var rtWorld = _rt.GetGlobalPosition();
			var local = world - rtWorld;
			var final = new Vect2(local.X + offsetX, local.Y + offsetY);

			_rt.Draw(Current.Texture, final, frame, Color, Origin, Scale, Rotation, Effects, Layer);
		}
		else
		{
			var final = new Vect2(Position.X + offsetX, Position.Y + offsetY);

			Renderer.Draw(Current.Texture, final, frame, Color, Origin, Scale, Rotation, Effects, Layer);
		}
	}
}
