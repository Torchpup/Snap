namespace Snap.Engine.Beacons;

/// <summary>
/// Manages topic-based messaging between components using a lightweight pub/sub system.
/// </summary>
public sealed class BeaconManager
{
	private class Subscription
	{
		public object Owner;
		public Action<BeaconHandle> Handler;
	}

	// Internally container mapping each topic to its list of subscribers + a lock object.
	// We store a small "Holder" object so reach topic has its own lock.
	private class TopicEntry
	{
		public readonly object SyncRoot = new object();
		public List<Subscription> Subscribers = new();
	}

	/*
        Provides:
            1. O(1) Lookup on the TopEntry (just a directory lookup)
            2. O(n_subscribers) to create a snapshot copy of the subscribers list and to invoke 
               each callback
            3. Per-Topic locking, so different topics don't serialze on one global lock.
            4. Safe handling of subcribers/unsubscribers even while a publish is in progress (no
               InvalidOperationException from modifying a collection during emnumeration)
    */

	private static readonly object _publicOwner = new();
	private readonly Dictionary<uint, TopicEntry> _topics = [];
	private readonly object _mapLock = new();

	/// <summary>
	/// Singleton instance of the <see cref="BeaconManager"/>.
	/// </summary>
	public static BeaconManager Instance { get; private set; }

	/// <summary>
	/// Gets the number of active topic entries.
	/// </summary>
	public int Count => _topics.Count;

	internal BeaconManager() => Instance ??= this;

	/// <summary>
	/// Subscribes a handler to the specified topic, using an enum as the topic identifier.
	/// </summary>
	/// <param name="topic">An <see cref="Enum"/> value used to identify the topic.</param>
	/// <param name="handler">The method to invoke when the topic is emitted.</param>
	/// <exception cref="ArgumentException">Thrown if the topic is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the handler is null.</exception>
	public void Connect(Enum topic, Action<BeaconHandle> handler) =>
		Connect(topic.ToEnumString(), _publicOwner, handler);

	/// <summary>
	/// Subscribes a handler to the specified topic, using a string identifier.
	/// </summary>
	/// <param name="topic">The name of the topic.</param>
	/// <param name="handler">The method to invoke when the topic is emitted.</param>
	/// <exception cref="ArgumentException">Thrown if the topic is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the handler is null.</exception>
	public void Connect(string topic, Action<BeaconHandle> handler) =>
		Connect(topic, _publicOwner, handler);
	internal void Connect(string topic, object owner, Action<BeaconHandle> handler)
	{
		if (string.IsNullOrEmpty(topic))
			throw new ArgumentException("Topic must be a non-empty string", nameof(topic));
		if (handler == null)
			throw new ArgumentNullException(nameof(handler));

		uint hash = HashHelpers.Cache32(topic);
		TopicEntry entry;
		lock (_mapLock)
		{
			if (!_topics.TryGetValue(hash, out entry))
			{
				entry = new TopicEntry();
				_topics[hash] = entry;
			}
		}

		lock (entry.SyncRoot)
		{
			entry.Subscribers.Add(new Subscription
			{
				Owner = owner,
				Handler = handler
			});
		}
	}

	/// <summary>
	/// Unsubscribes a handler from the specified topic.
	/// </summary>
	/// <param name="topic">The name of the topic to unsubscribe from.</param>
	/// <param name="handler">The handler method to remove.</param>
	public void Disconnect(string topic, Action<BeaconHandle> handler) =>
		Disconnect(topic, _publicOwner, handler);

