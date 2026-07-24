namespace Snap.Engine.Sounds;

/// <summary>
/// Provides a pooling mechanism for managing and reusing <see cref="SoundInstance"/> objects.
/// This static class optimizes audio playback by recycling sound instances rather than creating new ones,
/// reducing garbage collection pressure and improving performance for frequently played sounds.
/// </summary>
/// <remarks>
/// <para>
/// The pool maintains separate instance limits for both global usage (<see cref="MaxGlobalInstances"/>)
/// and per-sound usage (<see cref="MaxInstancePerSound"/>). When these limits are approached,
/// the pool employs a least-recently-used strategy to determine which instances to recycle.
/// </para>
/// <para>
/// Use this class through the <see cref="Sound"/> playback methods rather than directly creating
/// <see cref="SoundInstance"/> objects. The pool automatically manages instance lifecycle,
/// including disposal of instances that exceed their maximum lifetime.
/// </para>
/// <para>
/// This implementation is thread-safe for concurrent access and suitable for games with
/// dynamic audio requirements where sounds are frequently started and stopped.
/// </para>
/// </remarks>
/// <example>
/// The pool is typically used indirectly when playing sounds:
/// <code>
/// // The sound instance is obtained from the pool automatically
/// var instance = mySound.Play();
/// 
/// // When the sound finishes, it returns to the pool automatically
/// instance.Stop();
/// </code>
/// </example>
/// <seealso cref="SoundInstance"/>
/// <seealso cref="Sound"/>
public static class SoundInstancePool
{
	private static readonly List<SoundInstance> _activeInstances = new(256);
	private static readonly Queue<SoundInstance> _availbleInstances = new(64);
	private static readonly Dictionary<uint, List<SoundInstance>> SoundToInstances = [];
	private static readonly object Lock = new();
	private static uint s_nextInstanceId = 1;

	/// <summary>
	/// Gets the number of sound instances currently active and playing.
	/// </summary>
	/// <value>
	/// The total count of active sound instances.
	/// </value>
	public static int ActiveInstances => _activeInstances.Count;

	/// <summary>
	/// Gets the number of sound instances currently available in the pool for reuse.
	/// </summary>
	/// <value>
	/// The total count of available sound instances in the pool.
	/// </value>
	public static int AvailbleInstances => _availbleInstances.Count;

	/// <summary>
	/// The maximum number of sound instances that can be active globally across all sounds.
	/// This limit prevents excessive audio resource usage and potential performance issues.
	/// </summary>
	/// <remarks>
	/// When this limit is reached, attempts to play additional sounds may fail or trigger
	/// instance recycling based on the pool's configuration. This limit helps manage
	/// memory and processing overhead in audio-intensive applications.
	/// </remarks>
	public const int MaxGlobalInstances = 255;

	/// <summary>
	/// The maximum number of instances that can be created for any individual sound asset.
	/// This prevents a single sound from monopolizing the available instance pool.
	/// </summary>
	/// <remarks>
	/// This limit is useful for preventing issues where rapidly repeating sounds (like gunfire
	/// or footsteps) could consume all available instances. When this limit is reached for a
	/// particular sound, the oldest instance of that sound may be recycled for new playback.
	/// </remarks>
	public const int MaxInstancePerSound = 16;

