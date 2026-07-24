namespace Snap.Engine;

/// <summary>
/// Provides configuration options for the Snap engine.
/// This sealed class defines runtime settings such as save paths,
/// logging behavior, and other engine-wide parameters.
/// </summary>
/// <remarks>
/// Use <see cref="EngineSettings"/> to centralize and control
/// engine configuration. Being sealed ensures a consistent set
/// of options without inheritance, keeping behavior predictable
/// across platforms.
/// </remarks>
public sealed class EngineSettings
{
	private const uint MinimumAtlasPageSize = 512;
	private const uint MinPages = 3;
	private const uint MaxPages = 8;
	private const uint MinBatchIncrease = 1024, MaxBatchIncrease = 1024 * 4;
	private const uint MinimumDrawllCallCacheSize = 512;
	private const uint MaxLogFileSizeBytes = 50;
	private const uint BytesPerMB = 1_048_576;
	private const uint MaxLogEntries = 99;

	/// <summary>
	/// Gets the singleton instance of the engine settings.
	/// </summary>
	/// <remarks>
	/// This property is assigned when the <see cref="EngineSettings"/> constructor
	/// is first invoked. It ensures a single, globally accessible configuration
	/// object throughout the engine lifecycle.
	/// </remarks>
	public static EngineSettings Instance { get; private set; }