	/// <summary>
	/// Unsubscribes a handler from the specified topic, using an enum identifier.
	/// </summary>
	/// <param name="topic">The topic enum value to unsubscribe from.</param>
	/// <param name="handler">The handler method to remove.</param>
	public void Disconnect(Enum topic, Action<BeaconHandle> handler) =>
		Disconnect(topic.ToEnumString(), _publicOwner, handler);
	internal void Disconnect(string topic, object owner, Action<BeaconHandle> handler)
	{
		if (string.IsNullOrEmpty(topic) || handler == null) return;

		TopicEntry entry;
		uint hash = HashHelpers.Cache32(topic);

		lock (_mapLock)
			if (!_topics.TryGetValue(hash, out entry)) return; // No such topic, nothing to do.

		lock (entry.SyncRoot)
		{
			entry.Subscribers.RemoveAll(s => ReferenceEquals(s.Owner, owner)
				&& s.Handler == handler);

			//If no more subscribers, clea up topic entry
			if (entry.Subscribers.Count == 0)
			{
				lock (_mapLock)
				{
					_topics.Remove(hash);
				}
			}
		}
	}

	/// <summary>
	/// Emits the specified topic to all subscribers after a delay.
	/// </summary>
	/// <param name="topic">The topic enum to emit.</param>
	/// <param name="seconds">The delay in seconds before emitting.</param>
	/// <param name="args">Optional arguments to pass to the handler.</param>
	public void EmitDelayed(Enum topic, float seconds, params object[] args) =>
		EmitDelayed(_publicOwner, topic.ToEnumString(), seconds, args);

	/// <summary>
	/// Emits the specified topic to all subscribers after a delay.
	/// </summary>
	/// <param name="topic">The topic name to emit.</param>
	/// <param name="seconds">The delay in seconds before emitting.</param>
	/// <param name="args">Optional arguments to pass to the handler.</param>
	public void EmitDelayed(string topic, float seconds, params object[] args) =>
		EmitDelayed(_publicOwner, topic, seconds, args);
	internal void EmitDelayed(object owner, string topic, float seconds, params object[] args)
	{
		IEnumerator Delayed(float seconds)
		{
			yield return new WaitForSeconds(seconds);

			Emit(topic, args);
		}

		CoroutineManager.Instance.Start(Delayed(seconds), owner);
	}

	/// <summary>
	/// Emits the specified topic immediately to all subscribers.
	/// </summary>
	/// <param name="topic">The topic enum to emit.</param>
	/// <param name="args">Optional arguments to pass to the handler.</param>
	/// <exception cref="ArgumentException">Thrown if the topic is null or empty.</exception>
	public void Emit(Enum topic, params object[] args) =>
		Emit(topic.ToEnumString(), args);

	/// <summary>
	/// Emits the specified topic immediately to all subscribers.
	/// </summary>
	/// <param name="topic">The topic name to emit.</param>
	/// <param name="args">Optional arguments to pass to the handler.</param>
	/// <exception cref="ArgumentException">Thrown if the topic is null or empty.</exception>
	public void Emit(string topic, params object[] args)
	{
		if (string.IsNullOrEmpty(topic))
			throw new ArgumentException("Topic must be a non-empty string", nameof(topic));

		TopicEntry entry;
		uint hash = HashHelpers.Cache32(topic);

		lock (_mapLock)
		{
			if (!_topics.TryGetValue(hash, out entry))
			{
				// No subsribers for this topic => nothing to do.
				return;
			}
		}

		List<Subscription> subscribersSnapshot;
		lock (entry.SyncRoot)
		{
			subscribersSnapshot = new List<Subscription>(entry.Subscribers);
		}

		var handle = new BeaconHandle(topic, args == null || args.Length == 0
			? Array.Empty<object>() : args);

		foreach (var sub in subscribersSnapshot)
		{
			sub.Handler.Invoke(handle);

			// try
			// {

			// }
			// catch (Exception ex)
			// {
			// 	// throw new Exception($"{ex}");
			// }
		}
	}

	internal void ClearOwner(object owner)
	{
		if (owner == null)
			throw new ArgumentNullException(nameof(owner));

		lock (_mapLock)
		{
			foreach (var kv in _topics.ToList())
			{
				var entry = kv.Value;
				lock (entry.SyncRoot)
				{
					entry.Subscribers.RemoveAll(s => ReferenceEquals(s.Owner, owner));
					if (entry.Subscribers.Count == 0)
						_topics.Remove(kv.Key);
				}
			}
		}
	}

