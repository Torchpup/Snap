namespace Snap.Engine.Logs;

/// <summary>
/// Provides a file-based log sink that writes log entries to disk.
/// Supports rolling by file size and automatically generates date-based filenames
/// with daily rollover.
/// </summary>
/// <remarks>
/// Typical usage involves creating an instance with a target directory and base filename.
/// The sink will append log entries to the current file until either:
/// <list type="bullet">
///   <item>
///     <description>The file exceeds the configured maximum size.</description>
///   </item>
///   <item>
///     <description>The date changes, triggering a daily rollover.</description>
///   </item>
/// </list>
/// When rollover occurs, a new file is created with a timestamped name.
/// </remarks>
public class FileLogSink : ILogSink, IDisposable
{
	private readonly string _logDirectory;
	private readonly long _maxFileSizeBytes;
	private readonly int _maxRollFiles;
	private long _currentSize;
	private StreamWriter _writer;

	private DateTime _currentDate;
	private string _fileBaseName;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileLogSink"/> class.
	/// </summary>
	/// <param name="logDirectory">
	/// The directory where log files will be created and stored.
	/// </param>
	/// <param name="maxFileSizeBytes">
	/// The maximum size, in bytes, allowed for a single log file before rotation occurs.
	/// </param>
	/// <param name="maxRollFiles">
	/// The maximum number of rolled log files to retain. Older files beyond this limit may be deleted or overwritten.
	/// </param>
	public FileLogSink(string logDirectory, long maxFileSizeBytes, int maxRollFiles)
	{
		_logDirectory = logDirectory;
		_maxFileSizeBytes = maxFileSizeBytes;
		_maxRollFiles = maxRollFiles;

		Directory.CreateDirectory(_logDirectory);
		_currentDate = DateTime.Now.Date;
		_fileBaseName = _currentDate.ToString("ddd-MMM-yyyy");
		OpenWriter();
	}

	private void OpenWriter()
	{
		if (_writer != null)
		{
			try { _writer.Flush(); } catch { /* swallow flush errors */ }
			try { _writer.Dispose(); } catch { /* swallow dispose errors */ }
		}
		string path = GetBasePath();
		_currentSize = File.Exists(path) ? new FileInfo(path).Length : 0;
		var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
		_writer = new StreamWriter(fs, Encoding.Default) { AutoFlush = true };
	}

	private string GetBasePath() => Path.Combine(_logDirectory, _fileBaseName + ".log");
	private string GetRolledPath(int idx) => Path.Combine(_logDirectory, $"{_fileBaseName}-{idx}.log");

	private void CheckDateRollover()
	{
		var today = DateTime.Now.Date;
		if (today != _currentDate)
		{
			// Safely close old writer
			try { _writer.Close(); } catch { /* swallow close errors */ }
			try { _writer.Dispose(); } catch { /* swallow dispose errors */ }

			_currentDate = today;
			_fileBaseName = _currentDate.ToString("ddd-MMM-yyyy");
			OpenWriter();
		}
	}

	/// <summary>
	/// Writes a single character to the current log file.
	/// </summary>
	/// <param name="value">The character to write.</param>
	public void Write(char value)
	{
		CheckDateRollover();
		RotateIfNeeded(1);
		_writer.Write(value);
		_currentSize++;
	}

	/// <summary>
	/// Writes a string of text to the current log file.
	/// </summary>
	/// <param name="text">The text to write.</param>
	/// <remarks>
	/// The method calculates the byte count of the string using the default encoding
	/// to determine if a file rotation is needed before writing.
	/// </remarks>
	public void Write(string text)
	{
		CheckDateRollover();
		var bytes = Encoding.Default.GetByteCount(text);
		RotateIfNeeded(bytes);
		_writer.Write(text);
		_currentSize += bytes;
	}

	/// <summary>
	/// Writes a string of text followed by a line terminator to the current log file.
	/// </summary>
	/// <param name="text">The text to write before the line terminator.</param>
	/// <remarks>
	/// The method appends <see cref="Environment.NewLine"/> to the provided text, calculates the byte count
	/// using the default encoding, and determines if a file rotation is needed before writing.
	/// </remarks>
	public void WriteLine(string text)
	{
		CheckDateRollover();
		var line = text + Environment.NewLine;
		var bytes = Encoding.Default.GetByteCount(line);
		RotateIfNeeded(bytes);
		_writer.WriteLine(text);
		_currentSize += bytes;
	}

	/// <summary>
	/// Flushes any buffered log data to the underlying file.
	/// </summary>
	/// <remarks>
	/// Ensures that all written content is committed to disk immediately.
	/// </remarks>
	public void Flush() => _writer.Flush();

	/// <summary>
	/// Rotates the current log file if writing the upcoming data would exceed the maximum file size.
	/// </summary>
	/// <param name="upcomingBytes">
	/// The number of bytes that are about to be written. Used to determine whether rotation is required.
	/// </param>
	/// <remarks>
	/// If rotation is needed, the current writer is safely closed and disposed, existing rolled files are shifted,
	/// and a new writer is opened for continued logging.
	/// </remarks>
	public void RotateIfNeeded(long upcomingBytes)
	{
		if (_currentSize + upcomingBytes <= _maxFileSizeBytes)
			return;

		// Safely close and dispose current writer
		try { _writer.Close(); } catch { /* swallow close errors */ }
		try { _writer.Dispose(); } catch { /* swallow dispose errors */ }

		for (int i = _maxRollFiles; i >= 1; i--)
		{
			string dst = GetRolledPath(i);
			string src = (i == 1) ? GetBasePath() : GetRolledPath(i - 1);
			try { if (File.Exists(dst)) File.Delete(dst); } catch { /* swallow delete errors */ }
			try { if (File.Exists(src)) File.Move(src, dst); } catch { /* swallow move errors */ }
		}

		OpenWriter();
	}

	/// <summary>
	/// Releases all resources used by the <see cref="FileLogSink"/>.
	/// </summary>
	/// <remarks>
	/// Flushes any buffered data and disposes the underlying writer.
	/// </remarks>
	public void Dispose()
	{
		try { _writer?.Flush(); } catch { }
		try { _writer?.Dispose(); } catch { }

		GC.SuppressFinalize(this);
	}
}