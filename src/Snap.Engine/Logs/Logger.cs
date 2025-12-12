
namespace Snap.Engine.Logs;

/// <summary>
/// Specifies the severity level of a log entry.
/// </summary>
public enum LogLevel
{
	/// <summary>
	/// Informational messages that highlight the progress of the application
	/// at a coarse-grained level.
	/// </summary>
	Info,

	/// <summary>
	/// Potentially harmful situations or recoverable issues that warrant attention
	/// but do not stop the application.
	/// </summary>
	Warning,

	/// <summary>
	/// Error events that might still allow the application to continue running,
	/// but indicate a failure in a component or operation.
	/// </summary>
	Error,
}

/// <summary>
/// Central logger that fans out entries to multiple sinks and provides utility methods.
/// </summary>
/// <remarks>
/// The <see cref="Logger"/> class acts as a hub for log output. It can write to multiple
/// <see cref="ILogSink"/> targets simultaneously, ensuring that log entries are distributed
/// across different destinations (e.g., console, file).
/// 
/// In addition to basic write operations, it provides utility methods for flushing,
/// rotating sinks, and managing log levels.
/// </remarks>
public sealed class Logger : TextWriter, IDisposable
{
	private readonly List<ILogSink> _sinks = [];
	private readonly Queue<string> _recentEntries;
	private readonly int _maxRecentEntries;
	private readonly Lock _syncLock = new();
	private bool _disposed;

	/// <summary>
	/// Gets the singleton instance of the <see cref="Logger"/>.
	/// </summary>
	/// <remarks>
	/// This property provides global access to the central logger. It is initialized once
	/// and shared across the application.
	/// </remarks>
	public static Logger Instance { get; private set; }

	/// <summary>
	/// Gets the character encoding used by the logger.
	/// </summary>
	/// <remarks>
	/// Always returns <see cref="Encoding.Default"/> to match the system’s default encoding.
	/// </remarks>
	public override Encoding Encoding => Encoding.Default;

	/// <summary>
	/// Gets or sets the minimum log level for entries to be written.
	/// </summary>
	/// <remarks>
	/// Entries below the specified <see cref="LogLevel"/> are ignored. Defaults to <see cref="LogLevel.Info"/>.
	/// </remarks>
	public LogLevel Level { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="Logger"/> class.
	/// </summary>
	/// <param name="minLevel">
	/// The minimum <see cref="LogLevel"/> required for entries to be written.
	/// Entries below this level are ignored.
	/// </param>
	/// <param name="maxRecentEntries">
	/// The maximum number of recent log entries to retain in memory.
	/// </param>
	/// <remarks>
	/// This constructor enforces a singleton pattern. If an instance of <see cref="Logger"/> already exists,
	/// an <see cref="InvalidOperationException"/> is thrown. The <see cref="Instance"/> property is set to
	/// the newly created logger.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Thrown if a <see cref="Logger"/> instance has already been initialized.
	/// </exception>
	public Logger(LogLevel minLevel = LogLevel.Info, int maxRecentEntries = 100)
	{
		if (Instance != null)
			throw new InvalidOperationException("Logger already initialized.");

		Instance ??= this;

		Level = minLevel;
		_maxRecentEntries = maxRecentEntries;
		_recentEntries = new Queue<string>(maxRecentEntries);
	}

	/// <summary>
	/// Adds a log sink to the logger (e.g., console, file).
	/// </summary>
	/// <param name="sink">
	/// The <see cref="ILogSink"/> instance to add. Once added, the sink will receive
	/// all subsequent log entries.
	/// </param>
	/// <remarks>
	/// This method is thread-safe. If the <see cref="Logger"/> has already been disposed,
	/// an <see cref="ObjectDisposedException"/> is thrown.
	/// </remarks>
	/// <exception cref="ObjectDisposedException">
	/// Thrown if the <see cref="Logger"/> has already been disposed.
	/// </exception>
	public void AddSink(ILogSink sink)
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(Logger));

