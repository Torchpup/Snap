using Snap.Engine.Assets;

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
/// <remarks>
/// <see cref="Game"/> is the central entry point of the engine.  
/// It enforces a singleton instance via <see cref="Game.Instance"/> and provides access to
/// engine settings, input mapping, version information, and application directories.  
/// 
/// Because <see cref="Game"/> implements <see cref="IDisposable"/>, it must be properly disposed
/// to release unmanaged resources, close windows, and flush pending operations.  
/// Typical usage involves creating the game instance, running the main loop, and disposing
/// it at shutdown.
/// </remarks>
public class Game : IDisposable
{
	private const int TotalFpsQueueSamples = 16;
	private SFStyles _styles;
	private SFContext _context;
	SFVideoMode _videoMode;
	private bool _isDisposed, _initialized;
	private readonly Queue<float> _fpsQueue = [];
	private float _titleTimeout;
	private readonly SFImage _icon;
	private bool _canApplyChanges;

	/// <summary>
	/// Gets the singleton instance of the <see cref="Game"/>.
	/// </summary>
	/// <remarks>
	/// The engine enforces a single global <see cref="Game"/> instance to manage runtime state,
	/// settings, input, and application folders.
	/// </remarks>
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
	/// Gets a value indicating whether the game is currently active.
	/// </summary>
	/// <remarks>
	/// Defaults to <c>true</c>. This property can be toggled internally by the engine
	/// to reflect focus or suspension state.
	/// </remarks>
	public bool IsActive { get; private set; } = true;

	/// <summary>
	/// Gets the version string of the currently executing assembly.
	/// </summary>
	/// <remarks>
	/// This property retrieves the version metadata from the assembly manifest.
	/// </remarks>
	public string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();

	/// <summary>
	/// Gets a hashed representation of the current <see cref="Version"/>.
	/// </summary>
	/// <remarks>
	/// Uses <see cref="HashHelpers.Hash64(string)"/> to generate a 64-bit hash of the version string,
	/// formatted as an 8-character hexadecimal value.
	/// </remarks>
	public string VersionHash => $"{HashHelpers.Hash64(Version):X8}";

	/// <summary>
	/// Gets the input map for the game.
	/// </summary>
	/// <remarks>
	/// Provides access to input bindings and state, allowing the engine to query
	/// keyboard, mouse, and controller inputs.
	/// </remarks>
	public InputMap Input { get; private set; }

	/// <summary>
	/// Gets the root application data folder for the game.
	/// </summary>
	/// <remarks>
	/// The folder path is resolved using <see cref="FileHelpers.GetApplicationData(string,string)"/> 
	/// with the company and application name from <see cref="Settings"/>.
	/// </remarks>
	public string ApplicationFolder => FileHelpers.GetApplicationData(Settings.AppCompany, Settings.AppName);

	/// <summary>
	/// Gets the folder path where application logs are stored.
	/// </summary>
	/// <remarks>
	/// Combines <see cref="ApplicationFolder"/> with <see cref="EngineSettings.LogDirectory"/>.
	/// </remarks>
	public string ApplicationLogFolder => Path.Combine(ApplicationFolder, Settings.LogDirectory);

	/// <summary>
	/// Gets the folder path where application save data is stored.
	/// </summary>
	/// <remarks>
	/// Combines <see cref="ApplicationFolder"/> with <see cref="EngineSettings.SaveDirectory"/>.
	/// </remarks>
	public string ApplicationSaveFolder => Path.Combine(ApplicationFolder, Settings.SaveDirectory);

