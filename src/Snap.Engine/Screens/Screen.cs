namespace Snap.Engine.Screens;

/// <summary>
/// Represents a visual screen or scene in the engine, responsible for managing and rendering entities, UI, and camera logic.
/// </summary>
/// <remarks>
/// Screens are layered and updated by the <see cref="ScreenManager"/>. Each screen has its own camera, set of entities,
/// and lifecycle events such as <c>OnAdded</c>, <c>OnRemoved</c>, and <c>OnUpdate</c>.  
/// Use <see cref="AddEntity"/> to register entities, and override virtual methods to implement custom behavior.
/// </remarks>
public class Screen
{
	private readonly List<Entity> _entities = [], _updateEntities = [];
	private static readonly Comparison<Entity> SortComparison = (a, b) => a.Layer.CompareTo(b.Layer);

	private int _layer;
	private bool _visible = true;
	private DirtyState _dirtyState = DirtyState.Sort | DirtyState.AddOrRemove;
	private Entity[] _entityArrayBuffer = new Entity[256];
	private int[] _indexBuffer = new int[256];

	/// <summary>
	/// Unique identifier for this screen, assigned by the <see cref="ScreenManager"/>.
	/// </summary>
	public uint Id { get; internal set; }

	/// <summary>
	/// All entities currently managed by this screen.
	/// </summary>
	public IReadOnlyList<Entity> Entities => _entities;

	/// <summary>
	/// Returns a read-only collection of all entities on this screen that match the specified entity type.
	/// This provides a convenient way to access specific types of entities for processing or querying.
	/// </summary>
	/// <typeparam name="TEntity">The type of entity to filter for. Must inherit from <see cref="Entity"/>.</typeparam>
	/// <returns>
	/// A read-only list containing all entities on this screen that are of the specified type.
	/// Returns an empty list if no matching entities are found.
	/// </returns>
	/// <remarks>
	/// This method is useful when you need to work with a particular category of entities,
	/// such as finding all enemies, players, or collectibles on the current screen.
	/// The returned collection provides type-safe access to entities without requiring manual casting.
	/// </remarks>
	public IReadOnlyList<TEntity> EntitiesAs<TEntity>() where TEntity : Entity
		=> [.. Entities.OfType<TEntity>()];

	/// <summary>
	/// Subset of <see cref="Entities"/> that are currently active and updated.
	/// </summary>
	public IReadOnlyList<Entity> ActiveEntities => _updateEntities;

	/// <summary>
	/// Total number of entities in this screen.
	/// </summary>
	public int EntityCount => Entities.Count;

	/// <summary>
	/// The camera associated with this screen. Used for rendering and coordinate transformations.
	/// </summary>
	public Camera Camera { get; private set; }

	/// <summary>
	/// Whether this screen is in the process of exiting (i.e., being removed or faded out).
	/// </summary>
	public bool IsExiting { get; private set; }

	/// <summary>
	/// Whether this screen is currently active and receiving updates.
	/// </summary>
	public bool IsActive { get; private set; }

	/// <summary>
	/// Indicates if this screen is currently the topmost screen in the <see cref="ScreenManager"/> stack.
	/// </summary>
	public bool IsTopmostScreen { get; private set; }

	/// <summary>
	/// Whether this screen is marked as a UI screen. UI screens may render differently or ignore camera transforms.
	/// </summary>
	public bool IsUiScreen { get; set; }

	/// <summary>
	/// Returns true if the screen is active, topmost, and not in the process of exiting.
	/// Useful for gating input or update logic to only the currently focused screen.
	/// </summary>
	public bool IsActivScreen => IsActive && IsTopmostScreen && !IsExiting;

	/// <summary>
	/// Indicates whether the screen is currently visible and should be rendered.
	/// Changing this value triggers a dirty update to the screen manager.
	/// </summary>
	public bool IsVisible
	{
		get => _visible;
		set
		{
			if (_visible == value)
				return;

			_visible = value;

			ScreenManager.UpdateDirtyState(DirtyState.AddOrRemove);
		}
	}

	/// <summary>
	/// The draw order layer of the screen. Lower values are drawn first (i.e., in the background).
	/// Changing this value marks the screen manager's sort state as dirty.
	/// </summary>
	public int Layer
	{
		get => _layer;
		set
		{
			if (_layer == value)
				return;

			_layer = value;

			ScreenManager.UpdateDirtyState(DirtyState.Sort);
		}
	}

