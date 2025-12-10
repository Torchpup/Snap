namespace Snap.Engine.Coroutines.Routines.Time;

/// <summary>
/// A coroutine-style enumerator that invokes a specified action at a fixed time interval.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IEnumerator"/> so it can be scheduled by 
/// the engine's coroutine system. It runs indefinitely until manually stopped 
/// via the coroutine manager.
/// </para>
/// <para>
/// Timing is based on <see cref="Clock.DeltaTime"/> and will trigger 
/// the provided action approximately every seconds.
/// </para>
/// </remarks>
public class EverySeconds : IEnumerator
{
	private readonly float _interval;
	private readonly Action _action;
	private float _elapsed;

	/// <summary>
	/// Always returns <c>null</c> for this enumerator.
	/// </summary>
	public object Current => null;

	/// <summary>
	/// Creates a new <see cref="EverySeconds"/> enumerator that executes an action at a fixed interval.
	/// </summary>
	/// <param name="interval">The time interval between executions, in seconds.</param>
	/// <param name="action">The action to invoke every interval.</param>
	public EverySeconds(float interval, Action action)
	{
		_interval = interval;
		_action = action;
		_elapsed = 0f;
	}

	/// <summary>
	/// Advances the enumerator, updating the elapsed time and invoking the action if the interval has passed.
	/// </summary>
	/// <returns>
	/// Always returns <c>true</c> so that the enumerator runs indefinitely until explicitly stopped.
	/// </returns>
	public bool MoveNext()
	{
		_elapsed += Clock.Instance.DeltaTime;
		if (_elapsed >= _interval)
		{
			_elapsed -= _interval;
			_action?.Invoke();
		}
		return true; // runs forever unless manually stopped
	}

	/// <summary>
	/// Reset is not supported for this enumerator.
	/// </summary>
	/// <exception cref="NotSupportedException">Always thrown when called.</exception>
	public void Reset() => throw new NotSupportedException();
}
