namespace Snap.Engine.Entities;

/// <summary>
/// The base class for all entities in the SNAP engine.
/// Supports hierarchical relationships, positioning, sizing, rendering, input handling, coroutines, and beacons.
/// </summary>
public class Entity
{
	internal Screen _screen;
	internal Entity _parent;
	internal Vect2 _position;

	private bool _keepAlive, _visible = true;
	private int _layer = 0;
	private Color _color = Color.White;
	private readonly List<Entity> _children = new(64); // Used for referencing.

	/// <summary>
	/// Gets the screen this entity is currently part of.
	/// </summary>
	public Screen Screen => _screen;

	/// <summary>
	/// Gets the parent of this entity, or null if it is the root.
	/// </summary>
	public Entity Parent => _parent;

	/// <summary>
	/// Gets a read-only list of this entity's children.
	/// </summary>
	public IReadOnlyList<Entity> Children => _children;

	/// <summary>
	/// Returns a list of child entities that are of the specified type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type of entities to filter from the children.</typeparam>
	/// <returns>
	/// A list containing all child entities that are of type <typeparamref name="T"/>.
	/// </returns>
	public IReadOnlyList<T> ChildrenAs<T>() where T : Entity => [.. Children.OfType<T>()];

	/// <summary>
	/// The number of child entities attached to this one.
	/// </summary>
	public int ChildCount => Children.Count;

	/// <summary>
	/// True if this entity is a root (has no parent).
	/// </summary>
	public bool IsParent => _parent == null;

	/// <summary>
	/// True if this entity is a child (has a parent).
	/// </summary>
	public bool IsChild => _parent != null;

	/// <summary>
	/// True if the entity is flagged for removal and is being exited.
	/// </summary>
	public bool IsExiting { get; private set; }

	/// <summary>
	/// Gets the camera currently rendering this entity’s screen.
	/// </summary>
	public Camera Camera => _screen.Camera;

	/// <summary>
	/// True if the entity's screen is active.
	/// </summary>
	public bool IsActive => _screen?.IsActive ?? false;

	/// <summary>
	/// True if this entity belongs to the topmost screen.
	/// </summary>
	public bool IsTopmostScreen => _screen?.IsTopmostScreen ?? false;

	/// <summary>
	/// True if this entity belongs to the currently active screen.
	/// </summary>
	public bool IsActiveScreen => _screen?.IsActivScreen ?? false;

	/// <summary>
	/// The index of this entity among its siblings.
	/// </summary>
	public int ChildIndex => Parent != null
		? Parent.Children.IndexOf(this)
		: Children.IndexOf(this);

	/// <summary>
	/// If true, prevents the engine from removing this entity automatically.
	/// </summary>
	public bool KeepAlive
	{
		get
		{
			if (IsChild)
				return _parent.KeepAlive || _keepAlive;
			return _keepAlive;
		}
		set
		{
			if (_keepAlive == value)
				return;
			_keepAlive = value;

			if (_screen == null)
			{
				CoroutineManager.Start(WaitForNullScreen(() => _screen.SetDirtyState(DirtyState.Sort)));
				return;
			}
			_screen.SetDirtyState(DirtyState.AddOrRemove);
		}
	}

	/// <summary>
	/// Controls whether this entity is visible.
	/// </summary>
	public bool Visible
	{
		get
		{
			if (IsChild)
				return _parent.Visible && _visible;
			return _visible;
		}
		set
		{
			if (_visible == value)
				return;
			_visible = value;

			// Issue Fixed: If visibility is changed during gameplay and the top
			// entity goes invisible. The children wont move up. This forces
			// StackPanels to update, or any panel containing "Panel".

			if (Parent != null)
			{
				foreach (var panel in this.GetAncestorsOfType<Panel>())
					panel.SetDirtyState(DirtyState.Sort | DirtyState.AddOrRemove);
			}
		}
	}

	/// <summary>
	/// The combined color applied to this entity and inherited from its parent.
	/// </summary>
	public Color Color
	{
		get
		{
			if (IsChild)
			{
				if (_parent == null)
					Logger.Log(LogLevel.Warning, $"Parent '{_parent.GetType().Name}' is null on Entity.Color, defaulting to zero");

				return Color.Multiply(_parent.Color, _color);
			}

			return _color;
		}
		set => _color = value;
	}