	#region Helpers
	/// <summary>
	/// Gets the safe region scale factor from the engine settings.  
	/// This value defines the percentage of the screen considered safe for critical UI elements.
	/// </summary>
	protected float SafeRegion => EngineSettings.Instance.SafeRegion;

	/// <summary>
	/// Provides access to the global logger instance for logging debug or runtime information.
	/// </summary>
	protected Logger Logger => Logger.Instance;

	/// <summary>
	/// Provides access to the global clock used for delta time and time tracking.
	/// </summary>
	protected Clock Clock => Clock.Instance;

	/// <summary>
	/// Gets the singleton instance of the running engine.
	/// </summary>
	protected Game Game => Game.Instance;

	/// <summary>
	/// Gets the global random number generator used for deterministic or fast random operations.
	/// </summary>
	protected FastRandom Rand => FastRandom.Instance;

	/// <summary>
	/// Provides access to the main renderer responsible for drawing entities and visuals.
	/// </summary>
	protected Renderer Renderer => Renderer.Instance;

	/// <summary>
	/// Gets the global input mapping system used for handling keyboard, mouse, and gamepad input.
	/// </summary>
	protected InputMap Input => Game.Instance.Input;

	/// <summary>
	/// Provides access to the asset manager, responsible for loading and managing game assets.
	/// </summary>
	protected AssetManager Assets => AssetManager.Instance;

	/// <summary>
	/// Manages cross-scene communication via beacon signaling and listening.
	/// </summary>
	protected BeaconManager Beacon => BeaconManager.Instance;

	/// <summary>
	/// Handles all sound playback, effects, and audio instances.
	/// </summary>
	protected SoundManager SoundManager => SoundManager.Instance;

	/// <summary>
	/// Provides access to the screen manager which handles scene stacking and transitions.
	/// </summary>
	protected ScreenManager ScreenManager => ScreenManager.Instance;

	/// <summary>
	/// Manages coroutine execution tied to the screen's lifecycle.
	/// </summary>
	protected CoroutineManager CoroutineManager => CoroutineManager.Instance;

	/// <summary>
	/// Provides access to the service manager for registering and retrieving game services.
	/// </summary>
	/// <remarks>
	/// Services are singleton components that provide shared functionality across screens and entities.
	/// Use <see cref="ServiceManager.GetService{T}"/> to retrieve services and 
	/// <see cref="ServiceManager.RegisterService"/> to register new services.
	/// </remarks>
	protected ServiceManager Services => ServiceManager.Instance;


	/// <summary>
	/// Returns the index of the specified entity within the screen's entity list.
	/// </summary>
	/// <param name="entity">The <see cref="Entity"/> to search for.</param>
	/// <returns>
	/// The zero-based index of the entity if found; otherwise, <c>-1</c>.
	/// </returns>
	/// <remarks>
	/// Useful for debugging, ordering logic, or verifying an entity's presence in the screen.
	/// </remarks>
	public int GetEntityIndex(Entity entity) => _entities.IndexOf(entity);


	/// <summary>
	/// Exits and removes this screen from the <see cref="ScreenManager"/>.
	/// </summary>
	/// <remarks>
	/// Marks the screen as exiting and triggers removal from the screen stack.
	/// This does not immediately destroy the screen or its entities but schedules it for cleanup.
	/// </remarks>
	public void ExitScreen()
	{
		if (IsExiting)
			return;

		ScreenManager.Remove(this);
	}
	#endregion

	internal void EngineOnUpdate(bool isActive, bool isTopmostScreen) // Hot Path
	{
		IsActive = isActive;
		IsTopmostScreen = isTopmostScreen;

		Camera.Update(Clock.DeltaTime);

		if (_entities.Count == 0)
		{
			OnUpdate();
			return;
		}

		if (_dirtyState != DirtyState.None)
		{
			if (_dirtyState.HasFlag(DirtyState.AddOrRemove))
			{
				_updateEntities.Clear();
				_updateEntities.EnsureCapacity(_entities.Count);

				for (int i = 0; i < _entities.Count; i++)
				{
					var entity = _entities[i];

					if (entity != null
						&& !entity.IsExiting
						&& (entity.HasAncestorOfType<RenderTarget>()
						|| entity.Bounds.Intersects(Camera.CullBounds) || entity.KeepAlive))
					{
						_updateEntities.Add(entity);
					}
				}
			}

			if (_dirtyState.HasFlag(DirtyState.Sort))
			{
				var list = _updateEntities;
				int count = list.Count;

				if (_entityArrayBuffer.Length < count)
				{
					_entityArrayBuffer = new Entity[count];
					_indexBuffer = new int[count];
				}

				list.CopyTo(_entityArrayBuffer, 0);
				for (int i = 0; i < count; i++) _indexBuffer[i] = i;

				var tempArray = new Entity[count];
				Array.Copy(_entityArrayBuffer, tempArray, count);
				Array.Sort(_indexBuffer, 0, count, Comparer<int>.Create((a, b) =>
				{
					int primary = SortComparison(tempArray[a], tempArray[b]);
					return primary != 0 ? primary : a.CompareTo(b);
				}));

				list.Clear();
				for (int i = 0; i < count; i++)
				{
					list.Add(_entityArrayBuffer[_indexBuffer[i]]);
				}
			}

			_dirtyState = DirtyState.None;
		}

		for (int i = 0; i < _updateEntities.Count; i++)
		{
			var e = _updateEntities[i];

			if (!e.Visible)
				continue;

			e.EngineOnUpdate();
		}

		OnUpdate();
	}

