namespace Snap.Engine;

/// <summary>
/// Represents errors that occur during the creation of a game window.
/// </summary>
/// <remarks>
/// This exception is thrown when the engine fails to initialize or create a rendering window.  
/// It can wrap an inner exception to provide additional context about the underlying failure.
/// </remarks>
public sealed class WindowCreationException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="WindowCreationException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">
	/// A descriptive message that explains the reason for the window creation failure.
	/// </param>
	public WindowCreationException(string message)
		: base(message) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="WindowCreationException"/> class with a specified error message
	/// and a reference to the inner exception that caused this exception.
	/// </summary>
	/// <param name="message">
	/// A descriptive message that explains the reason for the window creation failure.
	/// </param>
	/// <param name="inner">
	/// The exception that is the cause of the current exception, or <c>null</c> if no inner exception is specified.
	/// </param>
	public WindowCreationException(string message, Exception inner)
		: base(message, inner) { }
}

/// <summary>
/// Represents the core game instance, responsible for managing engine state, settings, input,
/// and application lifecycle.
/// </summary>
public class Game : IDisposable
{
	private const int TotalFpsQueueSamples = 16;

	private readonly Queue<float> _fpsQueue = [];
	private readonly SFImage _icon;

	private SFStyles _styles;
	private SFState _state;
	private SFContext _context;
	private SFVideoMode _videoMode;
	private bool _isDisposed, _initialized;
	private float _titleTimeout;
	private bool _canApplyChanges;

	/// <summary>
	/// Gets the singleton instance of the <see cref="Game"/>.
	/// </summary>
	public static Game Instance { get; private set; }

	/// <summary>
	/// Gets the engine settings associated with this game instance.
	/// </summary>
	/// <remarks>
	/// Provides access to configuration values such as application name, company, directories,
	/// and rendering options.
	/// </remarks>
	public EngineSettings Settings { get; }

	/// <summary>
	/// Gets a value indicating whether the game window is currently active and in focus.
	/// </summary>
	public bool IsActive { get; private set; } = true;

	/// <summary>
	/// Gets the version string of the currently executing assembly.
	/// </summary>
	public string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();

	/// <summary>
	/// Gets a hashed representation of the current <see cref="Version"/>.
	/// </summary>
	public string VersionHash => $"{HashHelpers.Cache64(Version):X8}";

	/// <summary>
	/// Gets the application version as a formatted string.
	/// </summary>
	public string AppVersion => Settings.AppVersion.ToString();

	/// <summary>
	/// Gets a stable 8-character hexadecimal hash of the application version.
	/// </summary>
	public string AppVersionHash => $"{HashHelpers.Cache64(AppVersion):X8}";

	/// <summary>
	/// Gets the input map for the game.
	/// </summary>
	public InputMap Input { get; private set; }

	/// <summary>
	/// Gets the root application data folder for the game.
	/// </summary>
	public string ApplicationFolder => FileHelpers.GetApplicationData(Settings.AppCompany, Settings.AppName);

	/// <summary>
	/// Gets the absolute path to the application's content root folder.
	/// </summary>
	/// <returns>
	/// A string representing the full path to the content root folder.
	/// </returns>
	public string ContentRootFolder => Path.Combine(AppContext.BaseDirectory, Settings.AppContentRoot);

	/// <summary>
	/// Gets the folder path where application logs are stored.
	/// </summary>
	public string ApplicationLogFolder => Path.Combine(ApplicationFolder, Settings.LogDirectory);

	/// <summary>
	/// Gets the folder path where application save data is stored.
	/// </summary>
	public string ApplicationSaveFolder => Path.Combine(ApplicationFolder, Settings.SaveDirectory);

	/// <summary>
	/// Applies a change to the fullscreen mode setting.  
	/// If the value is identical to the current setting, no changes are marked for application.
	/// </summary>
	/// <param name="value">True to enable fullscreen mode; false to use windowed mode.</param>
	public void ApplyFullScreenChange(bool value)
	{
		if (Settings.FullScreen == value)
		{
			_canApplyChanges = false;
			return;
		}

		Settings.FullScreen = value;
		_canApplyChanges = true;
	}