	/// <summary>
	/// Attempts to retrieve or create a sound instance for playing the specified sound.
	/// This method implements the core pooling logic with multi-tiered instance management.
	/// </summary>
	/// <param name="sound">The sound asset to play. Must not be null.</param>
	/// <returns>
	/// A ready-to-play <see cref="SoundInstance"/> for the specified sound, or <c>null</c>
	/// if instance limits have been reached and no suitable instances are available.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method employs a sophisticated multi-tiered approach to instance management:
	/// <list type="number">
	/// <item>
	/// <description>
	/// First, checks if the specific sound has reached its per-sound instance limit.
	/// If so, looks for a stopped instance of that same sound to reuse.
	/// </description>
	/// </item>
	/// <item>
	/// <description>
	/// If no sound-specific instances are available, checks the general available instance queue
	/// for any reusable stopped instances.
	/// </description>
	/// </item>
	/// <item>
	/// <description>
	/// If the global instance limit has been reached, searches for the least-recently-used
	/// stopped instance across all sounds to recycle.
	/// </description>
	/// </item>
	/// <item>
	/// <description>
	/// Finally, if all else fails and global limits permit, creates a new sound instance.
	/// </description>
	/// </item>
	/// </list>
	/// </para>
	/// <para>
	/// This strategy prioritizes reuse of existing instances while respecting both per-sound
	/// and global resource limits. The method is thread-safe and suitable for use from
	/// multiple threads simultaneously.
	/// </para>
	/// <para>
	/// When instance limits are reached, the method logs warnings and returns <c>null</c>,
	/// indicating that the sound cannot be played at this time.
	/// </para>
	/// </remarks>
	public static SoundInstance GetInstance(Sound sound)
	{
		lock (Lock)
		{
			if (SoundToInstances.TryGetValue(sound.Id, out var soundInstances))
			{
				if (soundInstances.Count >= MaxInstancePerSound)
				{
					var reusable = soundInstances.FirstOrDefault(x => x.Status == Sounds.SoundStatus.Stopped);

					if (reusable != null)
					{
						reusable.Reset(sound);
						return reusable;
					}

					Logger.Instance.Log(LogLevel.Warning, $"[Audio] Sounds {sound.Id} has reached instances limit ({MaxInstancePerSound})");

					return null;
				}
			}

			if (_availbleInstances.Count > 0)
			{
				var instance = _availbleInstances.Dequeue();
				instance.Reset(sound);
				RegisterInstance(instance, sound);
				return instance;
			}

			if (_activeInstances.Count >= MaxGlobalInstances)
			{
				var oldestStopped = _activeInstances
					.Where(x => x.Status == Sounds.SoundStatus.Stopped)
					.OrderBy(x => x.LastUsedTime)
					.FirstOrDefault();

				if (oldestStopped != null)
				{
					UnregisterInstance(oldestStopped);
					oldestStopped.Reset(sound);
					RegisterInstance(oldestStopped, sound);
					return oldestStopped;
				}

				Logger.Instance.Log(LogLevel.Warning,
					$"[Audio] Global sound instance limit reached ({MaxGlobalInstances})");

				return null;
			}

			var newInstance = new SoundInstance(s_nextInstanceId++, sound, sound.Buffer);
			RegisterInstance(newInstance, sound);
			_activeInstances.Add(newInstance);

			return newInstance;
		}
	}

	/// <summary>
	/// Returns a sound instance to the pool for potential reuse.
	/// This method stops playback and makes the instance available for future sound requests.
	/// </summary>
	/// <param name="instance">The sound instance to release. Can be null or invalid, in which case the method does nothing.</param>
	/// <remarks>
	/// <para>
	/// This method performs several cleanup actions:
	/// <list type="bullet">
	///   <item><description>Unregisters the instance from sound-specific tracking</description></item>
	///   <item><description>Stops any ongoing playback immediately</description></item>
	///   <item><description>Places the instance in the available instances queue for future reuse</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// After this method is called, the instance remains in a valid state and can be
	/// retrieved again for playing other sounds. The method gracefully handles null or
	/// invalid instances, making it safe to call without additional validation.
	/// </para>
	/// <para>
	/// Use this method when you are finished with a sound instance and want to return
	/// its resources to the pool. This helps maintain optimal performance by keeping
	/// the pool populated with reusable instances.
	/// </para>
	/// </remarks>
	/// <example>
	/// Typically used after a sound has finished playing or when explicitly stopping a sound
	/// that you want to return to the pool immediately.
	/// </example>
	public static void ReleaseInstance(SoundInstance instance)
	{
		lock (Lock)
		{
			if (instance == null || !instance.IsValid)
				return;

			UnregisterInstance(instance);

			instance.Stop();
			_availbleInstances.Enqueue(instance);
		}
	}