		using (_syncLock.EnterScope())
		{
			_sinks.Add(sink);
		}
	}

	/// <summary>
	/// Writes a single character to all registered log sinks.
	/// </summary>
	/// <param name="value">The character to write.</param>
	/// <remarks>
	/// Before writing, each sink is checked for rotation needs using <see cref="ILogSink.RotateIfNeeded(long)"/>.
	/// If the logger has been disposed, the method returns immediately without performing any action.
	/// </remarks>
	public override void Write(char value)
	{
		if (_disposed)
			return;

		using (_syncLock.EnterScope())
		{
			foreach (var s in _sinks)
			{
				s.RotateIfNeeded(1);
				s.Write(value);
			}
		}
	}

	/// <summary>
	/// Writes a string of text to all registered log sinks.
	/// </summary>
	/// <param name="value">The text to write.</param>
	/// <remarks>
	/// The method calculates the byte count of the string using the default encoding
	/// to determine if rotation is needed before writing. Each sink is then instructed
	/// to rotate if necessary and receives the text.
	/// 
	/// If the <see cref="Logger"/> has been disposed, the method returns immediately
	/// without performing any action.
	/// </remarks>
	public override void Write(string value)
	{
		if (_disposed)
			return;

		var bytes = Encoding.Default.GetByteCount(value);

		using (_syncLock.EnterScope())
		{
			foreach (var s in _sinks)
			{
				s.RotateIfNeeded(bytes);
				s.Write(value);
			}
		}
	}

	/// <summary>
	/// Writes a string of text followed by a line terminator to all registered log sinks.
	/// </summary>
	/// <param name="value">The text to write before the line terminator.</param>
	/// <remarks>
	/// The method calculates the byte count of the text plus the line terminator using the default encoding
	/// to determine if rotation is needed before writing. Each sink is instructed to rotate if necessary
	/// and then receives the text.  
	/// 
	/// The written entry is also enqueued into the recent entries buffer. If the buffer exceeds
	/// <see cref="_maxRecentEntries"/>, the oldest entry is dequeued to maintain the limit.
	/// 
	/// If the <see cref="Logger"/> has been disposed, the method returns immediately without performing any action.
	/// </remarks>
	public override void WriteLine(string value)
	{
		if (_disposed)
			return;

		var line = value;
		var bytes = Encoding.Default.GetByteCount(line + Environment.NewLine);

		using (_syncLock.EnterScope())
		{
			foreach (var s in _sinks)
			{
				s.RotateIfNeeded(bytes);
				s.WriteLine(line);
			}
			_recentEntries.Enqueue(line);

			if (_recentEntries.Count > _maxRecentEntries)
				_recentEntries.Dequeue();
		}
	}

	/// <summary>
	/// Flushes all registered log sinks, ensuring that any buffered data is written out.
	/// </summary>
	/// <remarks>
	/// This method iterates through each sink and calls its <see cref="ILogSink.Flush"/> method.
	/// If the <see cref="Logger"/> has been disposed, the method returns immediately without performing any action.
	/// </remarks>
	public override void Flush()
	{
		if (_disposed)
			return;

		using (_syncLock.EnterScope())
		{
			foreach (var s in _sinks)
				s.Flush();
		}
	}

	/// <summary>
	/// Logs a message with the specified severity level.
	/// </summary>
	/// <param name="level">
	/// The <see cref="LogLevel"/> of the message. Messages below the current <see cref="Logger.Level"/> are ignored.
	/// </param>
	/// <param name="message">
	/// The message text to log.
	/// </param>
	/// <remarks>
	/// The method prefixes the message with a severity indicator and timestamp, then writes it to all registered sinks
	/// using <see cref="WriteLine(string)"/>.  
	/// 
	/// Additionally, the entry is written to the debug output via <see cref="Debug.WriteLine(string)"/>.
	/// If the <see cref="Logger"/> has been disposed, or if the specified level is lower than the configured minimum,
	/// the message is ignored.
	/// </remarks>
	public void Log(LogLevel level, string message)
	{
		if (_disposed || level < Level)
			return;

		string prefix = level switch
		{
			LogLevel.Warning => "[⚠️ WARNING]",
			LogLevel.Error => "[❌ ERROR]",
			_ => "[INFO]"
		};

		string entry = $"{DateTime.Now:HH:mm:ss} {prefix}: {message}";

		WriteLine(entry);

		Debug.WriteLine(entry);
	}

	/// <summary>
	/// Logs details of an exception and its inner exceptions.
	/// </summary>
	/// <param name="ex">
	/// The <see cref="Exception"/> to log. Both its message and stack trace are recorded.
	/// </param>
	/// <param name="level">
	/// The <see cref="LogLevel"/> to use when logging the exception. Defaults to <see cref="LogLevel.Error"/>.
	/// </param>
	/// <remarks>
	/// This method traverses the exception chain, logging each unique exception message and stack trace.
	/// A <see cref="HashSet{T}"/> is used to avoid infinite loops in case of cyclic inner exceptions.
	/// 
	/// If the <see cref="Logger"/> has been disposed, or if the specified level is lower than the configured minimum,
	/// the exception is ignored.
	/// </remarks>
	public void LogException(Exception ex, LogLevel level = LogLevel.Error)
	{
		if (_disposed || level < Level) return;
		var seen = new HashSet<Exception>();
		var current = ex;
		while (current != null && !seen.Contains(current))
		{
			seen.Add(current);
			Log(level, $"Exception: {current.Message}");
			if (!string.IsNullOrEmpty(current.StackTrace)) Log(level, current.StackTrace);
			current = current.InnerException;
		}
	}

	/// <summary>
	/// Logs a collection of key–value pairs at the specified severity level.
	/// </summary>
	/// <param name="level">
	/// The <see cref="LogLevel"/> to use when logging the fields. Entries below the current
	/// <see cref="Level"/> are ignored.
	/// </param>
	/// <param name="fields">
	/// A set of key–value pairs to log. Each pair is formatted as <c>Key=Value</c>,
	/// separated by spaces.
	/// </param>
	/// <remarks>
	/// The method builds a single log entry string from the provided fields and passes it
	/// to <see cref="Log(LogLevel, string)"/>.  
	/// 
	/// If the <see cref="Logger"/> has been disposed, or if the specified level is lower than
	/// the configured minimum, the fields are ignored.
	/// </remarks>
	public void LogFields(LogLevel level, params (string Key, object Value)[] fields)
	{
		if (_disposed || level < Level)
			return;

		var sb = new StringBuilder();

		for (int i = 0; i < fields.Length; i++)
		{
			var (key, value) = fields[i];

			sb.Append(key).Append('=').Append(value);
			if (i < fields.Length - 1)
				sb.Append(' ');
		}

		Log(level, sb.ToString());
	}

	/// <summary>
	/// Retrieves a snapshot of the most recent log entries.
	/// </summary>
	/// <returns>
	/// An array containing the recent log entries, ordered from oldest to newest.
	/// </returns>
	/// <remarks>
	/// The number of entries returned is limited by <see cref="_maxRecentEntries"/>.
	/// Thread-safe access is ensured by locking on <see cref="_syncLock"/>.
	/// </remarks>
	public string[] GetRecentEntries()
	{
		using (_syncLock.EnterScope())
		{
			return [.. _recentEntries];
		}
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="Logger"/> and optionally
	/// disposes of the managed resources.
	/// </summary>
	/// <param name="disposing">
	/// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
	/// </param>
	/// <remarks>
	/// When disposing, all registered sinks that implement <see cref="IDisposable"/> are disposed,
	/// the sink collection is cleared, and the singleton <see cref="Logger.Instance"/> is reset to <c>null</c>.
	/// </remarks>
	protected override void Dispose(bool disposing)
	{
		if (disposing && !_disposed)
		{
			using (_syncLock.EnterScope())
			{
				foreach (var s in _sinks)
					if (s is IDisposable d) d.Dispose();

				_sinks.Clear();

				_disposed = true;
				Instance = null;
			}
		}
		base.Dispose(disposing);
	}

	/// <summary>
	/// Disposes the <see cref="Logger"/> instance and releases all associated resources.
	/// </summary>
	public new void Dispose() => Dispose(true);
}