	/// <summary>
	/// Applies a change to the window size configuration.  
	/// If the provided size matches the current configuration, or is invalid, no change is applied.
	/// </summary>
	/// <param name="width">The desired window width in pixels. Must be greater than zero.</param>
	/// <param name="height">The desired window height in pixels. Must be greater than zero.</param>
	public void ApplyWindowSizeChange(uint width, uint height)
	{
		if (width <= 0)
		{
			_canApplyChanges = false;
			return;
		}
		if (height <= 0)
		{
			_canApplyChanges = false;
			return;
		}
		if (Settings.Window.X == width && Settings.Window.Y == height)
		{
			_canApplyChanges = false;
			return;
		}

		Settings.Window = new Vect2(width, height);
		_canApplyChanges = true;
	}

	/// <summary>
	/// Applies a change to the vertical synchronization (VSync) setting.  
	/// If the new value is the same as the current one, no action is taken.
	/// </summary>
	/// <param name="value">True to enable VSync; false to disable it.</param>
	public void ApplyVSyncChange(bool value)
	{
		if (Settings.VSync == value)
		{
			_canApplyChanges = false;
			return;
		}

		Settings.VSync = value;
		_canApplyChanges = true;
	}

	/// <summary>
	/// Applies a change to the antialiasing level.  
	/// If the provided value matches the existing configuration, no change is queued.
	/// </summary>
	/// <param name="value">The desired antialiasing level (samples per pixel). Must be a non-negative integer.</param>
	public void ApplyAntialiasingChange(uint value)
	{
		if (Settings.Antialiasing == value)
		{
			_canApplyChanges = false;
			return;
		}

		Settings.Antialiasing = (int)value;
		_canApplyChanges = true;
	}

	/// <summary>
	/// Commits any pending video, window, or rendering context changes.  
	/// Recreates the underlying render window if necessary.
	/// </summary>
	/// <exception cref="WindowCreationException">
	/// Thrown when the render window fails to initialize or the system does not support the required OpenGL version.
	/// </exception>
	/// <remarks>
	/// This method will:
	/// <list type="bullet">
	/// <item>Dispose of the existing window if it is invalid.</item>
	/// <item>Recreate the SFML render window with updated settings and context.</item>
	/// <item>Reattach input handlers and event listeners.</item>
	/// <item>Center the window if not in fullscreen mode.</item>
	/// </list>
	/// </remarks>
	public void ApplyChanges()
	{
		if (!_canApplyChanges)
			return;

		// Clean up existing window if it exists and is valid
		if (ToRenderer != null && !ToRenderer.IsInvalid && ToRenderer.IsOpen)
		{
			Input.Unload();

			ToRenderer.Closed -= OnWindowClose;
			ToRenderer.GainedFocus -= OnGainedFocus;
			ToRenderer.LostFocus -= OnLostFocus;
			ToRenderer.Resized -= OnWindowResized;

			ToRenderer.Close();
			ToRenderer.Dispose();
			ToRenderer = null; // Important to set to null after disposal
		}

		// _videoMode = new SFVideoMode((uint)Settings.Window.X, (uint)Settings.Window.Y);
		_state = Settings.FullScreen ? SFState.Fullscreen : SFState.Windowed;
		_videoMode = new SFVideoMode(new((uint)Settings.Window.X, (uint)Settings.Window.Y));
		_context = new SFContext { MinorVersion = 3, MajorVersion = 3, AntialiasingLevel = (uint)Settings.Antialiasing };
		_styles = Settings.WindowResize
			? SFStyles.Titlebar | SFStyles.Resize | SFStyles.Close
			: SFStyles.Titlebar | SFStyles.Resize | SFStyles.Close // This fixes until new bug fix.
																   // : SFStyles.Titlebar | SFStyles.Close
			;

		// if (Settings.FullScreen)
		// 	_styles |= SFStyles.Fullscreen;

		try
		{
			ToRenderer = new SFRenderWindow(_videoMode, Settings.AppTitle, _styles, _state, _context);

			if (ToRenderer.IsInvalid || !ToRenderer.IsOpen)
			{
				throw new WindowCreationException(
					"Failed to create SNAP window. Make sure your GPU supports OpenGl 3.3 or greater."
				);
			}

			_log.Log(LogLevel.Info, "Window successfully created.");

			// ToRenderer.SetIcon(_icon.Size.X, _icon.Size.Y, _icon.Pixels);
			ToRenderer.SetIcon(new(_icon.Size.X, _icon.Size.Y), _icon.Pixels);
			ToRenderer.SetVerticalSyncEnabled(Settings.VSync);
			ToRenderer.SetMouseCursorVisible(Settings.Mouse);
			ToRenderer.Closed += OnWindowClose;
			ToRenderer.GainedFocus += OnGainedFocus;
			ToRenderer.LostFocus += OnLostFocus;
			ToRenderer.Resized += OnWindowResized;

			if (!Settings.FullScreen)
			{
				// ToRenderer.Position = new SFVectI(
				// 	(int)(CurrentMonitor.Width - ToRenderer.Size.X) / 2,
				// 	(int)(CurrentMonitor.Height - ToRenderer.Size.Y) / 2
				// );
			}

			Input.Load();
		}
		catch (WindowCreationException wex)
		{
			_log.Log(LogLevel.Error, wex.Message);
			_log.LogException(wex);
			throw; // re-throw so upstream knows we’re fatally broken
		}
		catch (Exception ex)
		{
			// any other unexpected issue
			_log.Log(LogLevel.Error, "Unexpected error during window creation.");
			_log.LogException(ex);

			throw new WindowCreationException("Unexpected error while creating SNAP window.", ex);
		}

		_canApplyChanges = false;
	}

