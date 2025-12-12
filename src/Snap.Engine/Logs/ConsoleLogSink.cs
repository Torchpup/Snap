namespace Snap.Engine.Logs;

/// <summary>
/// Provides a log sink that writes log entries directly to the standard console output.
/// </summary>
/// <remarks>
/// This sink is useful for debugging, development, or applications where logs should be visible
/// in real time on the console. Rotation is not applicable for console output.
/// </remarks>
public class ConsoleLogSink : ILogSink
{
	/// <summary>
	/// Writes a single character to the console output.
	/// </summary>
	/// <param name="value">The character to write.</param>
	public void Write(char value) => Console.Write(value);

	/// <summary>
	/// Writes a string of text to the console output.
	/// </summary>
	/// <param name="text">The text to write.</param>
	public void Write(string text) => Console.Write(text);

	/// <summary>
	/// Writes a string of text followed by a line terminator to the console output.
	/// </summary>
	/// <param name="text">The text to write before the line terminator.</param>
	public void WriteLine(string text) => Console.WriteLine(text);

	/// <summary>
	/// Flushes the console output stream to ensure all buffered data is written.
	/// </summary>
	public void Flush() => Console.Out.Flush();

	/// <summary>
	/// Performs rotation if needed. For console output this is a no-op.
	/// </summary>
	/// <param name="upcomingBytes">
	/// The number of bytes that are about to be written. Ignored for console output.
	/// </param>
	public void RotateIfNeeded(long upcomingBytes) { /* no-op for console */ }
}