	/// <summary>
	/// Handles cleanup operations when a sound asset is being disposed.
	/// This method ensures all instances associated with the disposed sound are properly released.
	/// </summary>
	/// <param name="sound">The sound asset that is being disposed.</param>
	/// <remarks>
	/// <para>
	/// When a sound asset is disposed, this method performs comprehensive cleanup:
	/// <list type="bullet">
	///   <item><description>Stops playback on all instances using this sound</description></item>
	///   <item><description>Returns each associated instance to the available pool for reuse with other sounds</description></item>
	///   <item><description>Removes the sound from the internal tracking dictionary</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// This prevents memory leaks and ensures that instances bound to a disposed sound
	/// don't attempt to access invalid audio data. The method creates a copy of the
	/// instance list before processing to safely handle modifications during iteration.
	/// </para>
	/// <para>
	/// This method should be called by the sound disposal process to maintain pool
	/// integrity and prevent orphaned instances from accumulating in the system.
	/// </para>
	/// </remarks>
	/// <seealso cref="Sound.Dispose"/>
	public static void OnSoundDispose(Sound sound)
	{
		lock (Lock)
		{
			if (SoundToInstances.TryGetValue(sound.Id, out var instances))
			{
				foreach (var instance in instances.ToList())
				{
					instance.Stop();
					ReleaseInstance(instance);
				}

				SoundToInstances.Remove(sound.Id);
			}
		}
	}

	private static void RegisterInstance(SoundInstance instance, Sound sound)
	{
		instance.LastUsedTime = DateTime.Now;

		if (!SoundToInstances.TryGetValue(sound.Id, out var list))
		{
			list = new List<SoundInstance>();
			SoundToInstances[sound.Id] = list;
		}

		list.Add(instance);
	}

	private static void UnregisterInstance(SoundInstance instance)
	{
		if (instance != null && instance.Sound != null &&
		SoundToInstances.TryGetValue(instance.Sound.Id, out var list))
		{
			list.Remove(instance);

			if (list.Count == 0)
				SoundToInstances.Remove(instance.Sound.Id);
		}

		_activeInstances.Remove(instance);
	}

	/// <summary>
	/// Retrieves detailed statistics about the current state of the sound instance pool.
	/// This method provides insight into pool utilization and instance distribution.
	/// </summary>
	/// <returns>
	/// A tuple containing three statistics:
	/// <list type="bullet">
	///   <item><term>active</term><description>The count of instances currently in use for active playback</description></item>
	///   <item><term>available</term><description>The count of instances currently idle and available for reuse</description></item>
	///   <item><term>perSound</term><description>A dictionary mapping sound IDs to the number of instances allocated for each sound</description></item>
	/// </list>
	/// </returns>
	/// <remarks>
	/// <para>
	/// Use this method for debugging, monitoring, or profiling the pool's behavior.
	/// The statistics can help identify:
	/// <list type="bullet">
	///   <item><description>Whether the pool is nearing its capacity limits</description></item>
	///   <item><description>Which sounds are consuming the most instances</description></item>
	///   <item><description>The balance between active and available instances</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// This method provides a thread-safe snapshot of pool state at the moment of calling.
	/// The values represent instantaneous counts that may change immediately after the method returns.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// var stats = SoundInstancePool.GetStats();
	/// Console.WriteLine($"Active: {stats.active}, Available: {stats.available}");
	/// foreach (var kv in stats.perSound)
	/// {
	///     Console.WriteLine($"  Sound {kv.Key}: {kv.Value} instances");
	/// }
	/// </code>
	/// </example>
	public static (int active, int available, Dictionary<uint, int> perSound) GetStats()
	{
		lock (Lock)
		{
			var perSoundCounts = SoundToInstances.ToDictionary(
				kv => kv.Key,
				kv => kv.Value.Count
			);

			return (_activeInstances.Count, _availbleInstances.Count, perSoundCounts);
		}
	}


	/// <summary>
	/// Determines whether there are any actively playing instances of the specified sound.
	/// </summary>
	/// <param name="sound">The sound to check for active playback instances.</param>
	/// <returns>
	/// <c>true</c> if the sound has at least one instance currently playing or paused;
	/// otherwise, <c>false</c> if the sound has no instances or all its instances are stopped.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method checks for instances in any non-stopped state, including playing, paused,
	/// or any other active playback status. It only counts valid instances that are still
	/// functional and properly associated with the sound.
	/// </para>
	/// <para>
	/// Useful for preventing duplicate sound playback, implementing sound cooldowns,
	/// or determining if cleanup can proceed when stopping all instances of a sound.
	/// </para>
	/// </remarks>
	public static bool HasActiveInstances(Sound sound)
	{
		lock (Lock)
		{
			if (SoundToInstances.TryGetValue(sound.Id, out var instances))
			{
				return instances.Any(x => x.IsValid && x.Status != SoundStatus.Stopped);
			}

			return false;
		}
	}
}
