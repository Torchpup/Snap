namespace Snap.Engine.Systems;

/// <summary>
/// Represents a 2D camera that controls the view of the game world.
/// Supports smooth following, shaking, and clamping to a specified area.
/// </summary>
public class Camera
{

	private Vect2 _viewCenter, _viewport;
	private readonly SFView _view;
	private readonly Screen _screen;
	private Vect2 _position, _lastDirtyPosition;

	// Shake:
	private float _shakeDuration;         // total seconds of shake
	private float _shakeTimeRemaining;    // seconds left to shake
	private float _shakeMagnitude;        // initial magnitude (in world‐units)
	private Vect2 _orginalOffset;

	// Camera‐fly tween state:
	private bool _isFlying;
	private Vect2 _flyStart;
	private Vect2 _flyTarget;
	private float _flyDuration;
	private float _flyElapsed;
	private EaseType _flyEase;
	private Entity _followTarget;

	/// <summary>
	/// Gets or sets the camera's offset from its target position.
	/// </summary>
	public Vect2 Offset { get; set; }

	/// <summary>
	/// Gets or sets the camera's position in world coordinates.
	/// </summary>
	/// <remarks>
	/// Setting this value updates the view and culling bounds.
	/// </remarks>
	public Vect2 Position
	{
		get => _position;
		set
		{
			if (_position == value)
				return;
			_position = value;

			CullBounds = Rect2.FromCenter(_position, _viewport);
			_view.Center = _position;
			_screen.SetDirtyState(DirtyState.Update);
		}
	}

	/// <summary>
	/// Gets the camera's culling bounds, used for visibility checks.
	/// </summary>
	public Rect2 CullBounds { get; private set; }

	/// <summary>
	/// Gets or sets the camera's clamping rectangle.
	/// The camera will not move outside these bounds.
	/// </summary>
	public Rect2 Clamp { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="Camera"/> class.
	/// </summary>
	/// <param name="screen">The screen associated with this camera.</param>
	internal Camera(Screen screen)
	{
		_isFlying = false;
		_shakeDuration = 0f;
		_shakeTimeRemaining = 0f;
		_shakeMagnitude = 0f;

		_screen = screen;

		var s = EngineSettings.Instance;

		_viewport = s.Viewport;          // e.g. (1280, 720)
		_viewCenter = _viewport / 2f;    // e.g. (640, 360)

		_view = new SFView(new SFRectF(0f, 0f, _viewport.X, _viewport.Y));

		Position = _viewport / 2f;     // e.g. (160, 90)
									   // (Position setter will set CullBounds and _view.Center)

		// Don't remove or change. This makes it so stacking
		// LDTK tiles appear properly. Don't touch or mess with.
		_lastDirtyPosition = Position;
	}

	/// <summary>
	/// Stops following any entity or target.
	/// </summary>
	public void StopFollow() => _followTarget = null;

	/// <summary>
	/// Smoothly moves the camera to the specified target position over a duration.
	/// </summary>
	/// <param name="target">The target position to move to.</param>
	/// <param name="duration">The time, in seconds, to complete the movement.</param>
	/// <param name="ease">The easing function to use for the movement.</param>
	/// <remarks>
	/// If <paramref name="duration"/> is less than or equal to zero, the camera snaps instantly to the target.
	/// </remarks>
	public void Follow(Vect2 target, float duration, EaseType ease)
	{
		_followTarget = null;

		if (duration <= 0f)
		{
			// Instant snap, no tween
			Position = target;
			_isFlying = false;
			return;
		}

		_flyStart = Position;
		_flyTarget = target;
		_flyDuration = duration;
		_flyElapsed = 0f;
		_flyEase = ease;

		_isFlying = true;
	}

	/// <summary>
	/// Makes the camera follow the specified entity.
	/// </summary>
	/// <param name="entity">The entity to follow. If <see langword="null"/>, the camera stops following.</param>
	/// <param name="teleport">
	/// If <see langword="true"/>, the camera instantly snaps to the entity's position.
	/// If <see langword="false"/>, the camera smoothly follows the entity.
	/// </param>
	public void Follow(Entity entity, bool teleport)
	{
		if (entity == null)
		{
			// No entity to fly to—snap to origin or just do nothing.
			return;
		}

		if (teleport)
			Position = entity.Position;

		_followTarget = entity;
		_screen.SetDirtyState(DirtyState.Update);
	}

	/// <summary>
	/// Starts a camera shake effect.
	/// </summary>
	/// <param name="duration">The duration of the shake effect, in seconds.</param>
	/// <param name="magnitude">The maximum offset of the shake effect, in world units.</param>
	/// <remarks>
	/// If either <paramref name="duration"/> or <paramref name="magnitude"/> is less than or equal to zero,
	/// the shake effect is not applied.
	/// </remarks>
	public void StartShake(float duration, float magnitude)
	{
		if (duration <= 0f || magnitude <= 0f)
		{
			// No shake if inputs are invalid.
			_shakeTimeRemaining = 0f;
			return;
		}

		_shakeDuration = duration;
		_shakeTimeRemaining = duration;
		_shakeMagnitude = magnitude;
		_orginalOffset = Position;
	}

	internal void Update(float dt)
	{
		if (_isFlying)
		{
			UpdateFly(dt);
			return;
		}

		Vect2 targetPos = _followTarget != null
			? _followTarget.Position + Offset
			: Position + Offset;

		float tau = 0.3f; // ~0.3s time constant
		float rawT = 1f - MathF.Exp(-dt / tau);
		float t = Easing.Ease(EaseType.Linear, rawT);

		Vect2 desired = Vect2.Lerp(Position, targetPos, t);

		if (!Clamp.IsEmpty)
		{
			desired.X = Math.Clamp(
				desired.X,
				Clamp.Left + _viewCenter.X,
				Clamp.Right - _viewCenter.X
			);
			desired.Y = Math.Clamp(
				desired.Y,
				Clamp.Top + _viewCenter.Y,
				Clamp.Bottom - _viewCenter.Y
			);
		}

		if (_shakeTimeRemaining > 0f)
		{
			_shakeTimeRemaining -= dt;
			if (_shakeTimeRemaining < 0f)
				_shakeTimeRemaining = 0f;

			float normalized = _shakeTimeRemaining / _shakeDuration; // 1→0
			float currentMag = _shakeMagnitude * normalized;
			float offsetX = FastRandom.Instance.RangeFloat(-1, 1f) * currentMag;
			float offsetY = FastRandom.Instance.RangeFloat(-1, 1f) * currentMag;
			desired += new Vect2(offsetX, offsetY);

			if (_shakeTimeRemaining <= 0)
				desired = _orginalOffset;
		}

		Position = desired;

		const float pixelThreshold = 1.0f;
		if ((desired - _lastDirtyPosition).Length() >= pixelThreshold)
		{
			_screen.SetDirtyState(DirtyState.Update);
		}
	}

	private void UpdateFly(float dt)
	{
		_flyElapsed += dt;
		if (_flyElapsed >= _flyDuration)
		{
			// Tween is done
			Position = _flyTarget;
			_isFlying = false;
			return;
		}

		float tNorm = _flyElapsed / _flyDuration;
		float tEase = Easing.Ease(_flyEase, tNorm);

		Vect2 newPos = Vect2.Lerp(_flyStart, _flyTarget, tEase);

		Position = newPos;
	}


	internal SFView ToEngine => _view;
}