	private void OnWindowResized(object sender, SFSizeEventArgs e)
		=> Settings.Window = new Vect2(e.Size.X, e.Size.Y);
	private void OnLostFocus(object sender, EventArgs e)
		=> IsActive = false;
	private void OnGainedFocus(object sender, EventArgs e)
		=> IsActive = true;

	private void OnWindowClose(object sender, EventArgs e)
	{
		if (!ToRenderer.IsOpen)
			return;

		ToRenderer.Close();
	}

	// Systems:
	private readonly Logger _log;
	private readonly Clock _clock;
	private readonly BeaconManager _beacon;
	private readonly AssetManager _assets;
	private readonly FastRandom _rand;
	private readonly Renderer _renderer;
	private readonly SoundManager _soundManager;
	private readonly ScreenManager _screenManager;
	private readonly DebugRenderer _debugRenderer;
	private readonly ServiceManager _serviceManager;
	private readonly CoroutineManager _coroutineManager;
	private readonly TextureAtlasManager _textureAtlasManager;

	/// <summary>
	/// Initializes a new instance of the <see cref="Game"/> class using the specified engine settings.  
	/// This constructor performs full system initialization, including window creation, logging,
	/// context setup, and all core service managers required for the SNAP engine.
	/// </summary>
	/// <param name="settings">
	/// The <see cref="EngineSettings"/> object containing all configuration values required
	/// to bootstrap the engine (window parameters, graphics options, logging, input, etc.).
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="settings"/> is null.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown if <paramref name="settings"/> has not been initialized via <c>EngineSettingsBuilder.Build()</c>.
	/// </exception>
	/// <exception cref="WindowCreationException">
	/// Thrown when the render window fails to be created or an unsupported OpenGL version is detected.
	/// </exception>
	/// <remarks>
	/// This constructor:
	/// <list type="bullet">
	/// <item>Validates and stores engine settings.</item>
	/// <item>Creates required application folders for logs and saves.</item>
	/// <item>Initializes logging and outputs startup diagnostics.</item>
	/// <item>Creates and centers the SFML render window using the provided context and style flags.</item>
	/// <item>Registers window events and global exception handlers.</item>
	/// <item>Initializes all SNAP core subsystems — including input, clock, asset, sound, renderer,
	/// coroutine, and texture atlas managers.</item>
	/// </list>
	/// By the end of construction, the engine runtime environment is fully operational and ready to enter
	/// the main loop or game execution phase.
	/// </remarks>
	public Game(EngineSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);

		if (!settings.Initialized)
		{
			throw new InvalidOperationException(
				"Cannot create Engine: EngineSettings must be initialized. " +
				"Make sure you call EngineSettings.Build() before passing it in."
			);
		}