	/// <summary>
	/// Called once per frame while the screen is active, before entities are updated.
	/// </summary>
	/// <remarks>
	/// Override this method to handle input, run logic, or perform screen-level updates that aren't tied to specific entities.
	/// This method can also be used to trigger drawing operations manually if needed, though rendering is typically handled by entities.
	/// </remarks>
	protected virtual void OnUpdate() { }



	internal void EngineOnEnter()
	{
		Camera = new Camera(this);
		Camera.Update(Clock.DeltaTime);

		OnEnter();
	}

	/// <summary>
	/// Called when the screen is added to the <see cref="ScreenManager"/> and becomes active.
	/// </summary>
	/// <remarks>
	/// Override this method to initialize entities, load resources, or register stateful systems specific to this screen.
	/// Called automatically after the internal engine setup completes.
	/// </remarks>
	protected virtual void OnEnter() { }

	internal void EngineOnExit()
	{
		if (IsExiting)
			return;

		ClearBeacons();
		ClearRoutines();
		ClearEntities();

		IsExiting = true;

		OnExit();
	}

	/// <summary>
	/// Called when the screen is being removed from the <see cref="ScreenManager"/> or marked for exit.
	/// </summary>
	/// <remarks>
	/// Override this method to clean up resources, remove entities, or unregister systems related to this screen.
	/// Called automatically before the screen is removed and <see cref="IsExiting"/> is set.
	/// </remarks>
	protected virtual void OnExit() { }








	#region Screen
	/// <summary>
	/// Adds one or more screens to the <see cref="ScreenManager"/>.
	/// </summary>
	/// <param name="screens">The screens to add.</param>
	public void AddScreen(params Screen[] screens) => ScreenManager.Add(screens);

	/// <summary>
	/// Removes one or more screens from the <see cref="ScreenManager"/>.
	/// </summary>
	/// <param name="screens">The screens to remove.</param>
	public void RemoveScreen(params Screen[] screens) => ScreenManager.Remove(screens);

	/// <summary>
	/// Retrieves the first screen of the specified type from the <see cref="ScreenManager"/>.
	/// </summary>
	/// <typeparam name="T">The type of screen to find.</typeparam>
	/// <returns>The screen of type <typeparamref name="T"/> if found; otherwise, <c>null</c>.</returns>
	public T GetScreen<T>() where T : Screen => ScreenManager.Get<T>();

	/// <summary>
	/// Retrieves a screen instance by reference from the <see cref="ScreenManager"/>.
	/// </summary>
	/// <param name="screen">The screen instance to search for.</param>
	/// <returns>The matched screen if found; otherwise, <c>null</c>.</returns>
	public Screen GetScreen(Screen screen) => ScreenManager.Get(screen);

	/// <summary>
	/// Retrieves a screen by its unique identifier from the <see cref="ScreenManager"/>.
	/// </summary>
	/// <param name="id">The ID of the screen.</param>
	/// <returns>The screen with the matching ID, or <c>null</c> if not found.</returns>
	public Screen GetScreenById(uint id) => ScreenManager.GetById(id);
	#endregion