	internal void ClearPublicSubscriptions() => ClearOwner(_publicOwner);










	//Attr:
	private static readonly HashSet<object> InitializeOwners = new();

	internal static void Initialize(object owner)
	{
		ArgumentNullException.ThrowIfNull(owner);

		if (InitializeOwners.Contains(owner))
			return;
		InitializeOwners.Add(owner);

		var ownerType = owner.GetType();
		var flags = BindingFlags.Public
				| BindingFlags.NonPublic
				| BindingFlags.Instance
				| BindingFlags.Static
				| BindingFlags.DeclaredOnly
				;
		var beaconMethods = new List<MethodInfo>();

		for (Type t = ownerType; t != null; t = t.BaseType)
		{
			beaconMethods.AddRange(
				t.GetMethods(flags)
				 .Where(m => m.GetCustomAttribute<BeaconAttribute>() != null)
			);
		}

		var byTopic = beaconMethods
			.GroupBy(m => m.GetCustomAttribute<BeaconAttribute>().Topic);

		foreach (var method in byTopic)
		{
			var best = method
				.OrderBy(m => InherinaceDistance(ownerType, m.DeclaringType))
				.First();

			var attr = best.GetCustomAttribute<BeaconAttribute>();
			var topic = attr.Topic;
			var strict = attr.Strict;
			var pars = best.GetParameters();

			Action<BeaconHandle> handler = h =>
			{
				if (strict && h.Args.Length != pars.Length)
				{
					Logger.Instance.Log(LogLevel.Warning, $"[Beacon: {topic}] Handler {best.Name} excepts {pars.Length} args but got {h.Args.Length}. Skipping.");
					return;
				}

				if (strict)
				{
					for (int i = 0; i < pars.Length; i++)
					{
						var expected = pars[i].ParameterType;
						var actual = i < h.Args.Length ? h.Args[i].GetType() : null;

						if (actual == null || !expected.IsAssignableFrom(actual))
						{
							Logger.Instance.Log(LogLevel.Warning, $"[Beacon: {topic}] Param#{i} mismatch for '{best.Name}' expects {expected.Name}, got {actual?.Name ?? "null"}. Skipping.");
							return;
						}
					}
				}

				var callArgs = new object[pars.Length];
				for (int i = 0; i < pars.Length; i++)
				{
					if (i < h.Args.Length && pars[i].ParameterType.IsInstanceOfType(h.Args[i]!))
						callArgs[i] = h.Args[i];
					else
						callArgs[i] = GetDefault(pars[i].ParameterType);
				}

				best.Invoke(best.IsStatic ? null : owner, callArgs);
			};

			Instance.Connect(topic, owner, handler);
		}
	}
	private static int InherinaceDistance(Type owner, Type declType)
	{
		int dist = 0;
		for (var t = owner; t != null; t = t.BaseType, dist++)
		{
			if (t == declType)
				return dist;
		}

		return int.MaxValue;
	}

	private static object GetDefault(Type t) =>
		t.IsValueType ? Activator.CreateInstance(t) : null;
}

/// <summary>
/// Attribute used to mark a method as a beacon handler for a specific topic.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class BeaconAttribute : Attribute
{
	/// <summary>
	/// The topic this handler responds to. Can be a <see cref="string"/> or an <see cref="Enum"/>.
	/// </summary>
	public object Name { get; set; }

	/// <summary>
	/// Whether to enforce strict argument checking before invoking the handler.
	/// </summary>
	/// <remarks>
	/// If true, the handler will only be invoked if the number and types of arguments match exactly.
	/// If false, default values may be used to fill in missing or mismatched arguments.
	/// </remarks>
	public bool Strict { get; set; }

	internal string Topic => Name is Enum e
		? $"{e.GetType().FullName}.{e}"
		: Name?.ToString() ?? throw new InvalidOperationException("Beacon.Name was null");

	/// <summary>
	/// Creates a new <see cref="BeaconAttribute"/>.
	/// </summary>
	public BeaconAttribute() { }
}
