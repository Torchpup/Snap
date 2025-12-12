namespace Snap.Engine.Logs;

/// <summary>
/// Defines a sink for log entries. A log sink is a target that receives and processes log output.
/// </summary>
public interface ILogSink
{
	/// <summary>
	/// Writes a single character to the log sink.
	/// </summary>
	/// <param name="value">The character to write.</param>
	void Write(char value);

	/// <summary>
	/// Writes a string of text to the log sink.
	/// </summary>
	/// <param name="text">The text to write.</param>
	void Write(string text);

	/// <summary>
	/// Writes a string of text followed by a line terminator to the log sink.
	/// </summary>
	/// <param name="text">The text to write before the line terminator.</param>
	void WriteLine(string text);

	/// <summary>
	/// Flushes any buffered log data to the underlying target.
	/// </summary>
	void Flush();

	/// <summary>
	/// Rotates the log output if necessary, based on the size of upcoming data.
	/// </summary>
	/// <param name="upcomingBytes">The number of bytes that will be written next. 
	/// Used to determine if rotation should occur before writing.</param>
	void RotateIfNeeded(long upcomingBytes);
}