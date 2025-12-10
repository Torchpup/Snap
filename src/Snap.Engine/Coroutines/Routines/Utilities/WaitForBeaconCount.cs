namespace Snap.Engine.Coroutines.Routines.Utilities;

/// <summary>
/// A coroutine that waits for a specified beacon topic to be emitted
/// a given number of times before completing.
/// </summary>
/// <remarks>
/// <para>
/// This coroutine subscribes to a beacon topic and increments an internal counter
/// whenever the beacon is emitted. An optional predicate can filter beacon instances.
/// The coroutine completes once the target count is reached or an optional timeout expires.
/// </para>
/// <para>
/// Typically used within the coroutine system to pause execution until a specific
/// in-game event has occurred a certain number of times.
/// </para>
/// </remarks>
public sealed class WaitForBeaconCount : IEnumerator
{
	private readonly string _topic;
	private readonly int _targetCount;
	private readonly Func<BeaconHandle, bool> _predicate;
	private readonly float _timeoutSeconds;

	private int _count;
	private bool _subscribed;
	private bool _done;
	private float _elapsed;

	/// <summary>
	/// Always null for <see cref="WaitForBeaconCount"/>.
	/// </summary>
	public object Current => null;

	private readonly Action<BeaconHandle> _handler;

	/// <summary>
	/// Initializes a new instance of the <see cref="WaitForBeaconCount"/> class.
	/// </summary>
	/// <param name="topic">The beacon topic to listen for. Must be non-empty.</param>
	/// <param name="count">The number of times the beacon must be emitted before completion.</param>
	/// <param name="predicate">Optional filter that must return <c>true</c> for a beacon to count.</param>
	/// <param name="timeoutSeconds">
	/// Optional timeout in seconds. Set to a negative value to wait indefinitely.
	/// </param>
	/// <exception cref="ArgumentException">Thrown if <paramref name="topic"/> is null or empty.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="count"/> is less than or equal to zero.</exception>
	public WaitForBeaconCount(string topic, int count, Func<BeaconHandle, bool> predicate = null, float timeoutSeconds = -1f)
	{
		if (string.IsNullOrEmpty(topic))
			throw new ArgumentException("Topic must be non-empty.", nameof(topic));
		if (count <= 0)
			throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive.");

		_topic = topic;
		_targetCount = count;
		_predicate = predicate;
		_timeoutSeconds = timeoutSeconds;
		_handler = OnBeacon;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="WaitForBeaconCount"/> class 
	/// using an enum topic value.
	/// </summary>
	/// <param name="topic">The beacon topic as an enum.</param>
	/// <param name="count">The number of times the beacon must be emitted before completion.</param>
	/// <param name="predicate">Optional filter that must return <c>true</c> for a beacon to count.</param>
	/// <param name="timeoutSeconds">
	/// Optional timeout in seconds. Set to a negative value to wait indefinitely.
	/// </param>
	public WaitForBeaconCount(Enum topic, int count, Func<BeaconHandle, bool> predicate = null, float timeoutSeconds = -1f)
		: this(topic.ToEnumString(), count, predicate, timeoutSeconds) { }

	/// <summary>
	/// Advances the coroutine.
	/// On the first call, subscribes to the specified beacon topic.
	/// Continues running until the target count or timeout is reached.
	/// </summary>
	/// <returns>
	/// <c>true</c> if the coroutine should continue running; <c>false</c> if it has completed.
	/// </returns>
	public bool MoveNext()
	{
		if (_done) return false;

		if (!_subscribed)
		{
			BeaconManager.Instance.Connect(_topic, _handler);
			_subscribed = true;
		}

		if (_timeoutSeconds >= 0f)
		{
			_elapsed += Clock.Instance.DeltaTime;
			if (_elapsed >= _timeoutSeconds)
			{
				Cleanup();
				_done = true;
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Reset is not supported for this coroutine.
	/// </summary>
	/// <exception cref="NotSupportedException">Always thrown when called.</exception>
	public void Reset() => throw new NotSupportedException();

	private void OnBeacon(BeaconHandle h)
	{
		if (_predicate == null || _predicate(h))
		{
			_count++;
			if (_count >= _targetCount)
			{
				Cleanup();
				_done = true;
			}
		}
	}

	private void Cleanup()
	{
		if (_subscribed)
		{
			try { BeaconManager.Instance.Disconnect(_topic, _handler); }
			catch { /* ignore */ }
			_subscribed = false;
		}
	}
}