	#region Entity
	/// <summary>
	/// Adds one or more entities to the screen.
	/// </summary>
	/// <param name="entities">The entities to add.</param>
	/// <remarks>
	/// This method initializes each entity by setting its screen reference,
	/// running its <c>EngineOnEnter</c> lifecycle method, and registering it with the <see cref="BeaconManager"/>.
	/// Null or exiting entities are ignored.  
	/// After adding, the screen's dirty state is updated for sorting and update processing.
	/// </remarks>
	public void AddEntity(params Entity[] entities)
	{
		if (entities == null || entities.Length == 0)
			return;

		_entities.EnsureCapacity(entities.Length);
		for (int i = 0; i < entities.Length; i++)
		{
			var e = entities[i];
			if (e == null || e.IsExiting)
				continue;

			_entities.Add(e);

			e._screen = this;
			BeaconManager.Initialize(e);
			e.EngineOnEnter();
		}

		SetDirtyState(DirtyState.Sort | DirtyState.AddOrRemove);
	}

	/// <summary>
	/// Removes one or more entities from the screen.
	/// </summary>
	/// <param name="entities">The entities to remove.</param>
	/// <remarks>
	/// Entities that are <c>null</c> or marked as exiting are skipped.  
	/// If an entity is found and removed, its <c>EngineOnExit</c> method is called.  
	/// If any entities are removed, the screen's dirty state is updated to reflect the changes for sorting and updating.
	/// </remarks>
	public void RemoveEntity(params Entity[] entities)
	{
		if (entities == null || entities.Length == 0)
			return;

		bool anyRemoved = false;

		for (int i = 0; i < entities.Length; i++)
		{
			var e = entities[i];

			if (e == null || e.IsExiting)
				continue;
			if (!_entities.Remove(e))
				continue;

			e.EngineOnExit();
			anyRemoved = true;
		}

		if (anyRemoved)
			SetDirtyState(DirtyState.Sort | DirtyState.AddOrRemove);
	}

	/// <summary>
	/// Gets the entity of type <typeparamref name="T"/> at the specified index,
	/// or <c>null</c> if the index is out of range or type doesn't match.
	/// </summary>
	/// <typeparam name="T">The type of entity to retrieve.</typeparam>
	/// <param name="index">The index in the filtered list of matching entities.</param>
	/// <returns>The entity at the specified index, or <c>null</c> if not found.</returns>
	public T GetEntity<T>(int index) where T : Entity
		=> _entities.OfType<T>().ElementAtOrDefault(index);

	/// <summary>
	/// Tries to get the entity of type <typeparamref name="T"/> at the specified index.
	/// </summary>
	/// <typeparam name="T">The type of entity to retrieve.</typeparam>
	/// <param name="index">The index in the filtered list of matching entities.</param>
	/// <param name="entity">The resulting entity, or <c>null</c> if not found.</param>
	/// <returns><c>true</c> if an entity was found; otherwise, <c>false</c>.</returns>
	public bool TryGetEntity<T>(int index, out T entity) where T : Entity
	{
		entity = GetEntity<T>(index);

		return entity != null;
	}

	/// <summary>
	/// Removes all entities from the screen.
	/// </summary>
	/// <remarks>
	/// Internally calls <see cref="RemoveEntity"/> with all current entities.
	/// If the list is already empty, no action is taken.
	/// </remarks>
	public void ClearEntities()
	{
		if (_entities.Count == 0)
			return;

		RemoveEntity([.. _entities]);
	}
	#endregion


	#region Beacons
	/// <summary>
	/// Connects a beacon handler to a specified topic using a string identifier.
	/// Beacons are a lightweight pub-sub messaging system used to decouple logic between screens, entities, or systems.
	/// Only connected listeners will receive messages, making them similar to Godot signals or Defold's messaging system.
	/// </summary>
	/// <param name="topic">The string name of the beacon topic to listen for.</param>
	/// <param name="handler">The action to invoke when this beacon topic is emitted.</param>
	public void ConnectBeacon(string topic, Action<BeaconHandle> handler) => Beacon.Connect(topic, this, handler);

	/// <summary>
	/// Connects a beacon handler to a specified topic using an enum identifier.
	/// Beacons allow screens and entities to react to emitted messages without needing direct references or tight coupling.
	/// </summary>
	/// <param name="topic">The enum representing the beacon topic to listen for.</param>
	/// <param name="handler">The action to invoke when this beacon topic is emitted.</param>
	public void ConnectBeacon(Enum topic, Action<BeaconHandle> handler) => Beacon.Connect(topic.ToEnumString(), this, handler);

	/// <summary>
	/// Disconnects a previously connected beacon handler from a string-based topic.
	/// </summary>
	/// <param name="topic">The string name of the beacon topic to disconnect from.</param>
	/// <param name="handler">The specific handler that was connected.</param>
	public void DisconnectBeacon(string topic, Action<BeaconHandle> handler) => Beacon.Disconnect(topic, this, handler);