	/// <summary>
	/// Applies a change to the fullscreen mode setting.  
	/// If the value is identical to the current setting, no changes are marked for application.
	/// </summary>
	/// <param name="value">True to enable fullscreen mode; false to use windowed mode.</param>
	/// <remarks>
	/// Sets <see cref="_canApplyChanges"/> to true only if the fullscreen state differs from the existing setting.
	/// </remarks>
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
	/// <remarks>
	/// Updates <see cref="EngineSettings.Window"/> and flags <see cref="_canApplyChanges"/> if a valid size change is detected.
	/// </remarks>
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
	/// <remarks>
	/// Sets <see cref="_canApplyChanges"/> to true only when the setting differs from the current configuration.
	/// </remarks>
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
	/// <remarks>
	/// Converts the value to an integer and marks <see cref="_canApplyChanges"/> only if the level differs.
	/// </remarks>
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
	/// After successful execution, <see cref="_canApplyChanges"/> is reset to false.
	/// </remarks>
	public void ApplyChanges()
	{
		if (!_canApplyChanges)
			return;

		if (ToRenderer?.IsInvalid == true)
		{
			Input.Unload();

			ToRenderer.Closed -= OnWindowClose;
			ToRenderer.GainedFocus -= OnGainedFocus;
			ToRenderer.LostFocus -= OnLostFocus;

			ToRenderer.Close();
			ToRenderer.Dispose();
		}

		_videoMode = new SFVideoMode((uint)Settings.Window.X, (uint)Settings.Window.Y);
		_context = new SFContext { MinorVersion = 3, MajorVersion = 3, AntialiasingLevel = (uint)Settings.Antialiasing };

		_styles = Settings.WindowResize
			? SFStyles.Titlebar | SFStyles.Resize | SFStyles.Close
			: SFStyles.Titlebar | SFStyles.Close;

		if (Settings.FullScreen)
			_styles |= SFStyles.Fullscreen;

		try
		{
			ToRenderer = new SFRenderWindow(_videoMode, Settings.AppTitle, _styles, _context);

			if (ToRenderer.IsInvalid || !ToRenderer.IsOpen)
			{
				throw new WindowCreationException(
					"Failed to create SNAP window. Make sure your GPU supports OpenGl 3.3 or greater."
				);
			}

			_log.Log(LogLevel.Info, "Window successfully created.");

			ToRenderer.SetIcon(_icon.Size.X, _icon.Size.Y, _icon.Pixels);
			ToRenderer.SetVerticalSyncEnabled(Settings.VSync);
			ToRenderer.SetMouseCursorVisible(Settings.Mouse);
			ToRenderer.Closed += OnWindowClose;
			ToRenderer.GainedFocus += OnGainedFocus;
			ToRenderer.LostFocus += OnLostFocus;

			if (!Settings.FullScreen)
			{
				ToRenderer.Position = new SFVectI(
					(int)(CurrentMonitor.Width - ToRenderer.Size.X) / 2,
					(int)(CurrentMonitor.Height - ToRenderer.Size.Y) / 2
				);
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

	private void OnLostFocus(object sender, EventArgs e) => IsActive = false;
	private void OnGainedFocus(object sender, EventArgs e) => IsActive = true;

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

		_styles = Settings.WindowResize
			? SFStyles.Titlebar | SFStyles.Resize | SFStyles.Close
			: SFStyles.Titlebar | SFStyles.Close;

		if (Settings.FullScreen)
			_styles |= SFStyles.Fullscreen;

		_log.Log(LogLevel.Info, $"Initializing video mode: {Settings.Window.X}x{Settings.Window.Y}");
		_videoMode = new SFVideoMode((uint)Settings.Window.X, (uint)Settings.Window.Y);

		_context = new SFContext { MajorVersion = 4, MinorVersion = 0, AntialiasingLevel = (uint)Settings.Antialiasing };
		_log.Log(LogLevel.Info, $"Creating OpenGL context: Version {_context.MajorVersion}.{_context.MinorVersion}, Antialiasing: {_context.AntialiasingLevel}");

		try
		{
			ToRenderer = new SFRenderWindow(_videoMode, Settings.AppTitle, _styles, _context);

			if (ToRenderer.IsInvalid || !ToRenderer.IsOpen)
			{
				throw new WindowCreationException(
					"Failed to create SNAP window. Make sure your GPU supports OpenGl 3.3 or greater."
				);
			}

			_log.Log(LogLevel.Info, "Window successfully created.");

			ToRenderer.SetIcon(_icon.Size.X, _icon.Size.Y, _icon.Pixels);

			if (!Settings.FullScreen)
			{
				ToRenderer.Position = new SFVectI(
					(int)(CurrentMonitor.Width - ToRenderer.Size.X) / 2,
					(int)(CurrentMonitor.Height - ToRenderer.Size.Y) / 2
				);
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

		ToRenderer.Closed += (_, _) => ToRenderer.Close();
		ToRenderer.GainedFocus += (_, _) => IsActive = true;
		ToRenderer.LostFocus += (_, _) => IsActive = false;

		// Happens only when app crashes, make sure to report:
		AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
		{
			if (args.ExceptionObject is Exception ex)
				_log.LogException(ex);

			_log.Log(LogLevel.Warning, "SNAP force Stopped\n");
		};

		// Only triggers if app doesnt crash:
		AppDomain.CurrentDomain.ProcessExit += (sender, args) => _log.Log(LogLevel.Info, "SNAP Stopped\n");

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

			// Setup defualt Asset Manager Provider
			AssetBootstrap.InitDefault();

			if (Settings.Services?.Length > 0)
			{
				_log.Log(LogLevel.Info, $"Adding {Settings.Services.Length} service{(Settings.Services.Length > 1 ? "s" : string.Empty)}.");
				for (int i = 0; i < Settings.Services.Length; i++)
					_serviceManager.RegisterService(Settings.Services[i]);
			}

			if (Settings.Screens?.Length > 0)
			{
				_log.Log(LogLevel.Info, $"Adding {Settings.Screens.Length} screen{(Settings.Screens.Length > 1 ? "s" : string.Empty)}.");
				_screenManager.Add(Settings.Screens);
			}
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
			var sb = new StringBuilder(1024);
			var tEntity = _screenManager.Screens.Sum(x => x.Entities.Count);
			var aEntity = _screenManager.Screens.Sum(x => x.ActiveEntities.Count);

			static double BytesToMib(long bytes) => bytes / 1024.0 / 1024.0;

			sb.Append($"{Settings.AppTitle} | ");

			// Fps: Fps (AvgFps)
			sb.Append($"Fps: {1f / _clock.DeltaTime:0} ({_fpsQueue.Average():0} avg) | ");

			// Entity: ActiveEntity/total Entities
			sb.Append($"Entity: {aEntity}/{tEntity} | ");

			// Assets: Bytes, Active/Total
			sb.Append($"Assets: {BytesToMib(_assets.BytesLoaded):0.00}MB, {_assets.Count}/{_assets.TotalCount} Assets Loaded | ");

			// Rendering: Draws, Batches
			sb.Append($"Batch: Draws: {_renderer.DrawCalls}, Batches: {_renderer.Batches} | ");

			// Atlas Manager: 1/8, <percent of ratio used>
			sb.Append($"Atlas: {TextureAtlasManager.Instance.Pages}/{TextureAtlasManager.Instance.MaxPages} Pages, {TextureAtlasManager.Instance.TotalFillRatio * 100f:0}% Filled | ");

			// // Coroutines: <number>
			sb.Append($"Routines: {CoroutineManager.Instance.Count} | ");

			// // Beacon (PubSub): <number>
			sb.Append($"Beacon: {BeaconManager.Instance.Count} | ");

			// // Sounds:
			sb.Append($"Sound: Playing: {_soundManager.PlayCount}, Banks: {_soundManager.Count}");

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

			return new Monitor((int)m.Width, (int)m.Height);
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
	/// <param name="tolerance">
	/// The allowed margin of error when comparing aspect ratios. Defaults to 0.01f.
	/// </param>
	/// <returns>
	/// A list of <see cref="Monitor"/> objects representing supported resolutions matching the given ratio.
	/// </returns>
	/// <remarks>
	/// This method checks and filters out modes whose aspect ratios do not match the target within the given tolerance.
	/// </remarks>
	public List<Monitor> GetSupportedMonitors(int wRatio, int hRatio, float tolerance = 0.01f)
	{
		float ratio = (float)wRatio / hRatio;

		return [.. SFVideoMode.FullscreenModes
			.Where(mode =>
			{
				float actualRatio = (float)mode.Width / mode.Height;
				return Math.Abs(actualRatio - ratio) < tolerance;
			})
			.Select(x => new Monitor((int)x.Width, (int)x.Height))];
	}

	internal SFRenderWindow ToRenderer { get; private set; }
}
