namespace Snap.Engine.Coroutines.Routines.Utilities;

/// <summary>
/// A coroutine that waits until a specific beacon topic is emitted.
/// Optional predicate can filter the beacon by inspecting the <see cref="BeaconHandle"/>.
/// Supports an optional timeout. Automatically unsubscribes when done.
/// </summary>
public sealed class WaitForBeacon : IEnumerator
{
	private readonly string _topic;
	private readonly Func<BeaconHandle, bool> _predicate;
	private readonly float _timeoutSeconds;
	private bool _subscribed;
	private bool _done;
	private float _elapsed;

	/// <summary>
	/// The last beacon handle that satisfied the wait, if any.
	/// Null if the wait ended due to timeout.
	/// </summary>
	public BeaconHandle? Result { get; private set; }

	/// <summary>
	/// Always null for <see cref="WaitForBeacon"/>.
	/// </summary>
	public object Current => null;

	private readonly Action<BeaconHandle> _handler;

	/// <summary>
	/// Creates a wait for a string topic.
	/// </summary>
	/// <param name="topic">Topic name to subscribe to.</param>
	/// <param name="predicate">
	/// Optional filter run for each beacon. If null, the first beacon on the topic completes the wait.
	/// </param>
	/// <param name="timeoutSeconds">
	/// Optional timeout in seconds. Use a negative value to wait forever.
	/// </param>
	public WaitForBeacon(string topic, Func<BeaconHandle, bool> predicate = null, float timeoutSeconds = -1f)
	{
		if (string.IsNullOrEmpty(topic))
			throw new ArgumentException("Topic must be non-empty.", nameof(topic));

		_topic = topic;
		_predicate = predicate;
		_timeoutSeconds = timeoutSeconds;
		_handler = OnBeacon;
	}

	/// <summary>
	/// Creates a wait for an enum topic.
	/// </summary>
	/// <param name="topic">Enum topic to subscribe to.</param>
	/// <param name="predicate">Optional filter. See <see cref="WaitForBeacon(string, Func{BeaconHandle, bool}, float)"/>.</param>
	/// <param name="timeoutSeconds">Optional timeout in seconds. Negative to wait forever.</param>
	public WaitForBeacon(Enum topic, Func<BeaconHandle, bool> predicate = null, float timeoutSeconds = -1f)
		: this(topic.ToEnumString(), predicate, timeoutSeconds) { }

	/// <summary>
	/// Advances the wait. Subscribes on first tick, then completes when a matching beacon arrives
	/// or the timeout elapses.
	/// </summary>
	/// <returns>True while waiting. False when completed or timed out.</returns>
	public bool MoveNext()
	{
		if (_done) return false;

		// Lazy subscribe on first tick to avoid holding a dangling handler if the routine never starts.
		if (!_subscribed)
		{
			BeaconManager.Instance.Connect(_topic, _handler);
			_subscribed = true;
		}

		// Check timeout
		if (_timeoutSeconds >= 0f)
		{
			_elapsed += Clock.Instance.DeltaTime;
			if (_elapsed >= _timeoutSeconds)
			{
				Cleanup();
				Result = null; // timed out
				_done = true;
				return false;
			}
		}

		// Still waiting
		return true;
	}

	/// <summary>
	/// Not supported.
	/// </summary>
	public void Reset() => throw new NotSupportedException();

	private void OnBeacon(BeaconHandle handle)
	{
		if (_done) return;

		if (_predicate == null || _predicate(handle))
		{
			Result = handle;
			Cleanup();
			_done = true;
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