		_icon = new SFImage(EmbeddedResources.GetAppIcon());

		Instance ??= this;
		Settings = settings;

		// Before setting any folders for log, etc. Make suer they exist:
		CreateFolder(ApplicationFolder, "Application data root");
		CreateFolder(ApplicationLogFolder, "Application log folder");
		CreateFolder(ApplicationSaveFolder, "Application save folder");

		_log = new Logger(Settings.LogLevel, Settings.LogMaxRecentEntries);
		_log.AddSink(new FileLogSink(ApplicationLogFolder, Settings.LogFileSizeCap, Settings.LogMaxRecentEntries));

		_log.Log(LogLevel.Info, "────────────────────────────────────────────────────────────");
		_log.Log(LogLevel.Info, "           ███████═╗ ███══╗ ██═╗  █████══╗ ██████══╗");
		_log.Log(LogLevel.Info, "           ██ ╔════╝ ████ ╚╗██ ║ ██ ╔═██ ║ ██ ╔═██ ║");
		_log.Log(LogLevel.Info, "           ███████═╗ ██ ██ ╚██ ║ ███████ ║ ██████ ╔╝");
		_log.Log(LogLevel.Info, "            ╚═══██ ║ ██ ║██ ██ ║ ██ ╔═██ ║ ██ ╔═══╝");
		_log.Log(LogLevel.Info, "           ███████ ║ ██ ║ ████ ║ ██ ║ ██ ║ ██ ║");
		_log.Log(LogLevel.Info, "            ╚══════╝ ╚══╝  ╚═══╝ ╚══╝ ╚══╝ ╚══╝");
		_log.Log(LogLevel.Info, "────────────────────────────────────────────────────────────");
		_log.Log(LogLevel.Info, $"         Version: {Version}, Hash: {VersionHash}");
		_log.Log(LogLevel.Info, "────────────────────────────────────────────────────────────");

		// _styles = Settings.WindowResize
		// 	? SFStyles.Titlebar | SFStyles.Resize | SFStyles.Close
		// 	: SFStyles.Titlebar | SFStyles.Close;
		_styles = Settings.WindowResize
			? SFStyles.Titlebar | SFStyles.Resize | SFStyles.Close
			: SFStyles.Titlebar | SFStyles.Resize | SFStyles.Close // This fixes until new bug fix.
																   // : SFStyles.Titlebar | SFStyles.Close
			;

		// if (Settings.FullScreen)
		// 	_styles |= SFStyles.Fullscreen;

		_log.Log(LogLevel.Info, $"Initializing video mode: {Settings.Window.X}x{Settings.Window.Y}");
		// _videoMode = new SFVideoMode((uint)Settings.Window.X, (uint)Settings.Window.Y);
		_videoMode = new SFVideoMode(new((uint)Settings.Window.X, (uint)Settings.Window.Y));
		_state = Settings.FullScreen ? SFState.Fullscreen : SFState.Windowed;
		_context = new SFContext { MajorVersion = 4, MinorVersion = 0, AntialiasingLevel = (uint)Settings.Antialiasing };
		_log.Log(LogLevel.Info, $"Creating OpenGL context: Version {_context.MajorVersion}.{_context.MinorVersion}, Antialiasing: {_context.AntialiasingLevel}");

		try
		{
			ToRenderer = new SFRenderWindow(_videoMode, Settings.AppTitle, _styles, _state, _context);

			if (ToRenderer.IsInvalid || !ToRenderer.IsOpen)
			{
				throw new WindowCreationException(
					"Failed to create SNAP window. Make sure your GPU supports OpenGl 3.3 or greater."
				);
			}

			_log.Log(LogLevel.Info, "Window successfully created.");

			// ToRenderer.SetIcon(_icon.Size.X, _icon.Size.Y, _icon.Pixels);
			ToRenderer.SetIcon(new(_icon.Size.X, _icon.Size.Y), _icon.Pixels);

			if (!Settings.FullScreen)
			{
				// ToRenderer.Position = new SFVectI(
				// 	(int)(CurrentMonitor.Width - ToRenderer.Size.X) / 2,
				// 	(int)(CurrentMonitor.Height - ToRenderer.Size.Y) / 2
				// );
			}
		}
		catch (WindowCreationException wex)
		{
			_log.Log(LogLevel.Error, wex.Message);
			_log.LogException(wex);
			throw; // re-throw so upstream knows we’re fatally broken
		}
		catch (Exception ex)
		{
			// any other unexpected issue
			_log.Log(LogLevel.Error, "Unexpected error during window creation.");
			_log.LogException(ex);

			throw new WindowCreationException("Unexpected error while creating SNAP window.", ex);
		}

