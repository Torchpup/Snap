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
	private const int MinBatchIncrease = 32, MaxBatchIncrease = 1024;
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
	/// Gets the current batch increasement value for the engine.
	/// </summary>
	/// <remarks>
	/// This value determines how many items are processed or allocated
	/// in a single batch operation. It is constrained by the minimum and
	/// maximum batch limits defined in the engine.
	/// </remarks>
	internal int BatchIncreasment { get; private set; }
	
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



	/// <summary>
	/// Gets a value indicating whether half-texel offset rendering is enabled.
	/// </summary>
	/// <remarks>
	/// When enabled, rendering applies a half-texel offset to improve texture sampling
	/// accuracy and alignment. This can help avoid artifacts in certain graphics pipelines.
	/// </remarks>
	internal bool HalfTexelOffset { get; private set; }
	
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

	/// <summary>
	/// Gets the current logging level for the engine.
	/// </summary>
	/// <remarks>
	/// The logging level controls the verbosity of diagnostic output.
	/// Typical values include Debug, Info, Warning, and Error.
	/// </remarks>
	internal LogLevel LogLevel { get; private set; }
	
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




	/// <summary>
	/// Gets the directory path used for saving game data.
	/// </summary>
	/// <remarks>
	/// The save directory must be a valid, non-empty path string. It should not contain
	/// the path separator character defined by <see cref="Path.PathSeparator"/>.
	/// </remarks>
	internal string SaveDirectory { get; private set; }
	
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



	/// <summary>
	/// Gets the directory path used for storing log files.
	/// </summary>
	/// <remarks>
	/// The log directory must be a valid, non-empty path string. It cannot contain
	/// invalid path characters or the path separator character defined by
	/// <see cref="Path.PathSeparator"/>.
	/// </remarks>
	internal string LogDirectory { get; private set; }
	
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


	/// <summary>
	/// Gets a value indicating whether mouse input is enabled for the engine.
	/// </summary>
	/// <remarks>
	/// When enabled, the engine will process mouse events such as movement,
	/// clicks, and scroll input. Disabling this option ignores mouse input
	/// during runtime.
	/// </remarks>
	internal bool Mouse { get; private set; }
	
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


	/// <summary>
	/// Gets a value indicating whether debug drawing is enabled.
	/// </summary>
	/// <remarks>
	/// When enabled, the engine will render additional visual information
	/// such as bounding boxes, guides, or diagnostic overlays to assist
	/// with debugging and development.
	/// </remarks>
	internal bool DebugDraw { get; private set; }
	
		
	/// <summary>
	/// Sets whether debug drawing should be enabled.
	/// </summary>
	/// <param name="value">
	/// A boolean value specifying whether debug drawing is enabled.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Enabling this option allows the engine to display visual debugging aids
	/// during runtime. Disable it for normal gameplay rendering without overlays.
	/// </remarks>
	public EngineSettings WithDebugDraw(bool value)
	{
		DebugDraw = value;

		return this;
	}


	/// <summary>
	/// Gets the company name associated with the application.
	/// </summary>
	/// <remarks>
	/// This value is typically used for metadata, save paths, or logging
	/// to identify the organization or developer responsible for the game.
	/// </remarks>
	internal string AppCompany { get; private set; }
	
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



	/// <summary>
	/// Gets a value indicating whether partial paths are allowed during pathfinding.
	/// </summary>
	/// <remarks>
	/// When enabled, the pathfinding system may return incomplete or partial paths
	/// if a full path to the destination cannot be found. This can be useful for
	/// fallback navigation or exploratory movement in complex maps.
	/// </remarks>
	internal bool AllowPartialPaths { get; private set; }
	
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


	/// <summary>
	/// Gets a value indicating whether vertical synchronization (VSync) is enabled.
	/// </summary>
	/// <remarks>
	/// When enabled, rendering is synchronized with the display's refresh rate to
	/// reduce screen tearing. Disabling VSync may improve performance but can
	/// introduce visual artifacts.
	/// </remarks>
	internal bool VSync { get; set; }
	
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


	/// <summary>
	/// Gets a value indicating whether the engine runs in full-screen mode.
	/// </summary>
	/// <remarks>
	/// When enabled, the application will occupy the entire display surface.
	/// Disabling this option allows the engine to run in windowed mode.
	/// </remarks>
	internal bool FullScreen { get; set; }
	
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


	/// <summary>
	/// Gets the antialiasing level used by the engine.
	/// </summary>
	/// <remarks>
	/// Antialiasing reduces visual artifacts such as jagged edges by smoothing
	/// rendered graphics. The value typically represents the number of samples
	/// (e.g., 2x, 4x, 8x) applied during rendering.
	/// </remarks>
	internal int Antialiasing { get; set; }
	
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


	/// <summary>
	/// Gets a value indicating whether window resizing is enabled.
	/// </summary>
	/// <remarks>
	/// When enabled, the application window can be resized by the user.
	/// Disabling this option locks the window to its initial dimensions.
	/// </remarks>
	internal bool WindowResize { get; private set; }
	
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



	/// <summary>
	/// Gets the size of the draw call cache used by the engine.
	/// </summary>
	/// <remarks>
	/// The draw call cache determines how many draw calls can be stored
	/// before being processed. This value must be greater than or equal
	/// to <c>MinimumDrawllCallCacheSize</c> to ensure stable rendering.
	/// </remarks>
	internal int DrawCallCache { get; private set; }
	
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


	/// <summary>
	/// Gets the atlas page size used by the engine.
	/// </summary>
	/// <remarks>
	/// The atlas page size defines the dimensions of texture atlas pages.
	/// This value must be greater than or equal to <c>MinimumAtlasPageSize</c>
	/// to ensure proper texture packing and rendering.
	/// </remarks>
	internal int AtlasPageSize { get; private set; }
	
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




	/// <summary>
	/// Gets the maximum number of atlas pages allowed by the engine.
	/// </summary>
	/// <remarks>
	/// This value defines the upper limit of texture atlas pages that can be
	/// allocated. It must be between <c>MinPages</c> and <c>MaxPages</c>
	/// inclusive to ensure stable texture management.
	/// </remarks>
	internal int MaxAtlasPages { get; private set; }
	
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



	/// <summary>
	/// Gets the input dead zone value.
	/// </summary>
	/// <remarks>
	/// The dead zone defines the threshold below which input values (such as from
	/// analog sticks or triggers) are ignored. This helps prevent unintended
	/// movement caused by slight hardware drift. The value must be between
	/// <c>0.0</c> and <c>1.0</c> inclusive.
	/// </remarks>
	internal float DeadZone { get; private set; }
	
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


	/// <summary>
	/// Gets the input map used by the engine.
	/// </summary>
	/// <remarks>
	/// The input map defines the collection of input actions and bindings
	/// available to the application. It must contain at least one action
	/// to be considered valid.
	/// </remarks>
	internal InputMap InputMap { get; private set; }
	
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
		if (value._actions.Count == 0)
			throw new InvalidOperationException("Actions collection must contain at least one item.");

		InputMap = value;

		return this;
	}


	/// <summary>
	/// Gets the application title.
	/// </summary>
	/// <remarks>
	/// The application title is used for display purposes, such as window captions,
	/// metadata, and logging. It must be a non-empty string.
	/// </remarks>
	internal string AppTitle { get; private set; }
	
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



	/// <summary>
	/// Gets the application name.
	/// </summary>
	/// <remarks>
	/// The application name is used for display purposes and also serves as the
	/// identifier for the application data folder. It must be a non-empty string
	/// to ensure valid metadata and storage paths.
	/// </remarks>
	internal string AppName { get; private set; }
	
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



	/// <summary>
	/// Gets the application content root directory.
	/// </summary>
	/// <remarks>
	/// The content root specifies the base directory where application assets
	/// such as textures, audio, and configuration files are located. The path
	/// must exist and be a valid directory.
	/// </remarks>
	internal string AppContentRoot { get; private set; }
	
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



	/// <summary>
	/// Gets the window dimensions for the application.
	/// </summary>
	/// <remarks>
	/// The window size is represented as a <see cref="Vect2"/> containing width
	/// and height values. Both dimensions must be greater than zero to ensure
	/// a valid rendering surface.
	/// </remarks>
	internal Vect2 Window { get; set; }
	
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




	/// <summary>
	/// Gets the viewport dimensions for the application.
	/// </summary>
	/// <remarks>
	/// The viewport defines the rendering area within the window. It is represented
	/// as a <see cref="Vect2"/> containing width and height values. Both dimensions
	/// must be greater than zero to ensure a valid rendering surface.
	/// </remarks>
	internal Vect2 Viewport { get; private set; }
	
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



	/// <summary>
	/// Gets the safe region value for the application.
	/// </summary>
	/// <remarks>
	/// The safe region is primarily used for alignment and UI layout purposes.
	/// It defines a margin or padding area to ensure that critical interface
	/// elements remain visible and properly positioned across different display
	/// devices and aspect ratios.
	/// </remarks>
	internal uint SafeRegion { get; private set; }
	
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


	/// <summary>
	/// Gets the clear color used by the engine.
	/// </summary>
	/// <remarks>
	/// The clear color defines the background color applied when clearing the
	/// rendering surface. It must be fully opaque (Alpha = 255) to ensure
	/// consistent rendering results.
	/// </remarks>
	internal Color ClearColor { get; private set; }
	
	/// <summary>
	/// Sets the clear color used by the engine.
	/// </summary>
	/// <param name="color">
	/// A <see cref="Color"/> value representing the clear color. Must be fully opaque.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentException"/> if the provided color is not fully
	/// opaque (Alpha ≠ 255). The clear color is applied when the rendering surface
	/// is reset at the start of each frame.
	/// </remarks>
	public EngineSettings WithClearColor(Color color)
	{
		if (color.A != 255)
			throw new ArgumentException("Clear color must be fully opaque (Alpha = 255).", nameof(color));

		ClearColor = color;

		return this;
	}




	/// <summary>
	/// Gets the collection of screens configured for the application.
	/// </summary>
	/// <remarks>
	/// Screens represent the rendering targets or display surfaces available
	/// to the engine. At least one screen must be provided to ensure the
	/// application has a valid output surface.
	/// </remarks>
	internal Screen[] Screens { get; private set; }
	
	/// <summary>
	/// Sets the collection of screens configured for the application.
	/// </summary>
	/// <param name="values">
	/// One or more <see cref="Screen"/> instances to be used by the engine.
	/// </param>
	/// <returns>
	/// The current <see cref="EngineSettings"/> instance for fluent configuration.
	/// </returns>
	/// <remarks>
	/// Throws an <see cref="ArgumentNullException"/> if the provided array is null,
	/// or an <see cref="ArgumentException"/> if no screens are specified. This ensures
	/// that the engine always has at least one valid screen to render to.
	/// </remarks>
	public EngineSettings WithScreens(params Screen[] values)
	{
		if (values == null)
			throw new ArgumentNullException(nameof(values), "Values cannot be null.");
		if (values.Length == 0)
			throw new ArgumentException("At least one screen must be provided");

		Screens = values;

		return this;
	}




	/// <summary>
	/// Gets the collection of game services configured for the application.
	/// </summary>
	/// <remarks>
	/// Game services provide reusable functionality such as input handling,
	/// audio playback, networking, or other subsystems. At least one service
	/// must be provided to ensure the engine has valid runtime support.
	/// </remarks>
	internal Service[] Services { get; private set; }
	
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
	public EngineSettings WithService(params Service[] values)
	{
		if (values == null)
			throw new ArgumentNullException(nameof(values), "Values cannot be null.");
		if (values.Length == 0)
			throw new ArgumentException("At least one service must be provided");

		Services = values;

		return this;
	}



	/// <summary>
	/// Gets the maximum log file size cap in bytes.
	/// </summary>
	/// <remarks>
	/// The log file size cap defines the upper limit for a single log file.
	/// Once the file reaches this size, rotation or truncation policies may
	/// be applied depending on the engine’s logging configuration. The value
	/// is stored internally in bytes, calculated from the specified megabytes.
	/// </remarks>
	internal int LogFileSizeCap { get; private set; }
	
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
	
	

	/// <summary>
	/// Gets the maximum number of recent log entries retained in memory.
	/// </summary>
	/// <remarks>
	/// This value determines how many of the most recent log entries are kept
	/// for quick access. It must be between 1 and <c>MaxLogEntries</c> inclusive
	/// to ensure valid logging behavior.
	/// </remarks>
	internal int LogMaxRecentEntries { get; private set; }
	
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

		// ClearColor default
		if (ClearColor == Color.Transparent)
			ClearColor = Color.CornFlowerBlue;

		// InputMap
		InputMap ??= new DefaultInputMap();

		// Atlas & cache defaults
		MaxAtlasPages = MaxAtlasPages > 0 ? MaxAtlasPages : 6;
		AtlasPageSize = AtlasPageSize > 0 ? AtlasPageSize : 512;
		DrawCallCache = DrawCallCache > 0 ? DrawCallCache : 512;
		DeadZone = DeadZone > 0 ? DeadZone : 0.2f;

		// Logfiles:
		LogFileSizeCap = LogFileSizeCap > 0 ? LogFileSizeCap : 1_000_000;
		LogMaxRecentEntries = LogMaxRecentEntries > 0 ? LogMaxRecentEntries : 100;

		SafeRegion = SafeRegion > 0 ? SafeRegion : 8;

		BatchIncreasment = BatchIncreasment > 0 ? BatchIncreasment : MinBatchIncrease;

		Initialized = true;

		return this;
	}
}