	/// <summary>
	/// Disconnects a previously connected beacon handler from an enum-based topic.
	/// </summary>
	/// <param name="topic">The enum representing the beacon topic to disconnect from.</param>
	/// <param name="handler">The specific handler that was connected.</param>
	public void DisconnectBeacon(Enum topic, Action<BeaconHandle> handler) => Beacon.Disconnect(topic.ToEnumString(), this, handler);

	/// <summary>
	/// Emits a beacon message immediately to all connected listeners of the specified string-based topic.
	/// Beacons allow systems, screens, or entities to communicate without direct references.
	/// </summary>
	/// <param name="topic">The string name of the beacon topic to emit.</param>
	/// <param name="args">Optional arguments passed to the beacon handler.</param>
	public void EmitBeacon(string topic, params object[] args) => Beacon.Emit(topic, args);

	/// <summary>
	/// Emits a beacon message immediately to all connected listeners of the specified enum-based topic.
	/// </summary>
	/// <param name="topic">The enum representing the beacon topic to emit.</param>
	/// <param name="args">Optional arguments passed to the beacon handler.</param>
	public void EmitBeacon(Enum topic, params object[] args) => Beacon.Emit(topic, args);

	/// <summary>
	/// Emits a beacon message after a delay, targeting listeners of a string-based topic.
	/// This can be useful for delayed effects, chain reactions, or timing-based messaging.
	/// </summary>
	/// <param name="topic">The string name of the beacon topic to emit.</param>
	/// <param name="seconds">The delay in seconds before the beacon is emitted.</param>
	/// <param name="args">Optional arguments passed to the beacon handler.</param>
	public void EmitBeaconDelayed(string topic, float seconds, params object[] args) => Beacon.EmitDelayed(this, topic, seconds, args);

	/// <summary>
	/// Emits a beacon message after a delay, targeting listeners of an enum-based topic.
	/// </summary>
	/// <param name="topic">The enum representing the beacon topic to emit.</param>
	/// <param name="seconds">The delay in seconds before the beacon is emitted.</param>
	/// <param name="args">Optional arguments passed to the beacon handler.</param>
	public void EmitBeaconDelayed(Enum topic, float seconds, params object[] args) => Beacon.EmitDelayed(this, topic.ToEnumString(), seconds, args);

	/// <summary>
	/// Clears all beacon connections owned by this screen, disconnecting all handlers tied to its scope.
	/// This is typically called during screen exit or cleanup to prevent dangling references.
	/// </summary>
	public void ClearBeacons() => Beacon.ClearOwner(this);
	#endregion


	#region Coroutines
	/// <summary>
	/// Starts a coroutine associated with this screen. Coroutines allow asynchronous sequences using <c>yield</c> instructions.
	/// </summary>
	/// <param name="routine">The coroutine enumerator to run.</param>
	/// <returns>A handle to control or query the coroutine later.</returns>
	public CoroutineHandle StartRoutine(IEnumerator routine) => CoroutineManager.Start(routine, this);

	/// <summary>
	/// Starts a coroutine after a specified delay in seconds.
	/// </summary>
	/// <param name="routine">The coroutine enumerator to run.</param>
	/// <param name="delay">The delay in seconds before the coroutine starts.</param>
	/// <returns>A handle to control or query the coroutine later.</returns>
	public CoroutineHandle StartRoutineDelayed(IEnumerator routine, float delay) => CoroutineManager.StartDelayed(delay, this, routine);

	/// <summary>
	/// Stops a running coroutine associated with this screen.
	/// </summary>
	/// <param name="handle">The coroutine handle returned by <see cref="StartRoutine"/> or <see cref="StartRoutineDelayed"/>.</param>
	/// <returns><c>true</c> if the coroutine was stopped; otherwise, <c>false</c>.</returns>
	public bool StopRoutine(CoroutineHandle handle) => CoroutineManager.Stop(handle);

	/// <summary>
	/// Checks whether the specified coroutine is still running.
	/// </summary>
	/// <param name="handle">The coroutine handle to check.</param>
	/// <returns><c>true</c> if the coroutine is running; otherwise, <c>false</c>.</returns>
	public bool HasRoutine(CoroutineHandle handle) => CoroutineManager.IsRunning(handle);

	/// <summary>
	/// Stops all coroutines that were started by this screen.
	/// Useful for cleanup when a screen is exiting or being removed.
	/// </summary>
	public void ClearRoutines() => CoroutineManager.StopAll(this);
	#endregion

	internal void SetDirtyState(DirtyState state) => _dirtyState |= state;
}