		ToRenderer.Closed += OnWindowClose;
		ToRenderer.GainedFocus += OnGainedFocus;
		ToRenderer.LostFocus += OnLostFocus;
		ToRenderer.Resized += OnWindowResized;

		// Happens only when app crashes, make sure to report:
		AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
		{
			settings.OnCrash?.Invoke(sender, args);

			if (args.ExceptionObject is Exception ex)
				_log.LogException(ex);

			_log.Log(LogLevel.Warning, "SNAP force Stopped\n");
		};

		// Only triggers if app doesnt crash:
		AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
		{
			settings.OnShutdown?.Invoke();

			_log.Log(LogLevel.Info, "SNAP Stopped\n");
		};

		_log.Log(LogLevel.Info, $"Vsync been set to: {settings.VSync}.");
		ToRenderer.SetVerticalSyncEnabled(settings.VSync);

		_log.Log(LogLevel.Info, $"Mouse visbility been set to: {settings.Mouse}.");
		ToRenderer.SetMouseCursorVisible(settings.Mouse);

		_log.Log(LogLevel.Info, "Initializing SNAP core services...");

		_log.Log(LogLevel.Info, "Initializing input mappings...");
		Input = settings.InputMap;

		_log.Log(LogLevel.Info, "Initializing Clock...");
		_clock = new Clock();

		_log.Log(LogLevel.Info, "Initializing Beacon Manager...");
		_beacon = new BeaconManager();

		_log.Log(LogLevel.Info, "Initializing Asset Manager...");
		_assets = new AssetManager();

		_log.Log(LogLevel.Info, "Initializing FastRandom...");
		_rand = new FastRandom();

		_log.Log(LogLevel.Info, $"Initializing Renderer Manager with {Settings.DrawCallCache} cached draw calls.");
		_renderer = new Renderer(Settings.DrawCallCache);

		_log.Log(LogLevel.Info, "Initializing Sound Manager...");
		_soundManager = new SoundManager();

		_log.Log(LogLevel.Info, "Initializing Debug Renderer Manager...");
		_debugRenderer = new DebugRenderer();

		_log.Log(LogLevel.Info, "Initializing Screen Manager...");
		_screenManager = new ScreenManager();

		_log.Log(LogLevel.Info, "Initializing Service Manager...");
		_serviceManager = new ServiceManager();

		_log.Log(LogLevel.Info, "Initializing Coroutine Manager...");
		_coroutineManager = new CoroutineManager();

