namespace Snap.Engine.Screens;

/// <summary>
/// Represents flags used to indicate what aspects of the screen or entity state are considered "dirty"
/// and require an update or refresh during the next frame.
/// </summary>
[Flags]
public enum DirtyState : uint // Used for Entity and ScreenManager
{
	/// <summary>
	/// No changes are pending.
	/// </summary>
	None,

	/// <summary>
	/// Indicates that the sorting order (e.g., by layer) needs to be updated.
	/// </summary>
	Sort = 1 << 0,

	/// <summary>
	/// Indicates that entities were added or removed and the collection needs to be updated.
	/// </summary>
	AddOrRemove = 1 << 1,
}

/// <summary>
/// Manages all active <see cref="Screen"/> instances, including their ordering, transitions,
/// lifecycle events, and global dirty state tracking.
/// </summary>
public sealed class ScreenManager
{
	private static uint _id;

	private readonly List<Screen> _screens = [];
	private readonly Dictionary<uint, Screen> _screensById = [];
	private readonly List<Screen> _updateScreens = [];

	private DirtyState _dirtyState;

	/// <summary>
	/// Gets the global singleton instance of the <see cref="ScreenManager"/>.
	/// </summary>
	public static ScreenManager Instance { get; private set; }

	/// <summary>
	/// Gets the total number of active screens currently managed.
	/// </summary>
	public int Count => _screens.Count;


	/// <summary>
	/// Gets a read-only list of all active <see cref="Screen"/> instances.
	/// The first screen is rendered behind the others; the last screen is on top.
	/// </summary>
	public IReadOnlyList<Screen> Screens => _screens;

	internal ScreenManager() => Instance ??= this;

	private static readonly Comparison<Screen> SortComparison = (a, b) => a.Layer.CompareTo(b.Layer);

	internal void Update() // Hot Path
	{
		if (_screens.Count == 0)
			return;

		if (_dirtyState != DirtyState.None)
		{
			if (_dirtyState.HasFlag(DirtyState.AddOrRemove))
			{
				_updateScreens.Clear();
				_updateScreens.EnsureCapacity(_screens.Count);

				for (int i = 0; i < _screens.Count; i++)
				{
					var s = _screens[i];

					if (s != null && !s.IsExiting)
						_updateScreens.Add(s);
				}
			}

			if (_dirtyState.HasFlag(DirtyState.Sort))
				_updateScreens.Sort(SortComparison);

			_dirtyState = DirtyState.None;
		}

		var isActive = Game.Instance.IsActive;
		Screen topmostScreen = null;

		for (int i = _updateScreens.Count - 1; i >= 0; i--)
		{
			var screen = _updateScreens[i];

			if (screen != null && !screen.IsUiScreen)
			{
				topmostScreen = screen;
				break; // Found it, exit early
			}
		}

		if (EngineSettings.Instance.DebugDraw != DebugDrawMode.None)
			DebugRenderer.Instance.Begin();

		Renderer.Instance.Begin();

		for (int i = 0; i < _updateScreens.Count; i++)
		{
			var screen = _updateScreens[i];

			if (!screen.IsVisible)
				continue;

			Renderer.Instance.SetCamera(screen.Camera);

			screen.EngineOnUpdate(isActive, screen == topmostScreen);
		}

		Renderer.Instance.End();

		if (EngineSettings.Instance.DebugDraw != DebugDrawMode.None)
			DebugRenderer.Instance.End();
	}



	/// <summary>
	/// Adds one or more <see cref="Screen"/> instances to the screen stack.
	/// </summary>
	/// <param name="screens">An array of screens to add. Screens are added in the order provided, with the last screen rendered on top.</param>
	public void Add(params Screen[] screens)
	{
		if (screens?.Length == 0)
			return;

		for (int i = 0; i < screens.Length; i++)
		{
			var screen = screens[i];

			screen.Id = _id++;
			BeaconManager.Initialize(screen);

			screen.EngineOnEnter();

			_screens.Add(screen);
			_screensById.Add(screen.Id, screen);
		}

		_dirtyState |= DirtyState.AddOrRemove | DirtyState.Sort;
	}

	/// <summary>
	/// Removes one or more <see cref="Screen"/> instances from the screen stack.
	/// </summary>
	/// <param name="screens">An array of screens to remove. Any screens not currently in the stack are ignored.</param>
	public void Remove(params Screen[] screens)
	{
		if (screens?.Length == 0)
			return;

		bool anyRemoved = false;

		for (int i = screens.Length - 1; i >= 0; i--)
		{
			var screen = screens[i];

			if (_screensById.Remove(screen.Id))
				_screens.Remove(screen);
			else
				continue;

			screen.EngineOnExit();
			anyRemoved = true;
		}

		if (anyRemoved)
			_dirtyState |= DirtyState.AddOrRemove | DirtyState.Sort;
	}

	/// <summary>
	/// Retrieves the first screen in the stack that matches the specified type.
	/// </summary>
	/// <typeparam name="T">The type of screen to search for.</typeparam>
	/// <returns>The first matching screen of type <typeparamref name="T"/>, or <c>null</c> if not found.</returns>
	public T Get<T>() where T : Screen => (T)_screens.FirstOrDefault(x => x is T);

	/// <summary>
	/// Retrieves the instance of a screen from the stack by its reference.
	/// </summary>
	/// <param name="screen">The screen instance to search for.</param>
	/// <returns>The matching <see cref="Screen"/> if found; otherwise, <c>null</c>.</returns>
	public Screen Get(Screen screen) => _screensById.TryGetValue(screen.Id, out var s) ? s : null;

	/// <summary>
	/// Retrieves a screen from the stack by its unique identifier.
	/// </summary>
	/// <param name="id">The unique screen ID.</param>
	/// <returns>The <see cref="Screen"/> with the specified ID if found; otherwise, <c>null</c>.</returns>
	public Screen GetById(uint id) => _screensById.TryGetValue(id, out var s) ? s : null;

	/// <summary>
	/// Attempts to retrieve the first screen in the stack that matches the specified type.
	/// </summary>
	/// <typeparam name="T">The type of screen to search for.</typeparam>
	/// <param name="screen">When this method returns, contains the screen if found; otherwise, <c>null</c>.</param>
	/// <returns><c>true</c> if a screen of the specified type was found; otherwise, <c>false</c>.</returns>
	public bool TryGet<T>(out T screen) where T : Screen
	{
		screen = Get<T>();

		return screen != null;
	}

	/// <summary>
	/// Clears all screens from the screen stack and marks the manager as needing an update.
	/// </summary>
	/// <remarks>
	/// This method removes all currently active screens using the <see cref="Remove"/> method
	/// and sets the <see cref="_dirtyState"/> flag to <see cref="DirtyState.AddOrRemove"/>.
	/// </remarks>
	public void Clear()
	{
		if (_screens.Count == 0)
			return;

		Remove([.. _screens]);

		_dirtyState |= DirtyState.AddOrRemove;
	}

	internal void UpdateDirtyState(DirtyState state) => _dirtyState |= state;
}