	/// <summary>
	/// The additive layer value of this entity, relative to its parent.
	/// </summary>
	public int Layer
	{
		get
		{
			if (IsChild)
			{
				if (_parent == null)
					Logger.Log(LogLevel.Warning, $"Parent '{_parent.GetType().Name}' is null on Entity.Layer, defaulting to zero");

				return _parent.Layer + _layer;
			}

			return _layer;
		}
		set
		{
			if (_layer == value)
				return;
			_layer = value;

			if (_screen == null)
			{
				CoroutineManager.Start(WaitForNullScreen(() => _screen.SetDirtyState(DirtyState.Sort)));
				return;
			}

			_screen.SetDirtyState(DirtyState.Sort);
		}
	}

	/// <summary>
	/// The world-space position of this entity, including parent offset.
	/// </summary>
	public Vect2 Position
	{
		get
		{
			if (IsChild)
				return _parent.Position + _position;
			return _position;
		}
		set => _position = value;
	}

	/// <summary>
	/// The local position relative to this entity's parent.
	/// </summary>
	public Vect2 LocalPosition
	{
		get => _position;
		set => _position = value;
	}

	/// <summary>
	/// The size of this entity. Affects layout, bounds, and rendering.
	/// </summary>
	private Vect2 _size = Vect2.One; // <-- Leave as one not zero, will cause the engine to skip these entities

	/// <summary>
	/// Gets the center point of the entity.
	/// </summary>
	/// <remarks>
	/// The center is calculated as half of the entity’s <c>Size</c>, returning
	/// a <see cref="Vect2"/> that represents the midpoint. This is commonly used
	/// for positioning, alignment, and transformations relative to the entity’s
	/// bounds.
	/// </remarks>
	public Vect2 Center => Size / 2f;

	/// <summary>
	/// The size of this entity. Affects layout, bounds, and rendering.
	/// </summary>
	public Vect2 Size
	{
		get => LocalSize;
		set => LocalSize = value;
	}

	/// <summary>
	/// Gets or sets the local size of this entity directly on the backing field,
	/// bypassing any subclass size setter logic.
	/// </summary>
	/// <remarks>
	/// Setting this notifies all ancestor <see cref="Panel"/> instances to recalculate their layouts.
	/// Prefer using <see cref="Size"/> for normal operations; use <see cref="LocalSize"/> internally
	/// when subclass size setters (which may disable auto-sizing) need to be bypassed.
	/// </remarks>
	/// <value>A <see cref="Vect2"/> representing the width and height of this entity.</value>
	public Vect2 LocalSize
	{
		get => _size;
		set
		{
			if (_size == value)
				return;
			_size = value;

			foreach (var e in this.GetAncestorsOfType<Panel>())
				e.SetDirtyState(DirtyState.AddOrRemove | DirtyState.Sort);
		}
	}