	/// <summary>
	/// Indicates whether the engine settings have been initialized.
	/// </summary>
	/// <remarks>
	/// This flag is set once the engine has completed its initialization process.
	/// It can be used to guard against premature access to configuration values.
	/// </remarks>
	public bool Initialized { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="EngineSettings"/> class.
	/// </summary>
	/// <remarks>
	/// The constructor assigns the static <see cref="Instance"/> property if it
	/// has not already been set, enforcing a singleton pattern for engine settings.
	/// </remarks>
	public EngineSettings() => Instance ??= this;

	/// <summary>
	/// Sets the batch increasement value for the engine.
	/// </summary>
	/// <param name="value">
	/// The desired batch increasement, which must be between
	/// <c>MinBatchIncrease</c> and <c>MaxBatchIncrease</c>.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// If the provided value falls outside the allowed range, an
	/// <see cref="ArgumentOutOfRangeException"/> is thrown.
	/// </remarks>
	public EngineSettings WithBatchIncreasment(uint value)
	{
		if (value < MinBatchIncrease || value > MaxBatchIncrease)
		{
			throw new ArgumentOutOfRangeException(nameof(value),
				$"Batch increasement must be between {MinBatchIncrease} and {MaxBatchIncrease}.");
		}

		BatchIncreasment = (int)value;
		return this;
	}
	internal int BatchIncreasment { get; private set; }

	/// <summary>
	/// Sets the half-texel offset rendering option.
	/// </summary>
	/// <param name="value">
	/// A boolean value specifying whether half-texel offset should be applied.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Enabling this option adjusts texture coordinates by half a texel to improve
	/// sampling precision. Disable it if your rendering pipeline does not require
	/// this correction.
	/// </remarks>
	public EngineSettings WithHalfTexelOffset(bool value)
	{
		HalfTexelOffset = value;

		return this;
	}
	internal bool HalfTexelOffset { get; private set; }

	/// <summary>
	/// Sets the logging level for the engine.
	/// </summary>
	/// <param name="value">
	/// The desired <see cref="LogLevel"/> to apply.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Adjusting the logging level allows you to control the amount of
	/// diagnostic information produced during runtime.
	/// </remarks>
	public EngineSettings WithLogLevel(LogLevel value)
	{
		LogLevel = value;

		return this;
	}
	internal LogLevel LogLevel { get; private set; }

	/// <summary>
	/// Sets the directory path used for saving game data.
	/// </summary>
	/// <param name="value">
	/// A non-empty string representing the directory path.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentNullException"/> if the value is null or empty,
	/// and an <see cref="ArgumentException"/> if the value contains the path separator
	/// character. This ensures cross-platform safe paths for saving game data.
	/// </remarks>
	public EngineSettings WithSaveDirectory(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentNullException(nameof(value), "Save directory path cannot be null or empty.");

		if (value.Contains(Path.PathSeparator))
			throw new ArgumentException($"Save directory path cannot contain the path separator character '{Path.PathSeparator}'.", nameof(value));

		SaveDirectory = value;

		return this;
	}
	internal string SaveDirectory { get; private set; }

	/// <summary>
	/// Sets the directory path used for storing log files.
	/// </summary>
	/// <param name="value">
	/// A non-empty string representing the directory path.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentNullException"/> if the value is null or whitespace,
	/// an <see cref="ArgumentException"/> if the value contains invalid path characters,
	/// or if it includes the path separator character. This ensures safe and valid
	/// paths for log storage across platforms.
	/// </remarks>
	public EngineSettings WithLogDirectory(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentNullException(nameof(value), "Log directory path cannot be null or whitespace.");
		if (value.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
			throw new ArgumentException("Log directory path contains invalid characters.", nameof(value));
		if (value.Contains(Path.PathSeparator))
			throw new ArgumentException($"Log directory path cannot contain the path separator character '{Path.PathSeparator}'.", nameof(value));

		LogDirectory = value;

		return this;
	}
	internal string LogDirectory { get; private set; }

	/// <summary>
	/// Sets whether mouse input should be enabled for the engine.
	/// </summary>
	/// <param name="value">
	/// A boolean value specifying whether mouse input is enabled.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Enabling this option allows the engine to capture and respond to mouse
	/// events. Disable it if mouse input is not required for your application.
	/// </remarks>
	public EngineSettings WithMouse(bool value)
	{
		Mouse = value;

		return this;
	}
	internal bool Mouse { get; private set; }


	/// <summary>
	/// Sets the debug draw mode, which determines which layers display debug entity shapes.
	/// </summary>
	/// <param name="mode">
	/// A <see cref="DebugDrawMode"/> value specifying the rendering behavior:
	/// <list type="bullet">
	/// <item><description><see cref="DebugDrawMode.All"/> - Renders debug shapes on all layers.</description></item>
	/// <item><description><see cref="DebugDrawMode.TopMost"/> - Renders debug shapes only on the topmost layer, above everything else.</description></item>
	/// <item><description><see cref="DebugDrawMode.BottomMost"/> - Renders debug shapes only on the bottommost layer, behind everything else.</description></item>
	/// <item><description><see cref="DebugDrawMode.None"/> - Disables debug shape rendering entirely.</description></item>
	/// </list>
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance, allowing for fluent method chaining.
	/// </returns>
	public EngineSettings WithDebugDraw(DebugDrawMode mode)
	{
		DebugDraw = mode;

		return this;
	}
	internal DebugDrawMode DebugDraw { get; private set; }





	/// <summary>
	/// Sets the company name associated with the application.
	/// </summary>
	/// <param name="value">
	/// A string representing the company or developer name.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// This value is stored for use in application metadata and may be
	/// referenced when creating save directories or log files.
	/// </remarks>
	public EngineSettings WithAppCompany(string value)
	{
		AppCompany = value;

		return this;
	}
	internal string AppCompany { get; private set; }

	/// <summary>
	/// Sets whether partial paths should be allowed during pathfinding.
	/// </summary>
	/// <param name="value">
	/// A boolean value specifying whether partial paths are permitted.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Enabling this option allows the pathfinding system to provide partial routes
	/// instead of failing outright when a complete path is unavailable.
	/// </remarks>
	public EngineSettings WithAllowPartialPaths(bool value)
	{
		AllowPartialPaths = value;

		return this;
	}
	internal bool AllowPartialPaths { get; private set; }

	/// <summary>
	/// Sets whether vertical synchronization (VSync) should be enabled.
	/// </summary>
	/// <param name="value">
	/// A boolean value specifying whether VSync is enabled.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Enabling VSync synchronizes frame updates with the monitor refresh cycle.
	/// Disable it if maximum rendering performance is preferred over visual stability.
	/// </remarks>
	public EngineSettings WithVSync(bool value)
	{
		VSync = value;

		return this;
	}
	internal bool VSync { get; set; }




	/// <summary>
	/// Sets whether the engine should run in full-screen mode.
	/// </summary>
	/// <param name="value">
	/// A boolean value specifying whether full-screen mode is enabled.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Enabling full-screen mode provides an immersive experience by covering the
	/// entire display. Disable it to run the application in a resizable window.
	/// </remarks>
	public EngineSettings WithFullScreen(bool value)
	{
		FullScreen = value;

		return this;
	}
	internal bool FullScreen { get; set; }

	/// <summary>
	/// Sets the antialiasing level used by the engine.
	/// </summary>
	/// <param name="value">
	/// The desired antialiasing level, expressed as a positive integer value.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Higher values improve visual quality but may reduce performance depending
	/// on hardware capabilities. Choose a level appropriate for the target platform.
	/// </remarks>
	public EngineSettings WithAntialiasing(uint value)
	{
		Antialiasing = (int)value;

		return this;
	}
	internal int Antialiasing { get; set; }

	/// <summary>
	/// Sets whether window resizing should be enabled.
	/// </summary>
	/// <param name="value">
	/// A boolean value specifying whether window resizing is enabled.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Enabling this option allows the user to adjust the application window size
	/// during runtime. Disable it to enforce a fixed window size.
	/// </remarks>
	public EngineSettings WithWindowResize(bool value)
	{
		WindowResize = value;

		return this;
	}
	internal bool WindowResize { get; private set; }

	/// <summary>
	/// Sets the size of the draw call cache used by the engine.
	/// </summary>
	/// <param name="value">
	/// The desired cache size, expressed as a positive integer value.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentOutOfRangeException"/> if the provided value
	/// is less than <c>MinimumDrawllCallCacheSize</c>. Larger cache sizes may
	/// improve performance but increase memory usage.
	/// </remarks>
	public EngineSettings WithDrawCallCache(uint value)
	{
		if (value < MinimumDrawllCallCacheSize)
		{
			throw new ArgumentOutOfRangeException(nameof(value),
				$"Draw call cache size must be at least {MinimumDrawllCallCacheSize}.");
		}

		DrawCallCache = (int)value;

		return this;
	}
	internal int DrawCallCache { get; private set; }

	/// <summary>
	/// Sets the atlas page size used by the engine.
	/// </summary>
	/// <param name="value">
	/// The desired atlas page size, expressed as a positive integer value.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentOutOfRangeException"/> if the provided value
	/// is less than <c>MinimumAtlasPageSize</c>. Larger atlas sizes allow more
	/// textures to be packed together but may increase memory usage.
	/// </remarks>
	public EngineSettings WithAtlasPageSize(uint value)
	{
		if (value < MinimumAtlasPageSize)
		{
			throw new ArgumentOutOfRangeException(nameof(value),
				$"Atlas page size must be at least {MinimumAtlasPageSize}.");
		}

		AtlasPageSize = (int)value;

		return this;
	}
	internal int AtlasPageSize { get; private set; }

	/// <summary>
	/// Sets the maximum number of atlas pages allowed by the engine.
	/// </summary>
	/// <param name="value">
	/// The desired maximum number of atlas pages.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentOutOfRangeException"/> if the provided value
	/// is less than <c>MinPages</c> or greater than <c>MaxPages</c>. This ensures
	/// that texture atlas allocation remains within valid bounds.
	/// </remarks>
	public EngineSettings WithMaxAtlasPages(uint value)
	{
		if (value < MinPages || value > MaxPages)
		{
			throw new ArgumentOutOfRangeException(nameof(value),
				$"Max atlas pages must be between {MinPages} and {MaxPages} inclusive.");
		}

		MaxAtlasPages = (int)value;

		return this;
	}
	internal int MaxAtlasPages { get; private set; }

	/// <summary>
	/// Sets the input dead zone value.
	/// </summary>
	/// <param name="value">
	/// A floating-point value between <c>0.0</c> and <c>1.0</c> inclusive.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentOutOfRangeException"/> if the provided value
	/// is outside the valid range. Use this to fine-tune input sensitivity and
	/// eliminate unwanted noise from analog devices.
	/// </remarks>
	public EngineSettings WithDeadZone(float value)
	{
		if (value < 0 || value > 1.0f)
		{
			throw new ArgumentOutOfRangeException(nameof(value),
				"Dead zone must be between 0.0 and 1.0 inclusive.");
		}

		DeadZone = value;

		return this;
	}
	internal float DeadZone { get; private set; }

	/// <summary>
	/// Sets the input map used by the engine.
	/// </summary>
	/// <param name="value">
	/// The <see cref="InputMap"/> instance containing input actions and bindings.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentNullException"/> if the provided map is null,
	/// or an <see cref="InvalidOperationException"/> if the actions collection
	/// is empty. This ensures that the engine always has a valid set of input
	/// actions to process.
	/// </remarks>
	public EngineSettings WithInputMap(InputMap value)
	{
		if (value == null)
			throw new ArgumentNullException(nameof(value), "Input map cannot be null.");
		if (value.Actions.Count == 0)
			throw new InvalidOperationException("Actions collection must contain at least one item.");

		InputMap = value;

		return this;
	}
	internal InputMap InputMap { get; private set; }

	/// <summary>
	/// Sets the application title.
	/// </summary>
	/// <param name="value">
	/// A non-empty string representing the application title.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentNullException"/> if the provided value is null
	/// or consists only of whitespace. This ensures the application always has a
	/// valid title for display and identification.
	/// </remarks>
	public EngineSettings WithAppTitle(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentNullException(nameof(value), "App title cannot be null.");

		AppTitle = value;

		return this;
	}
	internal string AppTitle { get; private set; }

	/// <summary>
	/// Sets the application name.
	/// </summary>
	/// <param name="value">
	/// A non-empty string representing the application name.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentNullException"/> if the provided value is null
	/// or consists only of whitespace. The application name is also used as the
	/// folder name for storing application data, so it must be valid for file system usage.
	/// </remarks>
	public EngineSettings WithAppName(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentNullException(nameof(value), "App name cannot be null.");

		AppName = value;

		return this;
	}
	internal string AppName { get; private set; }

	/// <summary>
	/// Sets the application content root directory.
	/// </summary>
	/// <param name="value">
	/// A string representing the path to the content root directory.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentNullException"/> if the provided value is null,
	/// empty, or whitespace. Throws a <see cref="DirectoryNotFoundException"/> if
	/// the specified directory does not exist. This ensures the engine always
	/// references a valid content root for loading assets.
	/// </remarks>
	public EngineSettings WithAppContentRoot(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentNullException(nameof(value), "App coontent root cannot be null, empty, or whitespace.");
		if (!Directory.Exists(value))
			throw new DirectoryNotFoundException($"The specified content root directory does not exist: '{value}'.");

		AppContentRoot = value;

		return this;
	}
	internal string AppContentRoot { get; private set; }

	/// <summary>
	/// Sets the window dimensions for the application.
	/// </summary>
	/// <param name="width">
	/// The desired window width, expressed as a positive integer greater than zero.
	/// </param>
	/// <param name="height">
	/// The desired window height, expressed as a positive integer greater than zero.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentOutOfRangeException"/> if either width or height
	/// is less than or equal to zero. This ensures the application window always
	/// has valid dimensions for rendering.
	/// </remarks>
	public EngineSettings WithWindow(uint width, uint height)
	{
		if (width == 0)
			throw new ArgumentOutOfRangeException(nameof(width), "Window width must be greater than zero");
		if (height == 0)
			throw new ArgumentOutOfRangeException(nameof(width), "Window height must be greater than zero");

		Window = new Vect2(width, height);

		return this;
	}
	internal Vect2 Window { get; set; }

	/// <summary>
	/// Sets the application version using major, minor, and optional build numbers.
	/// </summary>
	/// <param name="major">The major version component.</param>
	/// <param name="minor">The minor version component.</param>
	/// <param name="build">
	/// The build version component. Defaults to <c>0</c> if not specified.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance, enabling fluent method chaining.
	/// </returns>
	/// <remarks>
	/// This method is part of the fluent configuration API for <see cref="EngineSettings"/>,
	/// allowing engine settings to be configured inline before startup.
	/// </remarks>
	public EngineSettings WithAppVersion(uint major, uint minor, uint build = 0)
	{
		AppVersion = new Version((int)major, (int)minor, (int)build);

		return this;
	}
	internal Version AppVersion { get; private set; }

	/// <summary>
	/// Sets the viewport dimensions for the application.
	/// </summary>
	/// <param name="width">
	/// The desired viewport width, expressed as a positive integer greater than zero.
	/// </param>
	/// <param name="height">
	/// The desired viewport height, expressed as a positive integer greater than zero.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentOutOfRangeException"/> if either width or height
	/// is less than or equal to zero. This ensures the viewport always has valid
	/// dimensions for rendering within the application window.
	/// </remarks>
	public EngineSettings WithViewport(uint width, uint height)
	{
		if (width == 0)
			throw new ArgumentOutOfRangeException(nameof(width), "Viewport width must be greater than zero");
		if (height == 0)
			throw new ArgumentOutOfRangeException(nameof(width), "Viewport height must be greater than zero");

		Viewport = new Vect2(width, height);

		return this;
	}
	internal Vect2 Viewport { get; private set; }

	/// <summary>
	/// Sets the safe region value for the application.
	/// </summary>
	/// <param name="value">
	/// A positive integer representing the safe region size.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// The safe region is applied to UI alignment and layout calculations to
	/// prevent important elements from being clipped or obscured. Adjust this
	/// value to accommodate varying screen sizes and overscan behavior.
	/// </remarks>
	public EngineSettings WithSafeRegion(uint value)
	{
		SafeRegion = value;

		return this;
	}
	internal uint SafeRegion { get; private set; }

	/// <summary>
	/// Sets the clear color used by the engine.
	/// </summary>
	/// <param name="color">
	/// A <see cref="Color"/> value representing the clear color. Must be fully opaque.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when the provided color is not fully opaque (Alpha ≠ 255).
	/// </exception>
	/// <remarks>
	/// The clear color is applied when the rendering surface is cleared at the start of each frame.
	/// Alpha values are ignored as the clear color is always fully opaque.
	/// </remarks>
	public EngineSettings WithClearColor(Color color)
		=> WithClearColor(color.R, color.G, color.B);

	/// <summary>
	/// Sets the clear color used by the engine using individual RGB components.
	/// </summary>
	/// <param name="r">
	/// The red component of the clear color, ranging from 0 to 255.
	/// </param>
	/// <param name="g">
	/// The green component of the clear color, ranging from 0 to 255.
	/// </param>
	/// <param name="b">
	/// The blue component of the clear color, ranging from 0 to 255.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// This overload constructs a fully opaque <see cref="Color"/> from the specified RGB values.
	/// The clear color is applied when the rendering surface is cleared at the start of each frame.
	/// </remarks>
	public EngineSettings WithClearColor(byte r, byte g, byte b)
	{
		ClearColor = new Color(r, g, b);

		return this;
	}
	internal Color ClearColor { get; private set; }

	/// <summary>
	/// Sets the collection of screen types configured for the application.
	/// </summary>
	/// <param name="screens">
	/// One or more <see cref="Type"/> instances representing screens to be used by the engine.
	/// Each type must be <see cref="Screen"/> or derive from it.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="screens"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="screens"/> is empty.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when any type in <paramref name="screens"/> is not <see cref="Screen"/>
	/// or does not derive from <see cref="Screen"/>.
	/// </exception>
	/// <remarks>
	/// This method ensures the engine has at least one valid screen type to instantiate
	/// and render during the application lifecycle.
	/// </remarks>
	public EngineSettings WithScreens(params Type[] screens)
	{
		if (screens == null)
			throw new ArgumentNullException(nameof(screens), "cannot be null.");
		if (screens.Length == 0)
			throw new ArgumentException("At least one screen must be provided");
		if (!screens.All(x => x.IsSubclassOf(typeof(Screen)) || x == typeof(Screen)))
			throw new InvalidOperationException("All types must be Screen or dervice from Screen");

		Screens = screens;

		return this;
	}
	internal Type[] Screens { get; private set; }

	/// <summary>
	/// Sets the collection of game services configured for the application.
	/// </summary>
	/// <param name="values">
	/// One or more <see cref="Service"/> instances to be registered with the engine.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentNullException"/> if the provided array is null,
	/// or an <see cref="ArgumentException"/> if no services are specified. This ensures
	/// the engine always has at least one valid service to manage game functionality.
	/// </remarks>
	public EngineSettings WithService(params Type[] values)
	{
		if (values == null)
			throw new ArgumentNullException(nameof(values), "Values cannot be null.");
		if (values.Length == 0)
			throw new ArgumentException("At least one service must be provided");

		Services = values;

		return this;
	}
	internal Type[] Services { get; private set; }

	/// <summary>
	/// Sets the maximum log file size cap.
	/// </summary>
	/// <param name="value">
	/// The desired log file size in megabytes. Must be greater than zero and
	/// less than or equal to <c>MaxLogFileSizeBytes</c>.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentOutOfRangeException"/> if the provided value
	/// is zero or exceeds <c>MaxLogFileSizeBytes</c>. The value is converted to
	/// bytes using <c>BytesPerMB</c> for internal storage.
	/// </remarks>
	public EngineSettings WithLogFileCap(uint value)
	{
		if (value == 0 || value > MaxLogFileSizeBytes)
		{
			throw new ArgumentOutOfRangeException(nameof(value),
				$"Log file size must be between 1 and {MaxLogFileSizeBytes} megabytes.");
		}

		LogFileSizeCap = (int)(value * BytesPerMB);

		return this;
	}
	internal int LogFileSizeCap { get; private set; }

	/// <summary>
	/// Sets the maximum number of recent log entries retained in memory.
	/// </summary>
	/// <param name="value">
	/// The desired number of recent log entries, expressed as a positive integer.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentOutOfRangeException"/> if the provided value
	/// is less than 1 or greater than <c>MaxLogEntries</c>. This ensures the
	/// logging system maintains a valid and bounded set of recent entries.
	/// </remarks>
	public EngineSettings WithLogMaxRecentEntries(uint value)
	{
		if (value == 0 || value > MaxLogEntries)
		{
			throw new ArgumentOutOfRangeException(nameof(value),
				$"Log max recent entries must be between 1 and {MaxLogEntries} inclusive.");
		}

		LogMaxRecentEntries = (int)value;

		return this;
	}
	internal int LogMaxRecentEntries { get; private set; }

	/// <summary>
	/// Sets the number of minutes an asset can remain unused before being eligible for eviction.
	/// </summary>
	/// <param name="minutes">
	/// The eviction timeout in minutes. Must be greater than zero.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for method chaining.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="minutes"/> is zero.
	/// </exception>
	public EngineSettings WithAssetEvictionMinutes(uint minutes)
	{
		if (minutes == 0)
			throw new ArgumentOutOfRangeException(nameof(minutes), "asset eviction minutes to be greater than zero");

		AssetEvictionMinutes = (int)minutes;

		return this;
	}
	internal int AssetEvictionMinutes { get; private set; }


	/// <summary>
	/// Registers a callback to be invoked when an unhandled exception occurs in the engine.
	/// </summary>
	/// <param name="action">
	/// The callback to invoke when an unhandled exception is caught.
	/// The first parameter is the source object, and the second contains the exception event arguments.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance, enabling fluent method chaining.
	/// </returns>
	/// <remarks>
	/// This hook allows applications to log crashes, display error messages, or perform cleanup
	/// before the engine terminates. If no handler is set, the engine will use its default crash behavior.
	/// </remarks>
	public EngineSettings WithOnCrash(Action<object, UnhandledExceptionEventArgs> action)
	{
		OnCrash = action;

		return this;
	}
	internal Action<object, UnhandledExceptionEventArgs> OnCrash { get; private set; }


	/// <summary>
	/// Registers a callback to be invoked when the engine finishes its startup sequence.
	/// </summary>
	/// <param name="action">The callback to invoke on engine startup.</param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance, enabling fluent method chaining.
	/// </returns>
	/// <remarks>
	/// This is called after all engine systems are initialized but before the first frame is processed.
	/// Use it for loading initial assets, setting up services, or configuring initial screen state.
	/// </remarks>
	public EngineSettings WithOnStartup(Action action)
	{
		OnStartup = action;

		return this;
	}
	internal Action OnStartup { get; private set; }



	/// <summary>
	/// Registers a callback to be invoked when the engine begins its shutdown sequence.
	/// </summary>
	/// <param name="action">The callback to invoke on engine shutdown.</param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance, enabling fluent method chaining.
	/// </returns>
	/// <remarks>
	/// This is called when the engine is about to shut down, before any systems are disposed.
	/// Use it for saving persistent state, flushing logs, or cleaning up external resources.
	/// </remarks>
	public EngineSettings WithOnShutdown(Action action)
	{
		OnShutdown = action;

		return this;
	}
	internal Action OnShutdown { get; private set; }



	/// <summary>
	/// Sets the culling range used for viewport-based object culling.
	/// </summary>
	/// <param name="width">
	/// The culling width. Must be greater than zero.
	/// </param>
	/// <param name="height">
	/// The culling height. Must be greater than zero.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for method chaining.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="width"/> or <paramref name="height"/> is zero.
	/// </exception>
	public EngineSettings WithCullRange(uint width, uint height)
	{
		if (width == 0)
			throw new ArgumentOutOfRangeException(nameof(width), "Cull range width has to be greater than zero");
		if (height == 0)
			throw new ArgumentOutOfRangeException(nameof(height), "Cull range height has to be greater than zero");

		CullRange = new Vect2(width, height);

		return this;
	}
	internal Vect2 CullRange { get; private set; }



	/// <summary>
	/// Finalizes the engine settings configuration.
	/// </summary>
	/// <remarks>
	/// <list type="bullet">
	///   <item>
	///     <description>At least one screen must be configured.</description>
	///   </item>
	///   <item>
	///     <description><c>AppCompany</c> and <c>AppName</c> must be provided and non-empty.</description>
	///   </item>
	///   <item>
	///     <description><c>AppTitle</c>, <c>LogDirectory</c>, and <c>SaveDirectory</c> are
	///     assigned default values if not specified.</description>
	///   </item>
	///   <item>
	///     <description>The content root is resolved to either a <c>Content</c> or <c>Assets</c>
	///     directory if not explicitly set.</description>
	///   </item>
	///   <item>
	///     <description>Default values are applied for window size, viewport size, clear color,
	///     input map, atlas settings, draw call cache, dead zone, log file size cap,
	///     recent log entries, safe region, and batch increasement.</description>
	///   </item>
	///   <item>
	///     <description>Throws exceptions if required properties are missing or invalid, ensuring
	///     the engine cannot start with an incomplete configuration. Once executed,
	///     the settings are marked as initialized and subsequent calls return the
	///     current instance without reapplying defaults.</description>
	///   </item>
	/// </list>
	/// </remarks>
	public EngineSettings Build()
	{
		if (Initialized)
			return this;

		// Screens
		if (Screens is null || Screens.Length == 0)
			throw new InvalidOperationException("At least one screen must be configured.");

		// Company & AppName
		if (string.IsNullOrWhiteSpace(AppCompany))
		{
			throw new ArgumentException(
				"Company must be provided and cannot be empty or whitespace.", nameof(AppCompany));
		}

		if (string.IsNullOrWhiteSpace(AppName))
		{
			throw new ArgumentException(
				"AppName must be provided and cannot be empty or whitespace.", nameof(AppName));
		}

		// AppTitle
		AppTitle = string.IsNullOrWhiteSpace(AppTitle) ? "Game" : AppTitle.Trim();
		LogDirectory = string.IsNullOrWhiteSpace(LogDirectory) ? "Logs" : LogDirectory.Trim();
		SaveDirectory = string.IsNullOrWhiteSpace(SaveDirectory) ? "Saves" : SaveDirectory.Trim();

		// Content root
		if (string.IsNullOrWhiteSpace(AppContentRoot))
		{
			if (Directory.Exists("Content"))
			{
				AppContentRoot = "Content";
			}
			else if (Directory.Exists("Assets"))
			{
				AppContentRoot = "Assets";
			}
			else
			{
				throw new DirectoryNotFoundException(
					"No content directory found. Expected to find either a 'Content' or 'Assets' folder.");
			}
		}

		// Window & Viewport defaults
		if (Window.X <= 0 || Window.Y <= 0)
			Window = new Vect2(1280, 720);
		if (Viewport.X <= 0 || Viewport.Y <= 0)
			Viewport = new Vect2(320, 180);

		if (AppVersion == null)
			AppVersion = new(1, 0, 0, 0);

		// ClearColor default
		if (ClearColor == Color.Transparent)
			ClearColor = Color.CornFlowerBlue;

		// InputMap
		InputMap ??= new DefaultInputMap();

		// Atlas & cache defaults
		MaxAtlasPages = MaxAtlasPages > 0 ? MaxAtlasPages : 3;
		AtlasPageSize = AtlasPageSize > 0 ? AtlasPageSize : 512;
		DrawCallCache = DrawCallCache > 0 ? DrawCallCache : 512;
		DeadZone = DeadZone > 0 ? DeadZone : 0.2f;

		// Logfiles:
		LogFileSizeCap = LogFileSizeCap > 0 ? LogFileSizeCap : 1_000_000;
		LogMaxRecentEntries = LogMaxRecentEntries > 0 ? LogMaxRecentEntries : 100;

		SafeRegion = SafeRegion > 0 ? SafeRegion : 8;

		BatchIncreasment = BatchIncreasment > 0 ? BatchIncreasment : (int)MinBatchIncrease;

		Initialized = true;

		return this;
	}
}
