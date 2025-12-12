namespace Snap.Engine.Screens.Helpers;

/// <summary>
/// Represents a transitional screen in the application.
/// </summary>
/// <remarks>
/// A <see cref="TransitionScreen"/> is typically used to manage visual or logical transitions
/// between two different screens in the application. Examples include fade‑in/fade‑out effects,
/// loading indicators, or splash sequences.
/// </remarks>
public class TransitionScreen : Screen
{
	private readonly Screen[] _screens;
	private ColorRect _rect;

	/// <summary>
	/// Gets or sets the background color used for rendering.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="EngineSettings.ClearColor"/>.
	/// </remarks>
	public Color Color { get; set; } = EngineSettings.Instance.ClearColor;

	/// <summary>
	/// Gets or sets the easing function applied when transitioning in.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="EaseType.Linear"/>.
	/// </remarks>
	public EaseType EaseIn { get; set; } = EaseType.Linear;

	/// <summary>
	/// Gets or sets the easing function applied when transitioning out.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="EaseType.Linear"/>.
	/// </remarks>
	public EaseType EaseOut { get; set; } = EaseType.Linear;

	/// <summary>
	/// Gets or sets the duration, in seconds, of the ease-in transition.
	/// </summary>
	/// <remarks>
	/// Defaults to 0.5 seconds.
	/// </remarks>
	public float EaseInTime { get; set; } = 0.5f;

	/// <summary>
	/// Gets or sets the duration, in seconds, of the ease-out transition.
	/// </summary>
	/// <remarks>
	/// Defaults to 0.5 seconds.
	/// </remarks>
	public float EaseOutTime { get; set; } = 0.5f;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransitionScreen"/> class.
	/// </summary>
	/// <param name="screens">
	/// The collection of <see cref="Screen"/> objects to be transitioned.
	/// </param>
	/// <remarks>
	/// By default, the <see cref="Screen.Layer"/> property is set to 100 to ensure
	/// the transition screen renders above most other layers.
	/// </remarks>
	public TransitionScreen(params Screen[] screens)
	{
		_screens = screens;

		Layer = 100; // Default to 100
	}

	/// <summary>
	/// Called when the screen is entered.
	/// </summary>
	/// <remarks>
	/// This method adds a transparent <see cref="ColorRect"/> entity to the scene,
	/// starts the transition routine, and then invokes the base implementation.
	/// </remarks>
	protected override void OnEnter()
	{
		AddEntity(_rect = new ColorRect()
		{
			Color = Color * 0f,
		});

		StartRoutine(Transition());

		base.OnEnter();
	}

	private IEnumerator Transition()
	{
		var toRemove = ScreenManager.Screens.Where(x => x != this).ToList();

		yield return new Tween<float>(0f, 1f, EaseInTime, EaseIn, MathHelpers.SmoothLerp, (f) => _rect.Color = Color * f);

		foreach (var screen in toRemove)
		{
			if (screen == this)
				continue;

			ScreenManager.Remove(screen);

			yield return new WaitUntil(() => GetScreen(screen) == null);
		}

		yield return new WaitForNextFrame();

		foreach (var screen in _screens)
		{
			AddScreen(screen);

			yield return new WaitUntil(() => GetScreen(screen) != null);
		}

		yield return new WaitForNextFrame();

		yield return new Tween<float>(1f, 0f, EaseOutTime, EaseOut, MathHelpers.SmoothLerp, (f) => _rect.Color = Color * f);

		ExitScreen();
	}
}