	/// <summary>
	/// Gets the world-space bounding rectangle of this entity.
	/// </summary>
	public Rect2 Bounds
	{
		get
		{
			// Fetch hte entity's gobal position regardless.
			var worldPos = this.GetGlobalPosition();

			// Build the rectangle at world position
			return new(worldPos, Size);
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Entity"/> class.
	/// </summary>
	public Entity() { }


	#region Helpers
	/// <summary>
	/// Gets the safe region margin defined in the engine settings.
	/// </summary>
	/// <remarks>
	/// This value typically represents the padding or boundary buffer used
	/// to determine a 'safe' area within the screen or layout system.
	/// </remarks>
	public float SafeRegion => EngineSettings.Instance.SafeRegion;

	/// <summary>
	/// Casts a given entity to the specified type or throws an exception if the cast fails.
	/// </summary>
	/// <typeparam name="TEntity">The type to cast the entity to. Must inherit from <see cref="Entity"/>.</typeparam>
	/// <returns>The entity cast as the specified type.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the provided entity is null.</exception>
	/// <exception cref="InvalidCastException">Thrown if the entity cannot be cast to the requested type.</exception>
	public TEntity EntityAs<TEntity>() where TEntity : Entity
	{
		if (this == null)
			throw new InvalidOperationException("Cannot get parent: no parent is set.");

		if (this is TEntity parent)
			return parent;

		throw new InvalidCastException(
			$"Entity is of type {GetType().Name}, not {typeof(TEntity).Name}");
	}

	/// <summary>
	/// Casts this entity's parent to the specified type.
	/// </summary>
	/// <typeparam name="TParent">The type to cast the parent to. Must inherit from <see cref="Entity"/>.</typeparam>
	/// <returns>The parent cast as the specified type.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no parent is set.</exception>
	/// <exception cref="InvalidCastException">Thrown if the parent cannot be cast to the requested type.</exception>
	public TParent ParentAs<TParent>() where TParent : Entity
		=> _parent.EntityAs<TParent>();

	/// <summary>
	/// Provides access to the shared logging system.
	/// </summary>
	protected Logger Logger => Logger.Instance;

	/// <summary>
	/// Provides access to the engine's global clock.
	/// </summary>
	protected Clock Clock => Clock.Instance;

	/// <summary>
	/// Provides access to the running engine instance.
	/// </summary>
	protected Game Game => Game.Instance;

	/// <summary>
	/// Provides access to the global fast random number generator.
	/// </summary>
	protected FastRandom Rand => FastRandom.Instance;

	/// <summary>
	/// Provides access to the engine's renderer.
	/// </summary>
	protected Renderer Renderer => Renderer.Instance;

	/// <summary>
	/// Provides access to the input map for handling player input.
	/// </summary>
	protected InputMap Input => Game.Instance.Input;

	/// <summary>
	/// Provides access to the asset manager for loading and querying assets.
	/// </summary>
	protected AssetManager Assets => AssetManager.Instance;

	/// <summary>
	/// Provides access to the beacon system for event signaling.
	/// </summary>
	protected BeaconManager Beacon => BeaconManager.Instance;

	/// <summary>
	/// Provides access to the sound manager for playing audio.
	/// </summary>
	protected SoundManager SoundManager => SoundManager.Instance;

	/// <summary>
	/// Provides access to the screen manager for managing scenes and transitions.
	/// </summary>
	protected ScreenManager ScreenManager => ScreenManager.Instance;

	/// <summary>
	/// Provides access to the coroutine manager for asynchronous operations.
	/// </summary>
	protected CoroutineManager CoroutineManager => CoroutineManager.Instance;

	/// <summary>
	/// Provides access to the service manager for registering and retrieving game services.
	/// </summary>
	protected ServiceManager Services => ServiceManager.Instance;

	/// <summary>
	/// Flags this entity for destruction. It will be removed from the screen.
	/// </summary>
	public void Destroy()
	{
		if (IsExiting)
			return;

		Screen.RemoveEntity(this);
	}

	#endregion



	internal void EngineOnEnter()
	{
		OnEnter();
	}

	/// <summary>
	/// Called when the entity is first added to the screen.
	/// Override to perform setup logic.
	/// </summary>
	protected virtual void OnEnter() { }

	internal void EngineOnExit()
	{
		if (IsExiting)
			return;

		ClearBeacons();
		ClearRoutines();
		ClearChildren();

		IsExiting = true;

		foreach (var p in this.GetAncestorsOfType<Panel>())
		{
			p._children.Remove(this);
			p.SetDirtyState(DirtyState.Sort | DirtyState.AddOrRemove);
		}

		OnExit();
	}

	/// <summary>
	/// Called when the entity is removed from the screen.
	/// Override to perform cleanup logic.
	/// </summary>
	protected virtual void OnExit() { }

	internal void EngineOnUpdate()
	{
		if (EngineSettings.Instance.DebugDraw != DebugDrawMode.None)
		{
			var mode = EngineSettings.Instance.DebugDraw;
			var shouldDraw = mode == DebugDrawMode.All
				|| (mode == DebugDrawMode.TopMost && IsOnTopMostLayer())
				|| (mode == DebugDrawMode.BottomMost && IsOnBottomMostLayer());

			if (shouldDraw)
			{
				if (this.TryGetAncestorOfType<RenderTarget>(out var rt))
				{
					var world = this.GetGlobalPosition();
					var box = new Rect2(world, Size);
					var screenBox = Camera.WorldToScreen(box - rt.Offset);

					DebugRenderer.Instance.DrawRect(screenBox, Color.Red);
				}
				else
				{
					var screenBox = Camera.WorldToScreen(Bounds);
					DebugRenderer.Instance.DrawRect(screenBox, Color.Red);
				}
			}
		}

		OnUpdate();
	}


	private bool IsOnTopMostLayer()
	{
		if (Screen == null)
			return false;

		return Screen == ScreenManager.Instance.Screens[^1];
	}

	private bool IsOnBottomMostLayer()
	{
		if (Screen == null)
			return false;

		return Screen == ScreenManager.Instance.Screens[0];
	}


	/// <summary>
	/// Called every frame while the entity is active.
	/// Override to perform per-frame updates.
	/// </summary>
	protected virtual void OnUpdate() { }












	#region Children
	/// <summary>
	/// Adds one or more child entities to this entity.
	/// </summary>
	/// <param name="children">The entities to add.</param>
	public void AddChild(params Entity[] children)
	{
		if (children == null || children.Length == 0)
			return;

		IEnumerator WaitRoutine()
		{
			if (_screen == null)
				yield return new WaitWhile(() => _screen == null);

			var list = new List<Entity>(children.Length);
			for (int i = 0; i < children.Length; i++)
			{
				var c = children[i];
				if (c == null || c.IsExiting)
					continue;

				c._parent = this;
				_children.Add(c);

				list.Add(c);
			}

			if (list.Count > 0)
				_screen.AddEntity([.. list]);
		}

		CoroutineManager.Start(WaitRoutine());
	}

	/// <summary>
	/// Adds children and returns this entity cast to the given type.
	/// Useful for fluent chaining when constructing trees.
	/// </summary>
	/// <typeparam name="TParent">The parent type to return.</typeparam>
	/// <param name="children">The children to add.</param>
	public TParent AddToParent<TParent>(params Entity[] children) where TParent : Entity
	{
		if (children == null || children.Length == 0)
			return (TParent)this;

		for (int i = children.Length - 1; i >= 0; i--)
		{
			var c = children[i];
			if (c == null || c.IsExiting)
				continue;

			if (c._layer >= 0)
				c._layer++;
		}

		AddChild(children);

		return (TParent)this;
	}

	/// <summary>
	/// Removes one or more child entities from this entity.
	/// </summary>
	/// <param name="children">The entities to remove.</param>
	/// <returns>True if at least one child was removed.</returns>
	public bool RemoveChild(params Entity[] children)
	{
		if (children == null || children.Length == 0)
			return false;

		var list = new List<Entity>(children.Length);
		for (int i = 0; i < children.Length; i++)
		{
			var c = children[i];

			if (c == null || c.IsExiting)
				continue;

			if (_children.Remove(c))
			{
				c._parent = null;
				list.Add(c);
			}

			list.Add(c);
		}

		if (list.Count > 0)
		{
			_screen.RemoveEntity([.. list]);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Returns the Nth child of the specified type.
	/// </summary>
	public TEntity GetChild<TEntity>(int index) where TEntity : Entity
	{
		if (index < 0)
			return null;

		int count = 0;
		for (int i = 0; i < _children.Count; i++)
		{
			var c = _children[i];
			if (c is TEntity match)
			{
				if (count == index)
					return match;
				count++;
			}
		}

		return null!;
	}

	/// <summary>
	/// Attempts to retrieve the Nth child of the specified type.
	/// </summary>
	public bool TryGetChild<TEntity>(int index, out TEntity child) where TEntity : Entity
	{
		child = GetChild<TEntity>(index);

		return child != null;
	}

	/// <summary>
	/// Removes all children from this entity.
	/// </summary>
	public void ClearChildren()
	{
		if (_children.Count == 0)
			return;

		var toRemove = _children.ToArray();
		_children.Clear();

		_screen.RemoveEntity(toRemove);
	}
	#endregion



	private IEnumerator WaitForNullScreen(Action onReady)
	{
		yield return new WaitWhile(() => _screen == null);

		onReady?.Invoke();
	}




	#region Beacons
	/// <summary>
	/// Connects this entity to a beacon topic using a string identifier.
	/// </summary>
	/// <param name="topic">The topic name to listen for.</param>
	/// <param name="handler">The handler to invoke when the beacon is emitted.</param>
	public void ConnectBeacon(string topic, Action<BeaconHandle> handler) => Beacon.Connect(topic, this, handler);

	/// <summary>
	/// Connects this entity to a beacon topic using an enum value.
	/// </summary>
	/// <param name="topic">The topic name (enum) to listen for.</param>
	/// <param name="handler">The handler to invoke when the beacon is emitted.</param>
	public void ConnectBeacon(Enum topic, Action<BeaconHandle> handler) => Beacon.Connect(topic.ToEnumString(), this, handler);

	/// <summary>
	/// Disconnects this entity from a beacon topic using a string identifier.
	/// </summary>
	/// <param name="topic">The topic name to disconnect from.</param>
	/// <param name="handler">The handler to remove.</param>
	public void DisconnectBeacon(string topic, Action<BeaconHandle> handler) => Beacon.Disconnect(topic, this, handler);

	/// <summary>
	/// Disconnects this entity from a beacon topic using an enum value.
	/// </summary>
	/// <param name="topic">The topic name (enum) to disconnect from.</param>
	/// <param name="handler">The handler to remove.</param>
	public void DisconnectBeacon(Enum topic, Action<BeaconHandle> handler) => Beacon.Disconnect(topic.ToEnumString(), this, handler);

	/// <summary>
	/// Emits a beacon event immediately with the specified enum topic and arguments.
	/// </summary>
	/// <param name="topic">The topic enum value to emit.</param>
	/// <param name="args">Optional arguments passed with the beacon.</param>
	public void EmitBeacon(string topic, params object[] args) => Beacon.Emit(topic, args);

	/// <summary>
	/// Emits a beacon event immediately with the specified enum topic and arguments.
	/// </summary>
	/// <param name="topic">The topic enum value to emit.</param>
	/// <param name="args">Optional arguments passed with the beacon.</param>
	public void EmitBeacon(Enum topic, params object[] args) => Beacon.Emit(topic, args);

	/// <summary>
	/// Emits a delayed beacon event using an enum topic.
	/// </summary>
	/// <param name="topic">The topic enum value to emit.</param>
	/// <param name="seconds">Delay in seconds before emission.</param>
	/// <param name="args">Optional arguments passed with the beacon.</param>
	public void EmitBeaconDelayed(string topic, float seconds, params object[] args) => Beacon.EmitDelayed(this, topic, seconds, args);

	/// <summary>
	/// Emits a delayed beacon event using an enum topic.
	/// </summary>
	/// <param name="topic">The topic enum value to emit.</param>
	/// <param name="seconds">Delay in seconds before emission.</param>
	/// <param name="args">Optional arguments passed with the beacon.</param>
	public void EmitBeaconDelayed(Enum topic, float seconds, params object[] args) => Beacon.EmitDelayed(this, topic.ToEnumString(), seconds, args);

	/// <summary>
	/// Disconnects this entity from all beacons.
	/// </summary>
	public void ClearBeacons() => Beacon.ClearOwner(this);
	#endregion


	#region Coroutines
	/// <summary>
	/// Starts a coroutine tied to this entity.
	/// </summary>
	/// <param name="routine">The coroutine to start.</param>
	/// <returns>A handle used to manage the coroutine.</returns>
	public CoroutineHandle StartRoutine(IEnumerator routine) => CoroutineManager.Start(routine, this);

	/// <summary>
	/// Starts a coroutine after a delay in seconds.
	/// </summary>
	/// <param name="routine">The coroutine to run.</param>
	/// <param name="delay">Time to wait before starting.</param>
	/// <returns>A handle to the coroutine.</returns>
	public CoroutineHandle StartRoutineDelayed(IEnumerator routine, float delay) => CoroutineManager.StartDelayed(delay, this, routine);

	/// <summary>
	/// Stops the coroutine associated with the given handle.
	/// </summary>
	/// <param name="handle">The coroutine handle.</param>
	/// <returns>True if the coroutine was stopped successfully.</returns>
	public bool StopRoutine(CoroutineHandle handle) => CoroutineManager.Stop(handle);

	/// <summary>
	/// Returns true if the specified coroutine is still running.
	/// </summary>
	/// <param name="handle">The coroutine handle.</param>
	/// <returns>True if active, false otherwise.</returns>
	public bool HasRoutine(CoroutineHandle handle) => CoroutineManager.IsRunning(handle);

	/// <summary>
	/// Stops all coroutines currently running on this entity.
	/// </summary>
	public void ClearRoutines() => CoroutineManager.StopAll(this);
	#endregion


	#region Screen
	/// <summary>
	/// Adds one or more screens to the screen manager.
	/// </summary>
	/// <param name="screens">The screens to add.</param>
	public void AddScreen(params Screen[] screens) => ScreenManager.Add(screens);

	/// <summary>
	/// Removes one or more screens from the screen manager.
	/// </summary>
	/// <param name="screens">The screens to remove.</param>
	public void RemoveScreen(params Screen[] screens) => ScreenManager.Remove(screens);

	/// <summary>
	/// Gets a screen of the specified type.
	/// </summary>
	/// <typeparam name="T">The screen type.</typeparam>
	/// <returns>The screen instance.</returns>
	public T GetScreen<T>() where T : Screen => ScreenManager.Get<T>();

	/// <summary>
	/// Gets a screen instance matching the given reference.
	/// </summary>
	/// <param name="screen">The screen reference.</param>
	/// <returns>The matched screen.</returns>
	public Screen GetScreen(Screen screen) => ScreenManager.Get(screen);

	/// <summary>
	/// Gets a screen by its unique ID.
	/// </summary>
	/// <param name="id">The screen ID.</param>
	/// <returns>The screen with the specified ID, or null if not found.</returns>
	public Screen GetScreenById(uint id) => ScreenManager.GetById(id);
	#endregion
}