		_log.Log(LogLevel.Info, $"Initializing Texture Atlas manager. Page size: {Settings.AtlasPageSize} with max {Settings.MaxAtlasPages} pages");
		_textureAtlasManager = new TextureAtlasManager(Settings.AtlasPageSize, Settings.MaxAtlasPages);
	}

	/// <summary>
	/// Finalizer for the <see cref="Game"/> class.  
	/// Ensures that unmanaged resources are released if <see cref="Dispose(bool)"/> was not called explicitly.
	/// </summary>
	/// <remarks>
	/// Invokes <see cref="Dispose(bool)"/> with <c>false</c> to perform cleanup during garbage collection.  
	/// This should only run if the game instance was not disposed manually.
	/// </remarks>
	~Game() => Dispose(disposing: false);

	/// <summary>
	/// Closes the active game window and initiates application shutdown.  
	/// </summary>
	/// <remarks>
	/// Safely terminates the current <see cref="ToRenderer"/> instance if it is open.  
	/// If the window is already closed or invalid, the call is ignored.  
	/// This method does not immediately dispose engine systems; it simply signals the
	/// end of the active rendering session.
	/// </remarks>
	public void Quit()
	{
		if (ToRenderer?.IsOpen != true)
			return;

		ToRenderer.Close();
	}


	private void CreateFolder(string path, string description)
	{
		try
		{
			Directory.CreateDirectory(path);
		}
		catch (Exception ex)
		{
			_log.LogException(ex);
			throw new IOException($"Unable to create {description} at '{path}'", ex);
		}
	}

	/// <summary>
	/// Starts the main game loop for the SNAP engine.  
	/// Initializes core systems if not already initialized, loads services and screens,
	/// and begins processing events, updates, and rendering until the window is closed.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown if the render window is invalid and cannot be used to start the engine.
	/// </exception>
	/// <remarks>
	/// This method:
	/// <list type="bullet">
	/// <item>Validates the window before starting the loop.</item>
	/// <item>Loads the input map and initializes asset, screen, and service systems if needed.</item>
	/// <item>Executes the main loop, dispatching window events and updating the clock and coroutine managers each frame.</item>
	/// <item>Clears, updates, and redraws all active screens until the window is closed.</item>
	/// </list>
	/// The loop continues until the user or system triggers a window close event.
	/// </remarks>
	public void Run()
	{
		if (ToRenderer.IsInvalid)
			throw new InvalidOperationException("Window is invalid. Cannot start engine.");

		_log.Log(LogLevel.Info, "Loading InputMap...");
		Input.Load();

		// init
		if (!_initialized)
		{
			_initialized = true;

			if (Settings.Services?.Length > 0)
			{
				_log.Log(LogLevel.Info, $"Adding {Settings.Services.Length} service{(Settings.Services.Length > 1 ? "s" : string.Empty)}.");
				for (int i = 0; i < Settings.Services.Length; i++)
				{
					var tService = Settings.Services[i];

					if (!InstanceHelpers.TryCreateInstanceFromType<Service>(tService, [], out var service))
						continue;

					_serviceManager.RegisterService(service);
				}
			}

			if (Settings.Screens?.Length > 0)
			{
				var sResult = new List<Screen>(Settings.Screens.Length);
				for (int i = 0; i < Settings.Screens.Length; i++)
				{
					var tScreen = Settings.Screens[i];

					if (!InstanceHelpers.TryCreateInstanceFromType<Screen>(tScreen, [], out var screen))
						continue;

					sResult.Add(screen);
				}

				_log.Log(LogLevel.Info, $"Adding {sResult.Count} screen{(sResult.Count > 1 ? "s" : string.Empty)}.");
				_screenManager.Add([.. sResult]);
			}

			Settings.OnStartup?.Invoke();
		}

		while (ToRenderer.IsOpen)
		{
			ToRenderer.DispatchEvents();
			_clock.Update();
			_coroutineManager.Update();

			UpdateTitle();

			ToRenderer.Clear(Settings.ClearColor);
			_screenManager.Update();
			ToRenderer.Display();
		}
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="Game"/> instance  
	/// and optionally disposes of managed resources.
	/// </summary>
	/// <param name="disposing">
	/// True to release both managed and unmanaged resources;  
	/// false to release only unmanaged resources during finalization.
	/// </param>
	/// <remarks>
	/// This method is invoked by <see cref="Dispose()"/> when disposal is explicit,  
	/// or by the finalizer (~<see cref="Game"/>) when called by the garbage collector.  
	/// It clears all core managers and disposes of the active render window to free GPU and memory resources.
	/// </remarks>
	protected virtual void Dispose(bool disposing)
	{
		if (!_isDisposed)
		{
			_assets.Clear();
			_soundManager.Clear();
			_screenManager.Clear();
			ToRenderer?.Dispose();

			_isDisposed = true;
		}
	}

	/// <summary>
	/// Performs application-defined tasks associated with freeing, releasing,  
	/// or resetting unmanaged resources used by the <see cref="Game"/> instance.
	/// </summary>
	/// <remarks>
	/// Calls <see cref="Dispose(bool)"/> with <c>true</c> and suppresses finalization  
	/// to prevent redundant cleanup by the garbage collector.
	/// </remarks>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	private void UpdateTitle()
	{
		if (_fpsQueue.Count >= TotalFpsQueueSamples)
			_fpsQueue.Dequeue();

		_fpsQueue.Enqueue(1f / _clock.DeltaTime);

		if (_titleTimeout >= 0.000001f)
		{
			_titleTimeout -= _clock.DeltaTime;
		}
		else
		{
			var sb = new StringBuilder(1024 * 2);
			var tEntity = _screenManager.Screens.Sum(x => x.Entities.Count);
			var aEntity = _screenManager.Screens.Sum(x => x.ActiveEntities.Count);

			sb.Append($"{Settings.AppTitle} | ");

			// Fps: Fps (AvgFps)
			sb.Append($"Fps: {1f / _clock.DeltaTime:0} ({_fpsQueue.Average():0} avg) | ");

			// Entity: ActiveEntity/total Entities
			sb.Append($"Entity: {aEntity}/{tEntity} | ");

			// Screen: <number> <active screen>
			sb.Append($"Screens: {_screenManager.Count}, Active: {(_screenManager.Count > 0 ? _screenManager.Screens[^1].GetType().Name : "None")} |  ");

			// Rendering: Draws, Batches
			sb.Append($"Batch: Draws: {_renderer.DrawCalls}, Batches: {_renderer.Batches} | ");

			// Atlas Manager: 1/8, <percent of ratio used>
			sb.Append($"Atlas: {TextureAtlasManager.Instance.Pages}/{TextureAtlasManager.Instance.MaxPages} Pages, {TextureAtlasManager.Instance.TotalFillRatio * 100f:0}% Filled | ");

			// Coroutines: <number>
			sb.Append($"Routines: {CoroutineManager.Instance.Count} | ");

			// Beacon (PubSub): <number>
			sb.Append($"Beacon: {BeaconManager.Instance.Count} | ");

			// Sounds:
			sb.Append($"Sound: Playing: {_soundManager.PlayCount}, Banks: {_soundManager.Count}, Pool: {SoundInstancePool.AvailbleInstances}/{SoundInstancePool.ActiveInstances}");

			ToRenderer.SetTitle(sb.ToString());

			_titleTimeout += 1.0f;
		}
	}



	/// <summary>
	/// Gets the primary (desktop) monitor's resolution.
	/// </summary>
	/// <remarks>
	/// To retrieve the current monitor dimensions.
	/// </remarks>
	public Monitor CurrentMonitor
	{
		get
		{
			var m = SFVideoMode.DesktopMode;

			// return new Monitor((int)m.Width, (int)m.Height);
			return new Monitor((int)m.Size.X, (int)m.Size.Y);
		}
	}

	/// <summary>
	/// Retrieves a list of supported monitor resolutions that match a specified aspect ratio.
	/// </summary>
	/// <param name="wRatio">
	/// The width portion of the target aspect ratio (e.g., 16 for a 16:9 ratio).
	/// </param>
	/// <param name="hRatio">
	/// The height portion of the target aspect ratio (e.g., 9 for a 16:9 ratio).
	/// </param>
	/// <returns>
	/// A list of <see cref="Monitor"/> objects representing supported resolutions matching the given ratio.
	/// </returns>
	/// <remarks>
	/// This method checks and filters out modes whose aspect ratios do not match the target within the given tolerance.
	/// </remarks>
	public List<Monitor> GetSupportedMonitors(uint wRatio, uint hRatio)
	{
		const float tolerance = 0.01f;

		float ratio = (float)wRatio / hRatio;
		SFVideoMode[] modes = SFVideoMode.FullscreenModes;
		var resolutionMap = new Dictionary<string, SFVideoMode>(modes.Length);

		foreach (var mode in modes)
		{
			float actualRatio = (float)mode.Size.X / mode.Size.Y;

			if (Math.Abs(actualRatio - ratio) >= tolerance)
				continue;

			var key = $"{mode.Size.X}x{mode.Size.Y}";

			if (!resolutionMap.TryGetValue(key, out var exists) ||
			mode.BitsPerPixel > exists.BitsPerPixel)
			{
				resolutionMap[key] = mode;
			}
		}

		return [.. resolutionMap.Values
			.Select(mode => new Monitor((int)mode.Size.X, (int)mode.Size.Y))
		];
	}

	internal SFRenderWindow ToRenderer { get; private set; }
}
